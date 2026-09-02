using System.Globalization;
using CsvHelper.Configuration;

namespace TransactionReader.Models;

public sealed class TransactionMap : ClassMap<Transaction>
{
    public TransactionMap()
    {
        Map(m => m.TransactionDate).Convert(c => 
            DateOnly.ParseExact(c.Row.GetField("Transaction date") ?? string.Empty, "dd/MM/yyyy"));
        
        Map(m => m.PostDate).Convert(c => 
            DateOnly.ParseExact(c.Row.GetField("Post date") ?? string.Empty, "dd/MM/yyyy"));

        Map(m => m.Description).Name("Description");
        Map(m => m.Amount).Convert(c =>
            decimal.Parse(
                c.Row.GetField("Billing amount")?.Trim() ?? string.Empty,
                NumberStyles.Number,
                CultureInfo.InvariantCulture));
        
        Map(m => m.Currency).Name("Billing currency");
        Map(m => m.Status).Name("Transaction status");
        Map(m => m.Merchant).Name("Merchant name");
        Map(m => m.Country).Name("Country / region");
        Map(m => m.Area).Name("Area / district");
        Map(m => m.Side).Convert(c =>
            Enum.Parse<CreditOrDebit>(
                c.Row.GetField("Credit / Debit") ?? string.Empty,
                ignoreCase: true));
    }
}
