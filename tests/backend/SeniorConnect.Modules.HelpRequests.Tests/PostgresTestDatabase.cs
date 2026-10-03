using Microsoft.EntityFrameworkCore;
using Npgsql;
using SeniorConnect.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.HelpRequests.Tests;

/// <summary>
/// xUnit class fixture owning a <b>dedicated, uniquely named PostgreSQL
/// database</b> for one test class (IClassFixture semantics: created before the
/// first test of the class, disposed after the last).
///
/// Why a fresh database per class instead of migrating a shared database?
/// The Phase 4 gate requires that integration tests never depend on, and never
/// pollute, a developer's database state. Two Phase 4 classes both call
/// MigrateAsync; run in parallel against one shared database they raced each
/// other (42701 "column already exists"). A per-class database removes the
/// shared mutable state entirely:
///
/// TEST START  -> create empty database (unique name)
///             -> apply the migration chain deterministically, once
/// TESTS       -> run against this known state
/// DISPOSE     -> drop the database
///
/// The connection string still comes from SC_TEST_POSTGRES (an admin-capable
/// connection to a disposable PostgreSQL instance, e.g. the docker-compose db);
/// only the database name is replaced. When the variable is absent the
/// PostgresFactAttribute skips, as before — nothing here bypasses that gate.
/// </summary>
public sealed class PostgresTestDatabase : IAsyncLifetime
{
    public const string ConnectionStringVariable = "SC_TEST_POSTGRES";

    private static readonly string? AdminConnectionString =
        Environment.GetEnvironmentVariable(ConnectionStringVariable);

    /// <summary>Null when SC_TEST_POSTGRES is unset; PostgresFactAttribute then skips.</summary>
    public NpgsqlDataSource? DataSource { get; }

    /// <summary>Connection string of this fixture's own database.</summary>
    public string ConnectionString { get; }

    /// <summary>The migration chain applied once on an empty database.</summary>
    public int AppliedMigrations { get; private set; }

    /// <summary>The name of this fixture's own database.</summary>
    public string DatabaseName { get; }

    public PostgresTestDatabase()
    {
        if (string.IsNullOrWhiteSpace(AdminConnectionString))
        {
            // Keep properties well-formed anyway; PostgresFactAttribute skips.
            DatabaseName = "unconfigured";
            ConnectionString = "Host=unconfigured";
            return;
        }

        var builder = new NpgsqlConnectionStringBuilder(AdminConnectionString);
        if (string.IsNullOrWhiteSpace(builder.Database))
        {
            throw new InvalidOperationException(
                $"{ConnectionStringVariable} must name a database (a disposable PostgreSQL " +
                "instance whose login may create databases); the test manages its own databases.");
        }

        // A unique, concurrency-safe database name per fixture instance. The
        // database named in SC_TEST_POSTGRES itself is never touched.
        DatabaseName = "sc_test_" + Guid.NewGuid().ToString("N")[..12];
        builder.Database = DatabaseName;
        ConnectionString = builder.ConnectionString;

        DataSource = new NpgsqlDataSourceBuilder(builder.ConnectionString).Build();
    }

    public async Task InitializeAsync()
    {
        if (DataSource is null)
        {
            return;
        }

        await CreateEmptyDatabaseAsync();

        await using var db = new SeniorConnectDbContext(
            new DbContextOptionsBuilder<SeniorConnectDbContext>().UseNpgsql(ConnectionString).Options,
            new DefaultTenantContext { IsPlatformScope = true });
        await db.Database.MigrateAsync();
        AppliedMigrations = (await db.Database.GetAppliedMigrationsAsync()).Count();
    }

    /// <summary>
    /// Drops this fixture's database, including lingering connections, so the
    /// next class starts from a truly empty server.
    /// </summary>
    public async Task DisposeAsync()
    {
        if (DataSource is null)
        {
            return;
        }

        await DataSource.DisposeAsync();

        try
        {
            await using var connection = await OpenAdminConnectionAsync();
            await using var drop = connection.CreateCommand();
            // The name is generated here (hex GUID prefix) and quoted as a
            // PostgreSQL identifier; it is not user input. WITH (FORCE) also
            // terminates lingering connections (PostgreSQL 13+). One statement:
            // a multi-statement batch would run inside Npgsql's implicit
            // extended-protocol transaction, where DROP DATABASE is forbidden.
            drop.CommandText = $"DROP DATABASE IF EXISTS \"{DatabaseName}\" WITH (FORCE)";
            await drop.ExecuteNonQueryAsync();
        }
        catch (PostgresException)
        {
            // Teardown must not fail the run: the name is unique anyway.
        }
    }

    private async Task CreateEmptyDatabaseAsync()
    {
        await using var connection = await OpenAdminConnectionAsync();
        await using var create = connection.CreateCommand();
        create.CommandText = $"CREATE DATABASE \"{DatabaseName}\"";
        await create.ExecuteNonQueryAsync();
    }

    private async Task<NpgsqlConnection> OpenAdminConnectionAsync()
    {
        var admin = new NpgsqlConnectionStringBuilder(AdminConnectionString)
        {
            Database = "postgres"
        };
        var connection = new NpgsqlConnection(admin.ConnectionString);
        await connection.OpenAsync();
        return connection;
    }
}

/// <summary>
/// A test class that receives <see cref="PostgresTestDatabase"/> via
/// <c>IClassFixture&lt;PostgresTestDatabase&gt;</c> runs on its own empty,
/// freshly migrated database. Facts remain skipped when SC_TEST_POSTGRES is
/// unset (gate not met), never otherwise.
/// </summary>
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PostgresTestDatabase.ConnectionStringVariable)))
        {
            Skip = $"Set {PostgresTestDatabase.ConnectionStringVariable} to a disposable PostgreSQL connection string to run.";
        }
    }
}
