namespace GymDesk.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = AppRoles.Desk)]
public class ReceptionController(GymDbContext db, ActivityLogService activity) : DeskController
{
    public async Task<IActionResult> Index() =>
        View(new ReceptionPage
        {
            Inside = await db.CheckIns.Include(checkIn => checkIn.Member).OrderBy(checkIn => checkIn.CheckedInAt).ToListAsync()
        });

    [HttpPost]
    public async Task<IActionResult> Lookup(string query) =>
        Json(await InspectAsync(query, commit: false));

    [HttpPost]
    public async Task<IActionResult> Commit(string query)
    {
        var result = await InspectAsync(query, commit: true);
        if (result.Allowed)
        {
            await activity.WriteAsync($"Checked in {result.Name} ({result.MemberCode}).", "success");
        }

        return Json(result);
    }

    [HttpPost]
    public async Task<IActionResult> CheckOut(int memberId)
    {
        var visit = await db.CheckIns.Include(checkIn => checkIn.Member).FirstOrDefaultAsync(checkIn => checkIn.MemberId == memberId);
        if (visit is null)
        {
            return Json(new { ok = false, message = "That member is not checked in." });
        }

        var minutes = Math.Max(1, (int)Math.Round((DateTime.UtcNow - visit.CheckedInAt).TotalMinutes));
        db.WorkoutRecords.Add(new WorkoutRecord
        {
            MemberId = visit.MemberId,
            DurationMinutes = minutes,
            WorkoutDate = DateOnly.FromDateTime(DateTime.Today)
        });
        var lockers = await db.Lockers.Where(locker => locker.OccupiedByMemberId == visit.MemberId).ToListAsync();
        foreach (var locker in lockers)
        {
            locker.OccupiedByMemberId = null;
        }

        var name = visit.Member?.FullName ?? "Member";
        db.CheckIns.Remove(visit);
        await db.SaveChangesAsync();
        await activity.WriteAsync($"Checked out {name} after {minutes} minutes.", "info");
        return Json(new { ok = true, message = $"{name} checked out. Visit length: {minutes} minutes." });
    }

    private async Task<ReceptionResult> InspectAsync(string? query, bool commit)
    {
        query = (query ?? "").Trim();
        if (query.Length < 2)
        {
            return new ReceptionResult { Message = "Enter a member code or name." };
        }

        Member? member;
        if (query.All(char.IsDigit))
        {
            member = await db.Members.FirstOrDefaultAsync(item => item.MemberCode == query);
        }
        else
        {
            var matches = await db.Members
                .Where(item => (item.FirstName + " " + item.LastName).Contains(query) || item.Email.Contains(query))
                .Take(2)
                .ToListAsync();
            if (matches.Count > 1)
            {
                return new ReceptionResult { Message = "More than one member matches. Use the member code." };
            }

            member = matches.FirstOrDefault();
        }

        if (member is null)
        {
            return new ReceptionResult { Message = "No member was found." };
        }

        var result = new ReceptionResult
        {
            Found = true,
            Name = member.FullName,
            MemberCode = member.MemberCode,
            Gender = member.Gender
        };
        var alreadyInside = await db.CheckIns.AnyAsync(checkIn => checkIn.MemberId == member.Id);
        result.AlreadyInside = alreadyInside;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var pass = await db.MemberPasses
            .Where(item => item.MemberId == member.Id && item.ExpiresOn >= today && (item.VisitsRemaining == null || item.VisitsRemaining > 0))
            .OrderBy(item => item.ExpiresOn)
            .FirstOrDefaultAsync();

        if (pass is null)
        {
            result.Message = alreadyInside ? "Already inside, and no active pass was found." : "No active pass was found.";
            return result;
        }

        result.PassName = pass.PlanName;
        result.ExpiresOn = pass.ExpiresOn.ToString("dd MMM yyyy");
        result.VisitsRemaining = pass.VisitsRemaining?.ToString() ?? "Unlimited";
        if (alreadyInside)
        {
            result.Message = "This member is already checked in.";
            return result;
        }

        result.Allowed = true;
        result.Message = pass.ExpiresOn == today
            ? "Pass is valid and expires today."
            : "Pass is valid.";

        if (!commit)
        {
            return result;
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
        var free = db.Lockers.Where(locker => locker.OccupiedByMemberId == null);
        if (member.Gender is "Female" or "Male")
        {
            free = free.Where(locker => locker.Gender == member.Gender);
        }

        var available = await free.ToListAsync();
        Locker? locker = available.Count == 0 ? null : available[Random.Shared.Next(available.Count)];
        if (locker is not null)
        {
            locker.OccupiedByMemberId = member.Id;
            result.LockerNumber = locker.LockerNumber;
        }

        if (pass.VisitsRemaining is not null)
        {
            pass.VisitsRemaining -= 1;
            result.VisitsRemaining = pass.VisitsRemaining.ToString();
        }

        db.CheckIns.Add(new CheckIn
        {
            MemberId = member.Id,
            CheckedInAt = DateTime.UtcNow,
            LockerNumber = locker?.LockerNumber
        });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        result.Message = locker is null
            ? "Checked in. No free locker was available."
            : $"Checked in. Locker {locker.LockerNumber}.";
        return result;
    }
}

[Area("Admin")]
[Authorize(Roles = AppRoles.Desk)]
public class MembersController(GymDbContext db, ActivityLogService activity) : DeskController
{
    public async Task<IActionResult> Index(string? q)
    {
        var query = db.Members.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(member =>
                member.FirstName.Contains(q) ||
                member.LastName.Contains(q) ||
                member.Email.Contains(q) ||
                member.MemberCode.Contains(q));
        }

