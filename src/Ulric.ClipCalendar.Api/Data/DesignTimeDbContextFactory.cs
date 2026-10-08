using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ulric.ClipCalendar.Api.Data;

public sealed class SqliteDesignTimeFactory : IDesignTimeDbContextFactory<SqliteAppDbContext>
{
    public SqliteAppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SqliteAppDbContext>()
            .UseSqlite("Data Source=data/ulric.db")
            .Options;
        return new SqliteAppDbContext(options);
    }
}

public sealed class MySqlDesignTimeFactory : IDesignTimeDbContextFactory<MySqlAppDbContext>
{
    public MySqlAppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<MySqlAppDbContext>()
            .UseMySql(
                "Server=127.0.0.1;Port=3306;Database=ulric;User=ulric;Password=unused;CharSet=utf8mb4;",
                ServerVersion.Parse("5.7.39-mysql"))
            .Options;
        return new MySqlAppDbContext(options);
    }
}
