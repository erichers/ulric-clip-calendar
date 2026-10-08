using Microsoft.EntityFrameworkCore;

namespace Ulric.ClipCalendar.Api.Data;

public static class DatabaseSetup
{
    public const string SqliteProvider = "Sqlite";
    public const string MySqlProvider = "MySql";

    public static bool IsMySql(IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"];
        return provider is not null && provider.Equals(MySqlProvider, StringComparison.OrdinalIgnoreCase);
    }

    public static ServerVersion ResolveServerVersion(IConfiguration configuration, string connectionString)
    {
        var configured = configuration["Database:ServerVersion"];
        if (string.IsNullOrWhiteSpace(configured)
            || configured.Equals("AutoDetect", StringComparison.OrdinalIgnoreCase))
        {
            return ServerVersion.AutoDetect(connectionString);
        }

        return ServerVersion.Parse(configured);
    }

    public static void AddAppDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        if (IsMySql(configuration))
        {
            var connectionString = configuration.GetConnectionString("MySql");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "ConnectionStrings:MySql is required when Database:Provider is MySql.");
            }

            var version = ResolveServerVersion(configuration, connectionString);
            services.AddDbContext<MySqlAppDbContext>(options =>
                options.UseMySql(connectionString, version, mysql => mysql.EnableRetryOnFailure(3)));
            services.AddScoped<AppDbContext>(provider => provider.GetRequiredService<MySqlAppDbContext>());
            return;
        }

        var sqlite = configuration.GetConnectionString("Default") ?? "Data Source=data/ulric.db";
        services.AddDbContext<SqliteAppDbContext>(options => options.UseSqlite(sqlite));
        services.AddScoped<AppDbContext>(provider => provider.GetRequiredService<SqliteAppDbContext>());
    }
}
