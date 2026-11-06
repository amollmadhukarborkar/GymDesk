namespace GymDesk.Models;

public static class AppRoles
{
    public const string Member = "Member";
    public const string Staff = "Staff";
    public const string Manager = "Manager";
    public const string Desk = "Staff,Manager";
}

public static class PaymentChannels
{
    public const string Cash = "Cash";
    public const string Card = "Card";
    public const string Balance = "Balance";
}

public static class WeekDays
{
    public static readonly string[] Names = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];

    public static string Name(int day) => day is >= 1 and <= 7 ? Names[day - 1] : "Day";
}

public static class Money
{
    public static string Format(decimal amount, string? currency) => $"{amount:0.00} {currency}";
}

public class GymProfile
{
    public int Id { get; set; }

    [MaxLength(120)]
    public string BusinessName { get; set; } = "My Gym";

    [MaxLength(300)]
    public string Tagline { get; set; } = "";

    public string About { get; set; } = "";

    public string HouseRules { get; set; } = "";

    [MaxLength(80)]
    public string Country { get; set; } = "";

    [MaxLength(80)]
    public string City { get; set; } = "";

    [MaxLength(120)]
    public string Street { get; set; } = "";

    [MaxLength(20)]
    public string HouseNumber { get; set; } = "";

    [MaxLength(40)]
    public string Phone { get; set; } = "";

    [MaxLength(200)]
    public string Email { get; set; } = "";

    public int Capacity { get; set; } = 80;

    [MaxLength(8)]
    public string CurrencyCode { get; set; } = "INR";

    public bool AutoConfirmMembers { get; set; } = true;

    public bool AcceptCash { get; set; } = true;

    public bool AcceptCard { get; set; } = true;

    public bool AcceptBalance { get; set; } = true;

    [MaxLength(300)]
    public string? CardTerminalNote { get; set; }

    [MaxLength(200)]
    public string? SmtpHost { get; set; }

    public int SmtpPort { get; set; } = 587;

    public bool SmtpUseTls { get; set; } = true;

    [MaxLength(200)]
    public string? SmtpUser { get; set; }

    [MaxLength(200)]
    public string? SmtpPassword { get; set; }
}

public class Member
{
    public int Id { get; set; }

    [MaxLength(12)]
    public string MemberCode { get; set; } = "";

    [MaxLength(80)]
    public string FirstName { get; set; } = "";

    [MaxLength(80)]
    public string LastName { get; set; } = "";

    [MaxLength(200)]
    public string Email { get; set; } = "";

    [MaxLength(500)]
    public string PasswordHash { get; set; } = "";

    [MaxLength(20)]
    public string Gender { get; set; } = "Other";

    public DateOnly BirthDate { get; set; }

    [MaxLength(80)]
    public string City { get; set; } = "";

    [MaxLength(120)]
    public string Street { get; set; } = "";

    [MaxLength(20)]
    public string HouseNumber { get; set; } = "";

    [MaxLength(300)]
    public string? PhotoPath { get; set; }

    public DateTime RegisteredAt { get; set; }

    public bool IsConfirmed { get; set; }

    [MaxLength(80)]
    public string? ConfirmationToken { get; set; }

    public DateTime? LastLoginAt { get; set; }

    [MaxLength(64)]
    public string? LastLoginIp { get; set; }

    [Precision(10, 2)]
    public decimal AccountBalance { get; set; }

    public string FullName => $"{FirstName} {LastName}";
}

public class StaffUser
{
    public int Id { get; set; }

    [MaxLength(80)]
    public string FirstName { get; set; } = "";

    [MaxLength(80)]
    public string LastName { get; set; } = "";

    [MaxLength(80)]
    public string UserName { get; set; } = "";

    [MaxLength(500)]
    public string PasswordHash { get; set; } = "";

    public bool IsManager { get; set; }

    public string FullName => $"{FirstName} {LastName}";
}

public class MembershipPlan
{
    public int Id { get; set; }

    [MaxLength(120)]
    public string Name { get; set; } = "";

    public int ValidDays { get; set; }

    public int? VisitAllowance { get; set; }

    [Precision(10, 2)]
    public decimal Price { get; set; }

    public bool IsActive { get; set; } = true;
}

public class MemberPass
{
    public int Id { get; set; }

    public int MemberId { get; set; }

    public Member? Member { get; set; }

    [MaxLength(120)]
    public string PlanName { get; set; } = "";

    public DateOnly PurchasedOn { get; set; }

    public DateOnly ExpiresOn { get; set; }

    public int? VisitsRemaining { get; set; }
}

