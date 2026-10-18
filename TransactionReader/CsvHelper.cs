using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using TransactionReader.Models;

namespace TransactionReader;

public static class CsvHelper
{
    private static readonly CsvConfiguration Config = new(CultureInfo.InvariantCulture)
    {
        TrimOptions = TrimOptions.Trim,
        WhiteSpaceChars = [' ', '\t'],
    };
    
    public static List<Transaction> ReadFile(string filename)
    {
        using var reader = new StreamReader(filename);
        using var csv = new CsvReader(reader, Config);

        csv.Context.RegisterClassMap<TransactionMap>();
        return [..csv.GetRecords<Transaction>()];
    }
}