using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TransactionReader.Data;

/// <summary>
/// Used only by the EF Core tools (e.g. <c>dotnet ef migrations add</c>). The app itself gets
/// <see cref="TransactionDbContext"/> from the host's DI container.
/// </summary>
public class TransactionDbContextFactory : IDesignTimeDbContextFactory<TransactionDbContext>
{
    public TransactionDbContext CreateDbContext(string[] args)
    {
        var configuration = AppHost.CreateBuilder(args).Configuration;

        var options = new DbContextOptionsBuilder<TransactionDbContext>();
        AppHost.ConfigureDbContext(options, configuration);

        return new TransactionDbContext(options.Options);
    }
}