public class Invoice
{
    public int Id { get; set; }

    public int MemberId { get; set; }

    public Member? Member { get; set; }

    [MaxLength(255)]
    public string Description { get; set; } = "";

    [Precision(10, 2)]
    public decimal Amount { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "Paid";

    [MaxLength(20)]
    public string PaymentChannel { get; set; } = PaymentChannels.Cash;

    public DateTime IssuedAt { get; set; }
}

public class Locker
{
    public int Id { get; set; }

    public int LockerNumber { get; set; }

    [MaxLength(20)]
    public string Gender { get; set; } = "Male";

    public int? OccupiedByMemberId { get; set; }

    public Member? OccupiedByMember { get; set; }
}

public class ActivityLog
{
    public int Id { get; set; }

    public int? StaffUserId { get; set; }

    public StaffUser? StaffUser { get; set; }

    [MaxLength(500)]
    public string Message { get; set; } = "";

    [MaxLength(20)]
    public string Severity { get; set; } = "info";

    public DateTime OccurredAt { get; set; }
}

public class OpeningHour
{
    public int Id { get; set; }

    public int DayOfWeek { get; set; }

    public bool IsClosed { get; set; }

    public TimeOnly? OpensAt { get; set; }

    public TimeOnly? ClosesAt { get; set; }
}

public class OpeningException
{
    public int Id { get; set; }

    public DateOnly ExceptionDate { get; set; }

    public bool IsClosed { get; set; }

    public TimeOnly? OpensAt { get; set; }

    public TimeOnly? ClosesAt { get; set; }

    [MaxLength(200)]
    public string? Note { get; set; }
}

public class Product
{
    public int Id { get; set; }

    [MaxLength(120)]
    public string Name { get; set; } = "";

    public string Description { get; set; } = "";

    [Precision(10, 2)]
    public decimal Price { get; set; }

    public int StockQuantity { get; set; }

    [MaxLength(64)]
    public string Barcode { get; set; } = "";

    public DateTime CreatedAt { get; set; }
}

public class RevenueEntry
{
    public int Id { get; set; }

    public DateOnly BusinessDate { get; set; }

    [Precision(10, 2)]
    public decimal CashTotal { get; set; }

    [Precision(10, 2)]
    public decimal CardTotal { get; set; }
}

public class CheckIn
{
    public int Id { get; set; }

    public int MemberId { get; set; }

    public Member? Member { get; set; }

    public DateTime CheckedInAt { get; set; }

    public int? LockerNumber { get; set; }
}

public class ClassSlot
{
    public int Id { get; set; }

    [MaxLength(120)]
    public string Title { get; set; } = "";

    [MaxLength(12)]
    public string DayName { get; set; } = "Monday";

    public TimeOnly StartsAt { get; set; }

    public TimeOnly EndsAt { get; set; }

    [MaxLength(7)]
    public string ColorHex { get; set; } = "#0f766e";
}

public class Trainer
{
    public int Id { get; set; }

    [MaxLength(120)]
    public string FullName { get; set; } = "";

    public string Biography { get; set; } = "";

    [Precision(10, 2)]
    public decimal HourlyRate { get; set; }

    [Precision(10, 2)]
    public decimal TenSessionRate { get; set; }

    [MaxLength(300)]
    public string? PhotoPath { get; set; }
}

public class WorkoutRecord
{
    public int Id { get; set; }

    public int MemberId { get; set; }

    public Member? Member { get; set; }

    public int DurationMinutes { get; set; }

    public DateOnly WorkoutDate { get; set; }
}

public class ContactMessage
{
    public int Id { get; set; }

    [MaxLength(120)]
    public string SenderName { get; set; } = "";

    [MaxLength(200)]
    public string SenderEmail { get; set; } = "";

    public string Body { get; set; } = "";

    public DateTime ReceivedAt { get; set; }
}

public class CartLine
{
    public string Kind { get; set; } = "";

    public int ReferenceId { get; set; }

    public string Name { get; set; } = "";

    public int Quantity { get; set; } = 1;

    public decimal UnitPrice { get; set; }

    public decimal LineTotal => UnitPrice * Quantity;
}

public class ReceptionResult
{
    public bool Found { get; set; }

    public bool Allowed { get; set; }

    public string Message { get; set; } = "";

    public string Name { get; set; } = "";

    public string MemberCode { get; set; } = "";

    public string Gender { get; set; } = "";

    public string? PassName { get; set; }

    public string? ExpiresOn { get; set; }

    public string? VisitsRemaining { get; set; }

    public bool AlreadyInside { get; set; }

    public int? LockerNumber { get; set; }
}
