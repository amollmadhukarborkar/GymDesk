namespace GymDesk.Models;

public class RegisterInput
{
    [Required, MaxLength(80)]
    public string FirstName { get; set; } = "";

    [Required, MaxLength(80)]
    public string LastName { get; set; } = "";

    [Required, EmailAddress, MaxLength(200)]
    public string Email { get; set; } = "";

    [Required, MinLength(8), DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Required, Compare(nameof(Password)), DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = "";

    [Required]
    public string Gender { get; set; } = "Female";

    [Required]
    public DateOnly BirthDate { get; set; }

    [Required, MaxLength(80)]
    public string City { get; set; } = "";

    [Required, MaxLength(120)]
    public string Street { get; set; } = "";

    [Required, MaxLength(20)]
    public string HouseNumber { get; set; } = "";

    public IFormFile? Photo { get; set; }
}

public class LoginInput
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = "";

    public string? ReturnUrl { get; set; }
}

public class StaffLoginInput
{
    [Required, MaxLength(80)]
    public string UserName { get; set; } = "";

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = "";

    public string? ReturnUrl { get; set; }
}

public class ContactInput
{
    [Required, MaxLength(120)]
    public string SenderName { get; set; } = "";

    [Required, EmailAddress, MaxLength(200)]
    public string SenderEmail { get; set; } = "";

    [Required, MinLength(10)]
    public string Body { get; set; } = "";
}

public class ProfileInput
{
    [Required, MaxLength(80)]
    public string FirstName { get; set; } = "";

    [Required, MaxLength(80)]
    public string LastName { get; set; } = "";

    [Required, MaxLength(80)]
    public string City { get; set; } = "";

    [Required, MaxLength(120)]
    public string Street { get; set; } = "";

    [Required, MaxLength(20)]
    public string HouseNumber { get; set; } = "";
}

public class PasswordInput
{
    [Required, DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = "";

    [Required, MinLength(8), DataType(DataType.Password)]
    public string NewPassword { get; set; } = "";
}

public class MemberEditInput
{
    [Required, MaxLength(80)]
    public string FirstName { get; set; } = "";

    [Required, MaxLength(80)]
    public string LastName { get; set; } = "";

    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required]
    public string Gender { get; set; } = "Other";

    public bool IsConfirmed { get; set; }

    [MaxLength(80)]
    public string City { get; set; } = "";

    [MaxLength(120)]
    public string Street { get; set; } = "";

    [MaxLength(20)]
    public string HouseNumber { get; set; } = "";
}

public class PlanInput
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = "";

    [Range(1, 3650)]
    public int ValidDays { get; set; } = 30;

    public int? VisitAllowance { get; set; }

    [Range(0, 1000000)]
    public decimal Price { get; set; }

    public bool IsActive { get; set; } = true;
}

public class ProductInput
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = "";

    public string Description { get; set; } = "";

    [Range(0, 1000000)]
    public decimal Price { get; set; }

    [Range(0, 100000)]
    public int StockQuantity { get; set; }

    [Required, MaxLength(64)]
    public string Barcode { get; set; } = "";
}

public class TrainerInput
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string FullName { get; set; } = "";

    public string Biography { get; set; } = "";

    [Range(0, 1000000)]
    public decimal HourlyRate { get; set; }

    [Range(0, 1000000)]
    public decimal TenSessionRate { get; set; }

    public IFormFile? Photo { get; set; }
}

public class ClassSlotInput
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Title { get; set; } = "";

    [Required]
    public string DayName { get; set; } = "Monday";

    public TimeOnly StartsAt { get; set; }

    public TimeOnly EndsAt { get; set; }

    [Required, MaxLength(7)]
    public string ColorHex { get; set; } = "#0f766e";
}

public class LockerInput
{
    [Range(1, 9999)]
    public int LockerNumber { get; set; }

    [Required]
    public string Gender { get; set; } = "Female";
}

public class StaffInput
{
    [Required, MaxLength(80)]
    public string FirstName { get; set; } = "";

