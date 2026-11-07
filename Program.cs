using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);


var provider = builder.Configuration["Database:Provider"] ?? "MySql";

var connectionName = provider.Trim().ToLowerInvariant() switch
{
    "mysql" => "MySql",
    "postgresql" => "PostgreSql",
    "sqlserver" => "SqlServer",
    _ => throw new InvalidOperationException(
        $"Unsupported database provider: {provider}")
};

var connectionString = builder.Configuration
    .GetConnectionString(connectionName)
    ?? throw new InvalidOperationException(
        $"Connection string '{connectionName}' is missing.");


builder.Services.AddDbContext<GymDbContext>(options =>
{
    if (provider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase))
    {
        options.UseNpgsql(connectionString);
    }
    else if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlServer(connectionString);
    }
    else
    {
        var serverSettings = new MySqlConnector.MySqlConnectionStringBuilder(connectionString)
        {
            Database = string.Empty
        };
        options.UseMySql(connectionString, ServerVersion.AutoDetect(serverSettings.ConnectionString));
    }
});

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromHours(8);
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IPasswordHasher<Member>, PasswordHasher<Member>>();
builder.Services.AddSingleton<IPasswordHasher<StaffUser>, PasswordHasher<StaffUser>>();
builder.Services.AddScoped<SignInService>();
builder.Services.AddScoped<ActivityLogService>();
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<SalesService>();
builder.Services.AddScoped<MailService>();
builder.Services.AddScoped<UploadStore>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "gymdesk.auth";
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Denied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            var path = context.Request.Path.Value ?? "";
            var login = path.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase)
                ? "/Staff/Login"
                : "/Account/Login";
            var returnUrl = Uri.EscapeDataString(path + context.Request.QueryString);
            context.Response.Redirect($"{login}?returnUrl={returnUrl}");
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<GymProfileFilter>();
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

var app = builder.Build();

try
{
    await DatabaseInitializer.InitializeAsync(app.Services, provider, connectionString);
}
catch (Exception exception)
{
    throw new InvalidOperationException(
        "Gym Desk could not reach the database. Start MySQL, PostgreSQL, or SQL Server, then check Database:Provider and the matching connection string.",
        exception);
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Reception}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
