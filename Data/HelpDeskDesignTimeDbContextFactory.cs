using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace HelpDesk.Api.Data;

public sealed class HelpDeskDesignTimeDbContextFactory : IDesignTimeDbContextFactory<HelpDeskDbContext>
{
    public HelpDeskDbContext CreateDbContext(string[] args)
    {
        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
        var connectionString = configuration.GetConnectionString("HelpDesk")
            ?? throw new InvalidOperationException(
                "Configure ConnectionStrings:HelpDesk in appsettings or the ConnectionStrings__HelpDesk environment variable.");
        var options = new DbContextOptionsBuilder<HelpDeskDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new HelpDeskDbContext(options);
    }
}
