using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;

namespace GymDesk.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, string provider, string connectionString)
    {
        await EnsureDatabaseExistsAsync(provider, connectionString);
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GymDbContext>();
        await db.Database.EnsureCreatedAsync();
        var memberHasher = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.IPasswordHasher<Member>>();
        var staffHasher = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.IPasswordHasher<StaffUser>>();
        await DbSeeder.SeedAsync(db, memberHasher, staffHasher);
    }

    private static async Task EnsureDatabaseExistsAsync(string provider, string connectionString)
    {
        if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            var database = RequiredDatabaseName(builder.InitialCatalog);
            builder.InitialCatalog = "master";
            await using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync();
            await using var createDatabase = new SqlCommand(
                $"IF DB_ID(N'{database}') IS NULL CREATE DATABASE [{database}]",
                connection);
            await createDatabase.ExecuteNonQueryAsync();
            return;
        }

        if (provider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase))
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            var database = RequiredDatabaseName(builder.Database);
            builder.Database = "postgres";
            await using var connection = new NpgsqlConnection(builder.ConnectionString);
            await connection.OpenAsync();
            await using var exists = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @name", connection);
            exists.Parameters.AddWithValue("name", database);
            if (await exists.ExecuteScalarAsync() is null)
            {
                await using var create = new NpgsqlCommand($"CREATE DATABASE {database}", connection);
                await create.ExecuteNonQueryAsync();
            }
            return;
        }

        var mysql = new MySqlConnectionStringBuilder(connectionString);
        var mysqlDatabase = RequiredDatabaseName(mysql.Database);
        mysql.Database = "";
        await using var mysqlConnection = new MySqlConnection(mysql.ConnectionString);
        await mysqlConnection.OpenAsync();
        await using var command = new MySqlCommand(
            $"CREATE DATABASE IF NOT EXISTS `{mysqlDatabase}` CHARACTER SET utf8mb4",
            mysqlConnection);
        await command.ExecuteNonQueryAsync();
    }

    private static string RequiredDatabaseName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || !Regex.IsMatch(name, "^[A-Za-z0-9_]+$"))
        {
            throw new InvalidOperationException("The database name must contain only letters, numbers, and underscores.");
        }

        return name;
    }
}
