using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PatternPro.Core.Persistence;
using PatternPro.Core.Persistence.Repositories;
using PatternPro.DataAccess.Persistence;
using PatternPro.DataAccess.Repositories;

namespace PatternPro.DataAccess;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the data access layer: EF Core (when Postgres is configured) or JSON files under <paramref name="appDataDirectory"/>.
    /// </summary>
    public static IServiceCollection AddPatternProDataAccess(
        this IServiceCollection services,
        IConfiguration configuration,
        string appDataDirectory)
    {
        var pgConn = configuration.GetConnectionString("Postgres");
        if (!string.IsNullOrWhiteSpace(pgConn))
        {
            services.AddDbContextFactory<PatternProDbContext>(o => o.UseNpgsql(pgConn));
            services.AddSingleton<PostgreSqlAppDataStore>();
            services.AddSingleton<IAppDataStore>(sp => sp.GetRequiredService<PostgreSqlAppDataStore>());
            services.AddSingleton<IDataAccessLayer>(sp => sp.GetRequiredService<PostgreSqlAppDataStore>());
        }
        else
        {
            services.AddSingleton<JsonAppDataStore>(_ => new JsonAppDataStore(appDataDirectory));
            services.AddSingleton<IAppDataStore>(sp => sp.GetRequiredService<JsonAppDataStore>());
            services.AddSingleton<IDataAccessLayer>(sp => sp.GetRequiredService<JsonAppDataStore>());
        }

        services.AddSingleton<IPatternRepository, PatternRepository>();
        services.AddSingleton<IPieceRepository, PieceRepository>();
        services.AddSingleton<ISizeChartRepository, SizeChartRepository>();
        services.AddSingleton<IGradingRepository, GradingRepository>();
        services.AddSingleton<IEaseOverridesRepository, EaseOverridesRepository>();
        services.AddSingleton<IMeasurementProfileRepository, MeasurementProfileRepository>();

        if (!string.IsNullOrWhiteSpace(pgConn))
            services.AddSingleton<IUserRepository, PostgresUserRepository>();
        else
            services.AddSingleton<IUserRepository>(_ => new JsonUserRepository(appDataDirectory));

        return services;
    }

    /// <summary>Applies pending EF Core migrations when PostgreSQL is configured.</summary>
    public static void MigratePatternProDatabase(this IServiceProvider services)
    {
        var factory = services.GetService<IDbContextFactory<PatternProDbContext>>();
        if (factory is null)
        {
            Console.WriteLine("[PatternPro] Data store: JSON files (ConnectionStrings:Postgres is not set).");
            return;
        }

        using var db = factory.CreateDbContext();
        db.Database.Migrate();

        services.GetService<PostgreSqlAppDataStore>()?.ImportLegacyAppKvIfNeeded();

        var conn = db.Database.GetDbConnection();
        Console.WriteLine($"[PatternPro] Data store: PostgreSQL {conn.Database} @ {conn.DataSource}");
    }
}