    [Required, MaxLength(80)]
    public string LastName { get; set; } = "";

    [Required, MaxLength(80)]
    public string UserName { get; set; } = "";

    [Required, MinLength(8), DataType(DataType.Password)]
    public string Password { get; set; } = "";

    public bool IsManager { get; set; }
}

public class PasswordResetInput
{
    [Required, MinLength(8)]
    public string Password { get; set; } = "";
}

public class HoursForm
{
    public List<HourRow> Days { get; set; } = [];
}

public class HourRow
{
    public int DayOfWeek { get; set; }

    public bool IsClosed { get; set; }

    public TimeOnly? OpensAt { get; set; }

    public TimeOnly? ClosesAt { get; set; }
}

public class ExceptionInput
{
    [Required]
    public DateOnly ExceptionDate { get; set; }

    public bool IsClosed { get; set; }

    public TimeOnly? OpensAt { get; set; }

    public TimeOnly? ClosesAt { get; set; }

    [MaxLength(200)]
    public string? Note { get; set; }
}

public class SettingsInput
{
    [Required, MaxLength(120)]
    public string BusinessName { get; set; } = "";

    [MaxLength(300)]
    public string Tagline { get; set; } = "";

    public string About { get; set; } = "";

    public string HouseRules { get; set; } = "";

    public string Country { get; set; } = "";

    public string City { get; set; } = "";

    public string Street { get; set; } = "";

    public string HouseNumber { get; set; } = "";

    public string Phone { get; set; } = "";

    [EmailAddress]
    public string Email { get; set; } = "";

    [Range(1, 100000)]
    public int Capacity { get; set; } = 80;

    [Required, MaxLength(8)]
    public string CurrencyCode { get; set; } = "INR";

    public bool AutoConfirmMembers { get; set; }

    public bool AcceptCash { get; set; } = true;

    public bool AcceptCard { get; set; } = true;

    public bool AcceptBalance { get; set; } = true;

    public string? CardTerminalNote { get; set; }

    public string? SmtpHost { get; set; }

    public int SmtpPort { get; set; } = 587;

    public bool SmtpUseTls { get; set; } = true;

    public string? SmtpUser { get; set; }

    [DataType(DataType.Password)]
    public string? SmtpPassword { get; set; }
}

public class SaleAddInput
{
    public int MemberId { get; set; }

    public int PlanId { get; set; }

    public int ProductId { get; set; }

    [Range(1, 100)]
    public int Quantity { get; set; } = 1;

    public decimal TopUpAmount { get; set; }
}

public class CheckoutInput
{
    [Range(1, int.MaxValue)]
    public int MemberId { get; set; }

    [Required]
    public string PaymentChannel { get; set; } = PaymentChannels.Cash;
}

public class ReportPage
{
    public int MemberCount { get; set; }

    public int InsideCount { get; set; }

    public int ActivePassCount { get; set; }

    public decimal CashToday { get; set; }

    public decimal CardToday { get; set; }

    public int AverageWorkoutMinutes { get; set; }

    public List<LabeledCount> Genders { get; set; } = [];

    public List<LabeledCount> MonthlyRevenue { get; set; } = [];

    public List<string> ExpiringSoon { get; set; } = [];
}

public class LabeledCount
{
    public string Label { get; set; } = "";

    public decimal Amount { get; set; }
}

public class SalePage
{
    public List<Member> Members { get; set; } = [];

    public List<MembershipPlan> Plans { get; set; } = [];

    public List<Product> Products { get; set; } = [];

    public List<CartLine> Cart { get; set; } = [];

    public GymProfile Profile { get; set; } = new();
}

public class ReceptionPage
{
    public List<CheckIn> Inside { get; set; } = [];
}

public class HomePage
{
    public GymProfile Profile { get; set; } = new();

    public List<OpeningHour> Hours { get; set; } = [];

    public OpeningException? TodayException { get; set; }

    public List<MembershipPlan> Plans { get; set; } = [];

    public int TrainerCount { get; set; }
}