        ViewBag.Query = q ?? "";
        return View(await query.OrderBy(member => member.LastName).Take(200).ToListAsync());
    }

    public async Task<IActionResult> Edit(int id)
    {
        var member = await db.Members.FirstOrDefaultAsync(item => item.Id == id);
        if (member is null)
        {
            return NotFound();
        }

        ViewBag.MemberCode = member.MemberCode;
        ViewBag.Balance = member.AccountBalance;
        return View(new MemberEditInput
        {
            FirstName = member.FirstName,
            LastName = member.LastName,
            Email = member.Email,
            Gender = member.Gender,
            IsConfirmed = member.IsConfirmed,
            City = member.City,
            Street = member.Street,
            HouseNumber = member.HouseNumber
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, MemberEditInput input)
    {
        var member = await db.Members.FirstOrDefaultAsync(item => item.Id == id);
        if (member is null)
        {
            return NotFound();
        }

        if (await db.Members.AnyAsync(item => item.Email == input.Email && item.Id != id))
        {
            ModelState.AddModelError(nameof(input.Email), "Another member already uses that email.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.MemberCode = member.MemberCode;
            ViewBag.Balance = member.AccountBalance;
            return View(input);
        }

        member.FirstName = input.FirstName.Trim();
        member.LastName = input.LastName.Trim();
        member.Email = input.Email.Trim();
        member.Gender = input.Gender;
        member.IsConfirmed = input.IsConfirmed;
        member.City = input.City.Trim();
        member.Street = input.Street.Trim();
        member.HouseNumber = input.HouseNumber.Trim();
        await db.SaveChangesAsync();
        await activity.WriteAsync($"Updated member {member.MemberCode}.", "info");
        Note("Member saved.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var member = await db.Members.FirstOrDefaultAsync(item => item.Id == id);
        if (member is null)
        {
            return NotFound();
        }

        var code = member.MemberCode;
        db.Members.Remove(member);
        await db.SaveChangesAsync();
        await activity.WriteAsync($"Removed member {code}.", "warning");
        Note("Member removed.");
        return RedirectToAction(nameof(Index));
    }
}

[Area("Admin")]
[Authorize(Roles = AppRoles.Desk)]
public class SalesController(GymDbContext db, CartService cart, SalesService sales, ActivityLogService activity) : DeskController
{
    public async Task<IActionResult> Index() => View(await Page());

    [HttpPost]
    public async Task<IActionResult> AddPlan(int planId)
    {
        var plan = await db.MembershipPlans.FirstOrDefaultAsync(item => item.Id == planId && item.IsActive);
        if (plan is null)
        {
            Warn("That plan is not available.");
            return RedirectToAction(nameof(Index));
        }

        cart.Add(new CartLine { Kind = "plan", ReferenceId = plan.Id, Name = plan.Name, Quantity = 1, UnitPrice = plan.Price });
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> AddProduct(int productId, int quantity)
    {
        var product = await db.Products.FirstOrDefaultAsync(item => item.Id == productId);
        if (product is null)
        {
            Warn("That product was not found.");
            return RedirectToAction(nameof(Index));
        }

        cart.Add(new CartLine
        {
            Kind = "product",
            ReferenceId = product.Id,
            Name = product.Name,
            Quantity = Math.Clamp(quantity, 1, 100),
            UnitPrice = product.Price
        });
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult AddCredit(decimal amount)
    {
        if (amount <= 0)
        {
            Warn("Enter a credit amount greater than zero.");
            return RedirectToAction(nameof(Index));
        }

        cart.Add(new CartLine { Kind = "topup", Name = "Account credit", Quantity = 1, UnitPrice = amount });
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult Remove(int index)
    {
        cart.RemoveAt(index);
        return Json(new { ok = true });
    }

    [HttpPost]
    public async Task<IActionResult> Checkout(CheckoutInput input)
    {
        var result = await sales.CheckoutAsync(input.MemberId, input.PaymentChannel);
        if (!result.Ok)
        {
            Warn(result.Error);
            return RedirectToAction(nameof(Index));
        }

        var member = await db.Members.FirstAsync(item => item.Id == input.MemberId);
        await activity.WriteAsync($"Sold to {member.FullName} by {input.PaymentChannel}.", "success");
        Note($"Sale completed for {member.FullName}.");
        return RedirectToAction(nameof(Index));
    }

    private async Task<SalePage> Page() => new()
    {
        Members = await db.Members.Where(member => member.IsConfirmed).OrderBy(member => member.LastName).ToListAsync(),
        Plans = await db.MembershipPlans.Where(plan => plan.IsActive).OrderBy(plan => plan.Name).ToListAsync(),
        Products = await db.Products.OrderBy(product => product.Name).ToListAsync(),
        Cart = cart.Get(),
        Profile = await db.GymProfiles.OrderBy(item => item.Id).FirstAsync()
    };
}

[Area("Admin")]
[Authorize(Roles = AppRoles.Desk)]
public class InvoicesController(GymDbContext db) : DeskController
{
    public async Task<IActionResult> Index() =>
        View(await db.Invoices.Include(invoice => invoice.Member).OrderByDescending(invoice => invoice.IssuedAt).Take(300).ToListAsync());
}
