using System.Diagnostics;
using Microsoft.AspNetCore.Identity;

namespace GymDesk.Controllers;

public class HomeController(GymDbContext db, MailService mail) : DeskController
{
    public async Task<IActionResult> Index()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var page = new HomePage
        {
            Profile = await CurrentProfile(),
            Hours = await db.OpeningHours.OrderBy(hour => hour.DayOfWeek).ToListAsync(),
            TodayException = await db.OpeningExceptions.FirstOrDefaultAsync(item => item.ExceptionDate == today),
            Plans = await db.MembershipPlans.Where(plan => plan.IsActive).OrderBy(plan => plan.Price).Take(3).ToListAsync(),
            TrainerCount = await db.Trainers.CountAsync()
        };
        return View(page);
    }

    public async Task<IActionResult> Plans() =>
        View(await db.MembershipPlans.Where(plan => plan.IsActive).OrderBy(plan => plan.Price).ToListAsync());

    public async Task<IActionResult> Trainers() =>
        View(await db.Trainers.OrderBy(trainer => trainer.FullName).ToListAsync());

    public async Task<IActionResult> Schedule() =>
        View(await db.ClassSlots.OrderBy(slot => slot.StartsAt).ToListAsync());

    public async Task<IActionResult> Rules() => View(await CurrentProfile());

    public IActionResult Contact() => View(new ContactInput());

    [HttpPost]
    public async Task<IActionResult> Contact(ContactInput input)
    {
        if (!ModelState.IsValid)
        {
            return View(input);
        }

        db.ContactMessages.Add(new ContactMessage
        {
            SenderName = input.SenderName,
            SenderEmail = input.SenderEmail,
            Body = input.Body,
            ReceivedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var profile = await CurrentProfile();
        await mail.TrySendAsync(profile, profile.Email, $"Message from {input.SenderName}", input.Body);
        Note("Your message is with the front desk.");
        return RedirectToAction(nameof(Contact));
    }

    public async Task<IActionResult> Board()
    {
        ViewBag.Hours = await db.OpeningHours.OrderBy(hour => hour.DayOfWeek).ToListAsync();
        ViewBag.Slots = await db.ClassSlots.OrderBy(slot => slot.StartsAt).ToListAsync();
        return View(await CurrentProfile());
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });

    private async Task<GymProfile> CurrentProfile() =>
        await db.GymProfiles.OrderBy(item => item.Id).FirstAsync();
}

