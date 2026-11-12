using Microsoft.Extensions.DependencyInjection;
using TransactionReader;
using TransactionReader.Data;

using var host = AppHost.CreateBuilder(args).Build();

if (args.Length == 0)
{
    Console.WriteLine("Error: Please provide a CSV or PDF statement file path as an argument.");
    return 1;
}

var filePath = args[0];

if (!File.Exists(filePath))
{
    Console.WriteLine($"Error: File '{filePath}' not found.");
    return 1;
}

var transactions = Path.GetExtension(filePath).ToLowerInvariant() switch
{
    ".csv" => TransactionReader.Readers.CsvHelper.ReadFile(filePath),
    ".pdf" => TransactionReader.Readers.HsbcStatementReader.ReadFile(filePath),
    _ => null,
};

if (transactions is null)
{
    Console.WriteLine($"Error: Unsupported file type '{Path.GetExtension(filePath)}'. Expected .csv or .pdf.");
    return 1;
}
    
Console.WriteLine($"Successfully read {transactions.Count} transactions.");
    
foreach (var transaction in transactions)
{
    Console.WriteLine($"{transaction.TransactionDate} | {transaction.Merchant} | {transaction.Amount} {transaction.Currency} | {transaction.Side}");
}

// using var scope = host.Services.CreateScope();
// var db = scope.ServiceProvider.GetRequiredService<TransactionDbContext>();
// db.Transactions.AddRange(transactions);
// var saved = db.SaveChanges();
//
// Console.WriteLine($"Saved {saved} transactions to the database.");

return 0;