using System.Net;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Net.Mail;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace GymDesk.Services;

public class GymProfileFilter(GymDbContext db) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        GymProfile? profile = null;
        try
        {
            profile = await db.GymProfiles.OrderBy(item => item.Id).FirstOrDefaultAsync();
        }
        catch (Exception)
        {
            profile = null;
        }

        context.HttpContext.Items["GymProfile"] = profile;
        if (context.Controller is Controller controller)
        {
            controller.ViewData["BusinessName"] = profile?.BusinessName ?? "Gym Desk";
            controller.ViewData["Currency"] = profile?.CurrencyCode ?? "INR";
            controller.ViewData["City"] = profile?.City ?? "";
        }

        await next();
    }
}

public class SignInService(IHttpContextAccessor http)
{
    public async Task SignInAsync(int id, string name, string role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, id.ToString()),
            new(ClaimTypes.Name, name),
            new(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await http.HttpContext!.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));
    }

    public Task SignOutAsync() =>
        http.HttpContext!.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
}

public class ActivityLogService(GymDbContext db, IHttpContextAccessor http)
{
    public async Task WriteAsync(string message, string severity = "info")
    {
        int? staffId = null;
        var user = http.HttpContext?.User;
        if (user?.IsInRole(AppRoles.Staff) == true || user?.IsInRole(AppRoles.Manager) == true)
        {
            staffId = user.GetUserId();
            if (staffId == 0)
            {
                staffId = null;
            }
        }

        db.ActivityLogs.Add(new ActivityLog
        {
            StaffUserId = staffId,
            Message = message.Length > 500 ? message[..500] : message,
            Severity = severity,
            OccurredAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }
}

public class CartService(IHttpContextAccessor http)
{
    private const string SessionKey = "gymdesk-cart";

    public List<CartLine> Get()
    {
        var json = http.HttpContext?.Session.GetString(SessionKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<CartLine>>(json) ?? [];
    }

    public void Save(List<CartLine> lines) =>
        http.HttpContext!.Session.SetString(SessionKey, JsonSerializer.Serialize(lines));

    public void Add(CartLine line)
    {
        var lines = Get();
        var existing = lines.FirstOrDefault(item =>
            item.Kind == line.Kind && item.ReferenceId == line.ReferenceId && line.Kind != "topup");
        if (existing is null)
        {
            lines.Add(line);
        }
        else
        {
            existing.Quantity += line.Quantity;
            existing.UnitPrice = line.UnitPrice;
            existing.Name = line.Name;
        }

        Save(lines);
    }

    public void RemoveAt(int index)
    {
        var lines = Get();
        if (index >= 0 && index < lines.Count)
        {
            lines.RemoveAt(index);
            Save(lines);
        }
    }

    public void Clear() => http.HttpContext?.Session.Remove(SessionKey);
}

public class SalesService(GymDbContext db, CartService cart)
{
    public async Task<(bool Ok, string Error)> CheckoutAsync(int memberId, string channel)
    {
        var lines = cart.Get();
        if (lines.Count == 0)
        {
            return (false, "The cart is empty.");
        }

        if (channel is not (PaymentChannels.Cash or PaymentChannels.Card or PaymentChannels.Balance))
        {
            return (false, "Choose a payment method.");
        }

        if (channel == PaymentChannels.Balance && lines.Any(line => line.Kind == "topup"))
        {
            return (false, "Account credit cannot be paid from the account balance.");
        }

        var profile = await db.GymProfiles.OrderBy(item => item.Id).FirstAsync();
        if (channel == PaymentChannels.Cash && !profile.AcceptCash)
        {
            return (false, "Cash payments are turned off.");
        }

        if (channel == PaymentChannels.Card && !profile.AcceptCard)
        {
            return (false, "Card payments are turned off.");
        }

        if (channel == PaymentChannels.Balance && !profile.AcceptBalance)
        {
            return (false, "Balance payments are turned off.");
        }

        var member = await db.Members.FirstOrDefaultAsync(item => item.Id == memberId && item.IsConfirmed);
        if (member is null)
        {
            return (false, "Choose a confirmed member.");
        }

        var priced = new List<CartLine>();
        foreach (var line in lines)
        {
            if (line.Quantity < 1)
            {
                return (false, "Quantity must be at least 1.");
            }

            if (line.Kind == "product")
            {
                var product = await db.Products.FirstOrDefaultAsync(item => item.Id == line.ReferenceId);
                if (product is null || product.StockQuantity < line.Quantity)
                {
                    return (false, $"Not enough stock for {line.Name}.");
                }

                priced.Add(new CartLine
                {
                    Kind = "product",
                    ReferenceId = product.Id,
                    Name = product.Name,
                    Quantity = line.Quantity,
                    UnitPrice = product.Price
                });
            }
            else if (line.Kind == "plan")
            {
                var plan = await db.MembershipPlans.FirstOrDefaultAsync(item => item.Id == line.ReferenceId && item.IsActive);
                if (plan is null)
                {
                    return (false, "A membership plan in the cart is no longer available.");
                }

                priced.Add(new CartLine
                {
                    Kind = "plan",
                    ReferenceId = plan.Id,
                    Name = plan.Name,
                    Quantity = line.Quantity,
                    UnitPrice = plan.Price
                });
            }
            else if (line.Kind == "topup")
            {
                if (line.UnitPrice <= 0)
                {
                    return (false, "Credit amount must be greater than zero.");
                }

                priced.Add(line);
            }
            else
            {
                return (false, "The cart contains an unknown item.");
            }
        }

        var total = priced.Sum(line => line.LineTotal);
        if (channel == PaymentChannels.Balance && member.AccountBalance < total)
        {
            return (false, "The account balance does not cover this sale.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
        foreach (var line in priced.Where(item => item.Kind == "product"))
        {
            var product = await db.Products.FirstAsync(item => item.Id == line.ReferenceId);
            product.StockQuantity -= line.Quantity;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        foreach (var line in priced.Where(item => item.Kind == "plan"))
        {
            var plan = await db.MembershipPlans.FirstAsync(item => item.Id == line.ReferenceId);
            for (var copy = 0; copy < line.Quantity; copy++)
            {
                db.MemberPasses.Add(new MemberPass
                {
                    MemberId = member.Id,
                    PlanName = plan.Name,
                    PurchasedOn = today,
                    ExpiresOn = today.AddDays(plan.ValidDays),
                    VisitsRemaining = plan.VisitAllowance
                });
            }
        }

        foreach (var line in priced.Where(item => item.Kind == "topup"))
        {
            member.AccountBalance += line.LineTotal;
        }

        if (channel == PaymentChannels.Balance)
        {
            member.AccountBalance -= total;
        }

        var description = string.Join(", ", priced.Select(line => line.Quantity > 1 ? $"{line.Name} x{line.Quantity}" : line.Name));
        db.Invoices.Add(new Invoice
        {
            MemberId = member.Id,
            Description = description.Length > 255 ? description[..255] : description,
            Amount = total,
            Status = "Paid",
            PaymentChannel = channel,
            IssuedAt = DateTime.UtcNow
        });

        if (channel is PaymentChannels.Cash or PaymentChannels.Card)
        {
            var revenue = await db.RevenueEntries.FirstOrDefaultAsync(item => item.BusinessDate == today);
            if (revenue is null)
            {
                revenue = new RevenueEntry { BusinessDate = today };
                db.RevenueEntries.Add(revenue);
            }

            if (channel == PaymentChannels.Cash)
            {
                revenue.CashTotal += total;
            }
            else
            {
                revenue.CardTotal += total;
            }
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        cart.Clear();
        return (true, "");
    }
}

public class MailService
{
    public async Task<bool> TrySendAsync(GymProfile profile, string to, string subject, string body)
    {
        if (string.IsNullOrWhiteSpace(profile.SmtpHost) || string.IsNullOrWhiteSpace(to))
        {
            return false;
        }

        var from = string.IsNullOrWhiteSpace(profile.Email) ? profile.SmtpUser : profile.Email;
        if (string.IsNullOrWhiteSpace(from))
        {
            return false;
        }

        try
        {
#pragma warning disable SYSLIB0014
            using var client = new SmtpClient(profile.SmtpHost, profile.SmtpPort <= 0 ? 25 : profile.SmtpPort)
            {
                EnableSsl = profile.SmtpUseTls
            };
#pragma warning restore SYSLIB0014
            if (!string.IsNullOrWhiteSpace(profile.SmtpUser))
            {
                client.Credentials = new NetworkCredential(profile.SmtpUser, profile.SmtpPassword);
            }

            using var message = new MailMessage(from, to, subject, body);
            await client.SendMailAsync(message);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}

public class UploadStore(IWebHostEnvironment environment)
{
    public async Task<string> SaveImageAsync(IFormFile file, string folder)
    {
        if (file.Length == 0 || file.Length > 2 * 1024 * 1024)
        {
            throw new InvalidOperationException("Use an image of 2 MB or smaller.");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp"))
        {
            throw new InvalidOperationException("Use a JPG, PNG, or WEBP image.");
        }

        var directory = Path.Combine(environment.WebRootPath, "uploads", folder);
        Directory.CreateDirectory(directory);
        var name = $"{Guid.NewGuid():N}{extension}";
        await using var stream = File.Create(Path.Combine(directory, name));
        await file.CopyToAsync(stream);
        return $"/uploads/{folder}/{name}";
    }
}

public static class UserExtensions
{
    public static int GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var id) ? id : 0;
    }
}

public static class MemberCodes
{
    public static async Task<string> CreateAsync(GymDbContext db)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var code = Random.Shared.Next(10000000, 99999999).ToString();
            if (!await db.Members.AnyAsync(member => member.MemberCode == code))
            {
                return code;
            }
        }

        throw new InvalidOperationException("Could not assign a member code.");
    }
}
