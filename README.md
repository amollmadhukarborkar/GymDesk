# Gym Desk

ASP.NET Core MVC app for a gym front desk. Members, passes, check-in, shop sales, trainers, classes, and reports. Pages use Bootstrap and jQuery. Data is stored with Entity Framework Core in MySQL or PostgreSQL, locally or on Amazon RDS.

The database provider is chosen in `appsettings.json`:

```json
"Database": { "Provider": "MySql" }
```

Use `PostgreSql` to switch engines. Set the matching connection string under `ConnectionStrings`.

## Local MySQL

On this Windows machine, MariaDB is already set up for the default connection string (root / `gymdesk` on port 3306). It does not start with Windows. Start it, then start the site:

```powershell
.\start-database.ps1
dotnet run
```

Open the URL printed in the console, usually `http://localhost:5290`.

If you use Docker instead:

```bash
docker compose up -d mysql
dotnet run
```

Staff: `admin` / `Admin123!`

Sample member: `member@example.com` / `Member123!`, member code `10000001`.

Change those passwords in Staff and the member profile before anyone else can reach the site.

## Local PostgreSQL

```bash
docker compose up -d postgres
```

Set `Database:Provider` to `PostgreSql`. The default connection string uses database `gymdesk`, user `postgres`, password `postgres`.

## Amazon RDS

Point the same connection string at the RDS endpoint.

MySQL example:

`Server=your-instance.region.rds.amazonaws.com;Port=3306;Database=gymdesk;User ID=gymdesk;Password=your-password;SslMode=Required`

PostgreSQL example:

`Host=your-instance.region.rds.amazonaws.com;Port=5432;Database=gymdesk;Username=gymdesk;Password=your-password;SSL Mode=Require;Trust Server Certificate=true`

The database user needs permission to create the database on first launch, or you can create an empty database named `gymdesk` first. Tables are created automatically. SMTP, currency, address, and payment methods are edited under Settings after signing in as a manager.
