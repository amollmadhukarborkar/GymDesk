namespace GymDesk.Data;

public class GymDbContext(DbContextOptions<GymDbContext> options) : DbContext(options)
{
    public DbSet<GymProfile> GymProfiles => Set<GymProfile>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<StaffUser> StaffUsers => Set<StaffUser>();
    public DbSet<MembershipPlan> MembershipPlans => Set<MembershipPlan>();
    public DbSet<MemberPass> MemberPasses => Set<MemberPass>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Locker> Lockers => Set<Locker>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<OpeningHour> OpeningHours => Set<OpeningHour>();
    public DbSet<OpeningException> OpeningExceptions => Set<OpeningException>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<RevenueEntry> RevenueEntries => Set<RevenueEntry>();
    public DbSet<CheckIn> CheckIns => Set<CheckIn>();
    public DbSet<ClassSlot> ClassSlots => Set<ClassSlot>();
    public DbSet<Trainer> Trainers => Set<Trainer>();
    public DbSet<WorkoutRecord> WorkoutRecords => Set<WorkoutRecord>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Member>().HasIndex(member => member.Email).IsUnique();
        modelBuilder.Entity<Member>().HasIndex(member => member.MemberCode).IsUnique();
        modelBuilder.Entity<StaffUser>().HasIndex(staff => staff.UserName).IsUnique();
        modelBuilder.Entity<Product>().HasIndex(product => product.Barcode).IsUnique();
        modelBuilder.Entity<OpeningHour>().HasIndex(hour => hour.DayOfWeek).IsUnique();
        modelBuilder.Entity<OpeningException>().HasIndex(item => item.ExceptionDate).IsUnique();
        modelBuilder.Entity<RevenueEntry>().HasIndex(entry => entry.BusinessDate).IsUnique();
        modelBuilder.Entity<CheckIn>().HasIndex(checkIn => checkIn.MemberId).IsUnique();
        modelBuilder.Entity<Locker>().HasIndex(locker => locker.LockerNumber).IsUnique();

        modelBuilder.Entity<MemberPass>()
            .HasOne(pass => pass.Member)
            .WithMany()
            .HasForeignKey(pass => pass.MemberId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Invoice>()
            .HasOne(invoice => invoice.Member)
            .WithMany()
            .HasForeignKey(invoice => invoice.MemberId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<WorkoutRecord>()
            .HasOne(workout => workout.Member)
            .WithMany()
            .HasForeignKey(workout => workout.MemberId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CheckIn>()
            .HasOne(checkIn => checkIn.Member)
            .WithMany()
            .HasForeignKey(checkIn => checkIn.MemberId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Locker>()
            .HasOne(locker => locker.OccupiedByMember)
            .WithMany()
            .HasForeignKey(locker => locker.OccupiedByMemberId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ActivityLog>()
            .HasOne(log => log.StaffUser)
            .WithMany()
            .HasForeignKey(log => log.StaffUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
