using Microsoft.AspNetCore.Identity;

namespace GymDesk.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = AppRoles.Desk)]
public class ReportsController(GymDbContext db, MailService mail, ActivityLogService activity) : DeskController
{
    public async Task<IActionResult> Index()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var revenue = await db.RevenueEntries.FirstOrDefaultAsync(entry => entry.BusinessDate == today);
        var genderRows = await db.Members
            .GroupBy(member => member.Gender)
            .Select(group => new { Label = group.Key, Count = group.Count() })
            .ToListAsync();
        var genders = genderRows.Select(row => new LabeledCount { Label = row.Label, Amount = row.Count }).ToList();
        var entries = await db.RevenueEntries
            .Where(entry => entry.BusinessDate >= today.AddDays(-180))
            .ToListAsync();
        var months = entries
            .GroupBy(entry => entry.BusinessDate.ToString("yyyy-MM"))
            .OrderBy(group => group.Key)
            .Select(group => new LabeledCount
            {
                Label = group.Key,
                Amount = group.Sum(entry => entry.CashTotal + entry.CardTotal)
            })
            .ToList();
        var soon = today.AddDays(7);
        var expiring = await db.MemberPasses
            .Include(pass => pass.Member)
            .Where(pass => pass.ExpiresOn >= today && pass.ExpiresOn <= soon)
            .OrderBy(pass => pass.ExpiresOn)
            .ToListAsync();
        var durations = await db.WorkoutRecords.Select(workout => workout.DurationMinutes).ToListAsync();

        return View(new ReportPage
        {
            MemberCount = await db.Members.CountAsync(),
            InsideCount = await db.CheckIns.CountAsync(),
            ActivePassCount = await db.MemberPasses.CountAsync(pass =>
                pass.ExpiresOn >= today && (pass.VisitsRemaining == null || pass.VisitsRemaining > 0)),
            CashToday = revenue?.CashTotal ?? 0,
            CardToday = revenue?.CardTotal ?? 0,
            AverageWorkoutMinutes = durations.Count == 0 ? 0 : (int)Math.Round(durations.Average()),
            Genders = genders,
            MonthlyRevenue = months,
            ExpiringSoon = expiring.Select(pass => $"{pass.Member?.FullName} · {pass.PlanName} · {pass.ExpiresOn:dd MMM}").ToList()
        });
    }

    [HttpPost]
    public async Task<IActionResult> Remind()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var soon = today.AddDays(7);
        var passes = await db.MemberPasses
            .Include(pass => pass.Member)
            .Where(pass => pass.ExpiresOn >= today && pass.ExpiresOn <= soon && pass.Member != null)
            .ToListAsync();
        var profile = await db.GymProfiles.OrderBy(item => item.Id).FirstAsync();
        var sent = 0;
        foreach (var pass in passes.GroupBy(item => item.Member!.Email))
        {
            var member = pass.First().Member!;
            var lines = string.Join("\n", pass.Select(item => $"{item.PlanName} expires on {item.ExpiresOn:dd MMM yyyy}."));
            var body = $"Hello {member.FirstName},\n\nA pass at {profile.BusinessName} is close to its end date.\n\n{lines}\n\nThe front desk can renew it on your next visit.";
            if (await mail.TrySendAsync(profile, member.Email, $"{profile.BusinessName} pass reminder", body))
            {
                sent++;
            }
        }

        await activity.WriteAsync($"Renewal reminders sent to {sent} members. {passes.Count} passes expire within 7 days.", sent > 0 ? "success" : "warning");
        Note(sent > 0
            ? $"Sent {sent} reminder emails."
            : "No email was sent. Add SMTP details in settings, or use the expiring list on this page.");
        return RedirectToAction(nameof(Index));
    }
}

