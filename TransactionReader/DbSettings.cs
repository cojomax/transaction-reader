using Microsoft.Extensions.Configuration;

namespace TransactionReader;

public static class DbSettings
{
    private const string ConnectionStringVariable = "ConnectionStrings__Default";

    public static string ConnectionString =>
        new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json")
            .Build()
            .GetConnectionString("Default");
    // Environment.GetEnvironmentVariable(ConnectionStringVariable)
    // ?? throw new InvalidOperationException(
    //     $"Environment variable '{ConnectionStringVariable}' is not set.");
}
