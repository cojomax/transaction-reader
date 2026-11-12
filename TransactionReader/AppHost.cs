using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TransactionReader.Data;
using TransactionReader.Finverse;

namespace TransactionReader;

/// <summary>
/// Single place that configures the Generic Host, so the app and the EF Core design-time
/// tooling (<see cref="TransactionDbContextFactory"/>) share the same configuration.
/// </summary>
public static class AppHost
{
    // QUESTION: what calls this and what args are provided?
    public static HostApplicationBuilder CreateBuilder(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            // Resolve appsettings.json from the build output, regardless of the working directory.
            ContentRootPath = AppContext.BaseDirectory,
        });

        // Configuration sources (later overrides earlier), added by the host:
        //   appsettings.json → appsettings.{Environment}.json → User Secrets (Development only)
        //   → environment variables → command-line args
        
        // TODO: Consider extension methods instead of AppHost?

        builder.Services.AddDbContext<TransactionDbContext>(options =>
            ConfigureDbContext(options, builder.Configuration));

        builder.Services.AddOptions<FinverseOptions>()
            .BindConfiguration(FinverseOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return builder;
    }

    public static void ConfigureDbContext(DbContextOptionsBuilder options, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

        options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention();
    }
}