[Area("Admin")]
[Authorize(Roles = AppRoles.Desk)]
public class LockersController(GymDbContext db, ActivityLogService activity) : DeskController
{
    public async Task<IActionResult> Index() =>
        View(await db.Lockers.Include(locker => locker.OccupiedByMember).OrderBy(locker => locker.LockerNumber).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Create(LockerInput input)
    {
        if (!ModelState.IsValid || input.Gender is not ("Female" or "Male"))
        {
            Warn("Enter a locker number and a changing room.");
            return RedirectToAction(nameof(Index));
        }

        if (await db.Lockers.AnyAsync(locker => locker.LockerNumber == input.LockerNumber))
        {
            Warn("That locker number already exists.");
            return RedirectToAction(nameof(Index));
        }

        db.Lockers.Add(new Locker { LockerNumber = input.LockerNumber, Gender = input.Gender });
        await db.SaveChangesAsync();
        await activity.WriteAsync($"Added locker {input.LockerNumber}.", "info");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Release(int id)
    {
        var locker = await db.Lockers.FirstOrDefaultAsync(item => item.Id == id);
        if (locker is not null)
        {
            locker.OccupiedByMemberId = null;
            await db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var locker = await db.Lockers.FirstOrDefaultAsync(item => item.Id == id);
        if (locker is not null && locker.OccupiedByMemberId is null)
        {
            db.Lockers.Remove(locker);
            await db.SaveChangesAsync();
        }
        else
        {
            Warn("Release the locker before removing it.");
        }

        return RedirectToAction(nameof(Index));
    }
}

[Area("Admin")]
[Authorize(Roles = AppRoles.Desk)]
public class HoursController(GymDbContext db, ActivityLogService activity) : DeskController
{
    public async Task<IActionResult> Index()
    {
        var hours = await db.OpeningHours.OrderBy(hour => hour.DayOfWeek).ToListAsync();
        var form = new HoursForm
        {
            Days = Enumerable.Range(1, 7).Select(day =>
            {
                var hour = hours.FirstOrDefault(item => item.DayOfWeek == day);
                return new HourRow
                {
                    DayOfWeek = day,
                    IsClosed = hour?.IsClosed ?? false,
                    OpensAt = hour?.OpensAt,
                    ClosesAt = hour?.ClosesAt
                };
            }).ToList()
        };
        ViewBag.Exceptions = await db.OpeningExceptions.OrderBy(item => item.ExceptionDate).ToListAsync();
        return View(form);
    }

    [HttpPost]
    public async Task<IActionResult> Save(HoursForm input)
    {
        if (!ModelState.IsValid)
        {
            Warn("Check the opening and closing times.");
            return RedirectToAction(nameof(Index));
        }

        foreach (var day in input.Days)
        {
            if (!day.IsClosed && (day.OpensAt is null || day.ClosesAt is null || day.ClosesAt <= day.OpensAt))
            {
                Warn($"{WeekDays.Name(day.DayOfWeek)} needs an opening time before the closing time.");
                return RedirectToAction(nameof(Index));
            }

            var hour = await db.OpeningHours.FirstOrDefaultAsync(item => item.DayOfWeek == day.DayOfWeek);
            if (hour is null)
            {
                hour = new OpeningHour { DayOfWeek = day.DayOfWeek };
                db.OpeningHours.Add(hour);
            }

            hour.IsClosed = day.IsClosed;
            hour.OpensAt = day.IsClosed ? null : day.OpensAt;
            hour.ClosesAt = day.IsClosed ? null : day.ClosesAt;
        }

        await db.SaveChangesAsync();
        await activity.WriteAsync("Updated opening hours.", "info");
        Note("Opening hours saved.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> AddException(ExceptionInput input)
    {
        if (!ModelState.IsValid)
        {
            Warn("Choose a date for the exception.");
            return RedirectToAction(nameof(Index));
        }

        var existing = await db.OpeningExceptions.FirstOrDefaultAsync(item => item.ExceptionDate == input.ExceptionDate);
        if (existing is null)
        {
            existing = new OpeningException { ExceptionDate = input.ExceptionDate };
            db.OpeningExceptions.Add(existing);
        }

        existing.IsClosed = input.IsClosed;
        existing.OpensAt = input.IsClosed ? null : input.OpensAt;
        existing.ClosesAt = input.IsClosed ? null : input.ClosesAt;
        existing.Note = input.Note;
        await db.SaveChangesAsync();
        Note("Date exception saved.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteException(int id)
    {
        var item = await db.OpeningExceptions.FirstOrDefaultAsync(exception => exception.Id == id);
        if (item is not null)
        {
            db.OpeningExceptions.Remove(item);
            await db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }
}

[Area("Admin")]
[Authorize(Roles = AppRoles.Desk)]
public class MessagesController(GymDbContext db) : DeskController
{
    public async Task<IActionResult> Index() =>
        View(await db.ContactMessages.OrderByDescending(message => message.ReceivedAt).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var message = await db.ContactMessages.FirstOrDefaultAsync(item => item.Id == id);
        if (message is not null)
        {
            db.ContactMessages.Remove(message);
            await db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }
}

[Area("Admin")]
[Authorize(Roles = AppRoles.Desk)]
public class ActivityController(GymDbContext db) : DeskController
{
    public async Task<IActionResult> Index() =>
        View(await db.ActivityLogs.Include(log => log.StaffUser).OrderByDescending(log => log.OccurredAt).Take(300).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> ClearOld()
    {
        var cutoff = DateTime.UtcNow.AddDays(-90);
        var old = await db.ActivityLogs.Where(log => log.OccurredAt < cutoff).ToListAsync();
        db.ActivityLogs.RemoveRange(old);
        await db.SaveChangesAsync();
        Note($"Removed {old.Count} log entries older than 90 days.");
        return RedirectToAction(nameof(Index));
    }
}

[Area("Admin")]
[Authorize(Roles = AppRoles.Manager)]
public class StaffUsersController(GymDbContext db, IPasswordHasher<StaffUser> passwords, ActivityLogService activity) : DeskController
{
    public async Task<IActionResult> Index() =>
        View(await db.StaffUsers.OrderBy(staff => staff.UserName).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Create(StaffInput input)
    {
        if (!ModelState.IsValid)
        {
            Warn("Check the staff fields. Passwords need at least 8 characters.");
            return RedirectToAction(nameof(Index));
        }

        if (await db.StaffUsers.AnyAsync(staff => staff.UserName == input.UserName))
        {
            Warn("That username is already in use.");
            return RedirectToAction(nameof(Index));
        }

        var staff = new StaffUser
        {
            FirstName = input.FirstName.Trim(),
            LastName = input.LastName.Trim(),
            UserName = input.UserName.Trim(),
            IsManager = input.IsManager
        };
        staff.PasswordHash = passwords.HashPassword(staff, input.Password);
        db.StaffUsers.Add(staff);
        await db.SaveChangesAsync();
        await activity.WriteAsync($"Added staff account {staff.UserName}.", "success");
        Note("Staff account created.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Reset(int id, PasswordResetInput input)
    {
        var staff = await db.StaffUsers.FirstOrDefaultAsync(item => item.Id == id);
        if (staff is null || !ModelState.IsValid)
        {
            Warn("Enter a new password of at least 8 characters.");
            return RedirectToAction(nameof(Index));
        }

        staff.PasswordHash = passwords.HashPassword(staff, input.Password);
        await db.SaveChangesAsync();
        Note($"Password reset for {staff.UserName}.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var staff = await db.StaffUsers.FirstOrDefaultAsync(item => item.Id == id);
        if (staff is null)
        {
            return RedirectToAction(nameof(Index));
        }

        if (staff.Id == User.GetUserId())
        {
            Warn("You cannot remove the account you are using.");
            return RedirectToAction(nameof(Index));
        }

        if (staff.IsManager && await db.StaffUsers.CountAsync(item => item.IsManager) <= 1)
        {
            Warn("Keep at least one manager account.");
            return RedirectToAction(nameof(Index));
        }

        db.StaffUsers.Remove(staff);
        await db.SaveChangesAsync();
        await activity.WriteAsync($"Removed staff account {staff.UserName}.", "warning");
        return RedirectToAction(nameof(Index));
    }
}

[Area("Admin")]
[Authorize(Roles = AppRoles.Manager)]
public class SettingsController(GymDbContext db, ActivityLogService activity) : DeskController
{
    public async Task<IActionResult> Index()
    {
        var profile = await db.GymProfiles.OrderBy(item => item.Id).FirstAsync();
        return View(new SettingsInput
        {
            BusinessName = profile.BusinessName,
            Tagline = profile.Tagline,
            About = profile.About,
            HouseRules = profile.HouseRules,
            Country = profile.Country,
            City = profile.City,
            Street = profile.Street,
            HouseNumber = profile.HouseNumber,
            Phone = profile.Phone,
            Email = profile.Email,
            Capacity = profile.Capacity,
            CurrencyCode = profile.CurrencyCode,
            AutoConfirmMembers = profile.AutoConfirmMembers,
            AcceptCash = profile.AcceptCash,
            AcceptCard = profile.AcceptCard,
            AcceptBalance = profile.AcceptBalance,
            CardTerminalNote = profile.CardTerminalNote,
            SmtpHost = profile.SmtpHost,
            SmtpPort = profile.SmtpPort,
            SmtpUseTls = profile.SmtpUseTls,
            SmtpUser = profile.SmtpUser
        });
    }

    [HttpPost]
    public async Task<IActionResult> Index(SettingsInput input)
    {
        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var profile = await db.GymProfiles.OrderBy(item => item.Id).FirstAsync();
        profile.BusinessName = input.BusinessName.Trim();
        profile.Tagline = input.Tagline.Trim();
        profile.About = input.About.Trim();
        profile.HouseRules = input.HouseRules.Trim();
        profile.Country = input.Country.Trim();
        profile.City = input.City.Trim();
        profile.Street = input.Street.Trim();
        profile.HouseNumber = input.HouseNumber.Trim();
        profile.Phone = input.Phone.Trim();
        profile.Email = input.Email.Trim();
        profile.Capacity = input.Capacity;
        profile.CurrencyCode = input.CurrencyCode.Trim().ToUpperInvariant();
        profile.AutoConfirmMembers = input.AutoConfirmMembers;
        profile.AcceptCash = input.AcceptCash;
        profile.AcceptCard = input.AcceptCard;
        profile.AcceptBalance = input.AcceptBalance;
        profile.CardTerminalNote = input.CardTerminalNote;
        profile.SmtpHost = input.SmtpHost;
        profile.SmtpPort = input.SmtpPort;
        profile.SmtpUseTls = input.SmtpUseTls;
        profile.SmtpUser = input.SmtpUser;
        if (!string.IsNullOrWhiteSpace(input.SmtpPassword))
        {
            profile.SmtpPassword = input.SmtpPassword;
        }

        await db.SaveChangesAsync();
        await activity.WriteAsync("Updated gym settings.", "info");
        Note("Settings saved.");
        return RedirectToAction(nameof(Index));
    }
}