public class AccountController(
    GymDbContext db,
    SignInService signIn,
    IPasswordHasher<Member> passwords,
    MailService mail,
    UploadStore uploads) : DeskController
{
    [HttpGet]
    public IActionResult Login(string? returnUrl) => View(new LoginInput { ReturnUrl = returnUrl });

    [HttpPost]
    public async Task<IActionResult> Login(LoginInput input)
    {
        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var member = await db.Members.FirstOrDefaultAsync(item => item.Email == input.Email);
        if (member is null || passwords.VerifyHashedPassword(member, member.PasswordHash, input.Password) == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(string.Empty, "That email and password do not match.");
            return View(input);
        }

        if (!member.IsConfirmed)
        {
            ModelState.AddModelError(string.Empty, "Confirm the email link before signing in.");
            return View(input);
        }

        member.LastLoginAt = DateTime.UtcNow;
        member.LastLoginIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        await db.SaveChangesAsync();
        await signIn.SignInAsync(member.Id, member.FullName, AppRoles.Member);
        return Go(input.ReturnUrl, "Index", "Portal");
    }

    [HttpGet]
    public IActionResult Register() => View(new RegisterInput());

    [HttpPost]
    public async Task<IActionResult> Register(RegisterInput input)
    {
        if (input.BirthDate > DateOnly.FromDateTime(DateTime.Today))
        {
            ModelState.AddModelError(nameof(input.BirthDate), "Birth date cannot be in the future.");
        }

        if (input.Gender is not ("Female" or "Male" or "Other"))
        {
            ModelState.AddModelError(nameof(input.Gender), "Choose a gender option.");
        }

        if (await db.Members.AnyAsync(member => member.Email == input.Email))
        {
            ModelState.AddModelError(nameof(input.Email), "An account with that email already exists.");
        }

        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var profile = await db.GymProfiles.OrderBy(item => item.Id).FirstAsync();
        var member = new Member
        {
            MemberCode = await MemberCodes.CreateAsync(db),
            FirstName = input.FirstName.Trim(),
            LastName = input.LastName.Trim(),
            Email = input.Email.Trim(),
            Gender = input.Gender,
            BirthDate = input.BirthDate,
            City = input.City.Trim(),
            Street = input.Street.Trim(),
            HouseNumber = input.HouseNumber.Trim(),
            RegisteredAt = DateTime.UtcNow,
            IsConfirmed = profile.AutoConfirmMembers,
            ConfirmationToken = profile.AutoConfirmMembers ? null : Guid.NewGuid().ToString("N")
        };
        member.PasswordHash = passwords.HashPassword(member, input.Password);
        if (input.Photo is { Length: > 0 })
        {
            try
            {
                member.PhotoPath = await uploads.SaveImageAsync(input.Photo, "members");
            }
            catch (InvalidOperationException exception)
            {
                ModelState.AddModelError(nameof(input.Photo), exception.Message);
                return View(input);
            }
        }

        db.Members.Add(member);
        await db.SaveChangesAsync();

        if (!member.IsConfirmed && member.ConfirmationToken is not null)
        {
            var link = Url.Action("Confirm", "Account", new { token = member.ConfirmationToken }, Request.Scheme);
            var sent = await mail.TrySendAsync(profile, member.Email, "Confirm your gym account", $"Open this link to confirm your account: {link}");
            Note(sent
                ? "Account created. Use the confirmation link sent to your email."
                : $"Account created. Confirmation link: {link}");
        }
        else
        {
            Note($"Welcome. Your member code is {member.MemberCode}. You can sign in now.");
        }

        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public async Task<IActionResult> Confirm(string token)
    {
        var member = await db.Members.FirstOrDefaultAsync(item => item.ConfirmationToken == token);
        if (member is null)
        {
            Warn("That confirmation link is not valid.");
            return RedirectToAction(nameof(Login));
        }

        member.IsConfirmed = true;
        member.ConfirmationToken = null;
        await db.SaveChangesAsync();
        Note("Your account is confirmed. Sign in with your email.");
        return RedirectToAction(nameof(Login));
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    public IActionResult Denied() => View();
}

public class StaffController(GymDbContext db, SignInService signIn, IPasswordHasher<StaffUser> passwords) : DeskController
{
    [HttpGet]
    public IActionResult Login(string? returnUrl) => View(new StaffLoginInput { ReturnUrl = returnUrl });

    [HttpPost]
    public async Task<IActionResult> Login(StaffLoginInput input)
    {
        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var staff = await db.StaffUsers.FirstOrDefaultAsync(item => item.UserName == input.UserName);
        if (staff is null || passwords.VerifyHashedPassword(staff, staff.PasswordHash, input.Password) == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(string.Empty, "That username and password do not match.");
            return View(input);
        }

        await signIn.SignInAsync(staff.Id, staff.FullName, staff.IsManager ? AppRoles.Manager : AppRoles.Staff);
        return Go(input.ReturnUrl, "Index", "Reception", new { area = "Admin" });
    }
}

[Authorize(Roles = AppRoles.Member)]
public class PortalController(GymDbContext db, IPasswordHasher<Member> passwords, SignInService signIn) : DeskController
{
    public async Task<IActionResult> Index()
    {
        var member = await CurrentMember();
        var today = DateOnly.FromDateTime(DateTime.Today);
        ViewBag.Passes = await db.MemberPasses.Where(pass => pass.MemberId == member.Id).OrderByDescending(pass => pass.ExpiresOn).ToListAsync();
        ViewBag.Active = await db.MemberPasses
            .Where(pass => pass.MemberId == member.Id && pass.ExpiresOn >= today && (pass.VisitsRemaining == null || pass.VisitsRemaining > 0))
            .OrderBy(pass => pass.ExpiresOn)
            .FirstOrDefaultAsync();
        ViewBag.RecentInvoices = await db.Invoices.Where(invoice => invoice.MemberId == member.Id).OrderByDescending(invoice => invoice.IssuedAt).Take(5).ToListAsync();
        ViewBag.WorkoutCount = await db.WorkoutRecords.CountAsync(workout => workout.MemberId == member.Id);
        return View(member);
    }

    public async Task<IActionResult> Profile() => View(ToProfile(await CurrentMember()));

    [HttpPost]
    public async Task<IActionResult> Profile(ProfileInput input)
    {
        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var member = await CurrentMember();
        member.FirstName = input.FirstName.Trim();
        member.LastName = input.LastName.Trim();
        member.City = input.City.Trim();
        member.Street = input.Street.Trim();
        member.HouseNumber = input.HouseNumber.Trim();
        await db.SaveChangesAsync();
        Note("Profile saved.");
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost]
    public async Task<IActionResult> Password(PasswordInput input)
    {
        var member = await CurrentMember();
        if (!ModelState.IsValid || passwords.VerifyHashedPassword(member, member.PasswordHash, input.CurrentPassword) == PasswordVerificationResult.Failed)
        {
            Warn("The current password does not match.");
            return RedirectToAction(nameof(Profile));
        }

        member.PasswordHash = passwords.HashPassword(member, input.NewPassword);
        await db.SaveChangesAsync();
        Note("Password updated.");
        return RedirectToAction(nameof(Profile));
    }

    public async Task<IActionResult> Invoices()
    {
        var member = await CurrentMember();
        var invoices = await db.Invoices.Where(invoice => invoice.MemberId == member.Id).OrderByDescending(invoice => invoice.IssuedAt).ToListAsync();
        return View(invoices);
    }

    public async Task<IActionResult> Workouts()
    {
        var member = await CurrentMember();
        var workouts = await db.WorkoutRecords.Where(workout => workout.MemberId == member.Id).OrderByDescending(workout => workout.WorkoutDate).ToListAsync();
        return View(workouts);
    }

    public async Task<IActionResult> Card() => View(await CurrentMember());

    [HttpPost]
    public async Task<IActionResult> Close()
    {
        var member = await CurrentMember();
        var lockers = await db.Lockers.Where(locker => locker.OccupiedByMemberId == member.Id).ToListAsync();
        foreach (var locker in lockers)
        {
            locker.OccupiedByMemberId = null;
        }

        db.Members.Remove(member);
        await db.SaveChangesAsync();
        await signIn.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    private async Task<Member> CurrentMember() =>
        await db.Members.FirstAsync(member => member.Id == User.GetUserId());

    private static ProfileInput ToProfile(Member member) => new()
    {
        FirstName = member.FirstName,
        LastName = member.LastName,
        City = member.City,
        Street = member.Street,
        HouseNumber = member.HouseNumber
    };
}
