namespace Ulric.ClipCalendar.Tests;

public class MySqlMigrationTests
{
    [Fact]
    public void Initial_migration_is_utf8mb4_and_avoids_mysql_8_only_sql()
    {
        var root = FindRepositoryRoot();
        var folder = Path.Combine(root, "src", "Ulric.ClipCalendar.Api", "Data", "Migrations", "MySql");
        var files = Directory.GetFiles(folder, "*.cs");
        Assert.NotEmpty(files);
        var text = string.Join("\n", files.Select(File.ReadAllText));

        Assert.Contains("utf8mb4", text, StringComparison.Ordinal);
        Assert.Contains("utf8mb4_general_ci", text, StringComparison.Ordinal);
        Assert.DoesNotContain("utf8mb4_0900", text, StringComparison.Ordinal);
        Assert.DoesNotContain("INVISIBLE", text, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Ulric.ClipCalendar.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not find the repository root.");
    }
}
