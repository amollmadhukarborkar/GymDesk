using Microsoft.AspNetCore.Identity;

namespace GymDesk.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(
        GymDbContext db,
        IPasswordHasher<Member> memberHasher,
        IPasswordHasher<StaffUser> staffHasher)
    {
        if (!await db.GymProfiles.AnyAsync())
        {
            db.GymProfiles.Add(new GymProfile
            {
                BusinessName = "My Gym",
                Tagline = "Training, memberships, and the front desk in one place.",
                About = "A neighbourhood gym for strength, conditioning, and coached classes.",
                HouseRules = "Wipe down equipment when you finish.\nReturn weights to the racks.\nUse the locker assigned at the desk.\nAsk a coach before trying a new lift.",
                Country = "India",
                City = "Pune",
                Street = "Station Road",
                HouseNumber = "12",
                Phone = "+91 20 0000 0000",
                Email = "desk@example.com",
                Capacity = 80,
                CurrencyCode = "INR",
                AutoConfirmMembers = true
            });
        }

        if (!await db.OpeningHours.AnyAsync())
        {
            for (var day = 1; day <= 7; day++)
            {
                var weekend = day >= 6;
                db.OpeningHours.Add(new OpeningHour
                {
                    DayOfWeek = day,
                    IsClosed = day == 7,
                    OpensAt = day == 7 ? null : new TimeOnly(weekend ? 8 : 6, 0),
                    ClosesAt = day == 7 ? null : new TimeOnly(weekend ? 18 : 22, 0)
                });
            }
        }

        if (!await db.Lockers.AnyAsync())
        {
            for (var number = 1; number <= 24; number++)
            {
                db.Lockers.Add(new Locker
                {
                    LockerNumber = number,
                    Gender = number <= 12 ? "Female" : "Male"
                });
            }
        }

        if (!await db.MembershipPlans.AnyAsync())
        {
            db.MembershipPlans.AddRange(
                new MembershipPlan { Name = "Day visit", ValidDays = 1, VisitAllowance = 1, Price = 300, IsActive = true },
                new MembershipPlan { Name = "Ten visits", ValidDays = 60, VisitAllowance = 10, Price = 2500, IsActive = true },
                new MembershipPlan { Name = "Monthly", ValidDays = 30, VisitAllowance = null, Price = 1800, IsActive = true });
        }

        if (!await db.Trainers.AnyAsync())
        {
            db.Trainers.AddRange(
                new Trainer
                {
                    FullName = "Asha Kulkarni",
                    Biography = "Strength coach. Works with new lifters and return-to-training plans.",
                    HourlyRate = 900,
                    TenSessionRate = 7500
                },
                new Trainer
                {
                    FullName = "Rohan Mehta",
                    Biography = "Conditioning coach for classes and personal sessions.",
                    HourlyRate = 800,
                    TenSessionRate = 6800
                });
        }

        if (!await db.Products.AnyAsync())
        {
            db.Products.AddRange(
                new Product { Name = "Water bottle", Description = "750 ml bottle", Price = 250, StockQuantity = 40, Barcode = "890100000001", CreatedAt = DateTime.UtcNow },
                new Product { Name = "Towel", Description = "Gym towel", Price = 350, StockQuantity = 25, Barcode = "890100000002", CreatedAt = DateTime.UtcNow });
        }

        if (!await db.ClassSlots.AnyAsync())
        {
            db.ClassSlots.AddRange(
                new ClassSlot { Title = "Morning strength", DayName = "Monday", StartsAt = new TimeOnly(7, 0), EndsAt = new TimeOnly(8, 0), ColorHex = "#0f766e" },
                new ClassSlot { Title = "Conditioning", DayName = "Wednesday", StartsAt = new TimeOnly(18, 30), EndsAt = new TimeOnly(19, 15), ColorHex = "#b45309" },
                new ClassSlot { Title = "Open gym coaching", DayName = "Saturday", StartsAt = new TimeOnly(9, 0), EndsAt = new TimeOnly(10, 0), ColorHex = "#1d4ed8" });
        }

        if (!await db.StaffUsers.AnyAsync())
        {
            var manager = new StaffUser
            {
                FirstName = "Desk",
                LastName = "Manager",
                UserName = "admin",
                IsManager = true
            };
            manager.PasswordHash = staffHasher.HashPassword(manager, "Admin123!");
            db.StaffUsers.Add(manager);
        }

        if (!await db.Members.AnyAsync())
        {
            var member = new Member
            {
                MemberCode = "10000001",
                FirstName = "Sample",
                LastName = "Member",
                Email = "member@example.com",
                Gender = "Female",
                BirthDate = new DateOnly(1996, 4, 12),
                City = "Pune",
                Street = "Lake Road",
                HouseNumber = "4",
                RegisteredAt = DateTime.UtcNow,
                IsConfirmed = true,
                AccountBalance = 500
            };
            member.PasswordHash = memberHasher.HashPassword(member, "Member123!");
            db.Members.Add(member);
            await db.SaveChangesAsync();
            db.MemberPasses.Add(new MemberPass
            {
                MemberId = member.Id,
                PlanName = "Monthly",
                PurchasedOn = DateOnly.FromDateTime(DateTime.Today),
                ExpiresOn = DateOnly.FromDateTime(DateTime.Today.AddDays(30)),
                VisitsRemaining = null
            });
        }

        await db.SaveChangesAsync();
    }
}
