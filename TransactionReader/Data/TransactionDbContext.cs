using Microsoft.EntityFrameworkCore;
using TransactionReader.Models;

namespace TransactionReader.Data;

public class TransactionDbContext(DbContextOptions<TransactionDbContext> options) : DbContext(options)
{
    public DbSet<Transaction> Transactions => Set<Transaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Transaction>()
            .Property(t => t.Side)
            .HasConversion<string>();
    }
}
