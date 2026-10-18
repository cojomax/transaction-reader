if (args.Length == 0)
{
    Console.WriteLine("Error: Please provide a CSV file path as an argument.");
    return 1;
}

var csvFilePath = args[0];

if (!File.Exists(csvFilePath))
{
    Console.WriteLine($"Error: File '{csvFilePath}' not found.");
    return 1;
}

var transactions = TransactionReader.CsvHelper.ReadFile(csvFilePath);
    
Console.WriteLine($"Successfully read {transactions.Count} transactions.");
    
foreach (var transaction in transactions)
{
    Console.WriteLine($"{transaction.TransactionDate} | {transaction.Merchant} | {transaction.Amount} {transaction.Currency} | {transaction.Side}");
}

return 0;