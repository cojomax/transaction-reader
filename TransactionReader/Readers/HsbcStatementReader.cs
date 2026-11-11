using System.Globalization;
using System.Text.RegularExpressions;
using TransactionReader.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace TransactionReader.Readers;

/// <summary>
/// Reads transactions from an HSBC credit card eStatement PDF. The statement table has no
/// ruled cells, so rows are rebuilt from word positions and the description area is split
/// into merchant / location / country by the fixed x offsets HSBC prints them at.
/// </summary>
public static partial class HsbcStatementReader
{
    // Left edges (in PDF points) of each column, measured from real statements.
    private const double TransDateX = 95;
    private const double MerchantX = 130;
    private const double LocationX = 258;
    private const double CountryX = 330;
    private const double ForeignCurrencyX = 350;
    private const double AmountX = 475;

    // Words whose baselines are within this distance belong to the same printed line.
    private const double LineTolerance = 2;

    [GeneratedRegex(@"^\d{2}[A-Z]{3}$")]
    private static partial Regex DayMonth();

    private sealed record Line(IReadOnlyList<Word> Words)
    {
        public string Text(double fromX, double toX) 
            => string.Join(' ', Words
                .Where(w => w.BoundingBox.Left >= fromX && w.BoundingBox.Left < toX)
                .Select(w => w.Text));
    }

    public static List<Transaction> ReadFile(string filename)
    {
        using var document = PdfDocument.Open(filename);

        var statementDate = ReadStatementDate(document.GetPage(1));
        string? accountCurrency = null;
        decimal? previousBalance = null;
        decimal? statementBalance = null;
        List<Transaction> transactions = [];

        foreach (var page in document.GetPages())
        {
            var lines = GroupIntoLines(page.GetWords());
            var headerIndex = lines.FindIndex(IsTableHeader);
            
            if (headerIndex < 0)
            {
                continue;
            }

            accountCurrency ??= ReadAccountCurrency(lines[headerIndex]);

            foreach (var line in lines.Skip(headerIndex + 1))
            {
                var description = line.Text(MerchantX, AmountX);
                var amountText = line.Text(AmountX, double.MaxValue);

                if (description == "PREVIOUS BALANCE")
                {
                    previousBalance = ParseBalance(amountText);
                }
                else if (description == "STATEMENT BALANCE")
                {
                    statementBalance = ParseBalance(amountText);
                }
                else if (IsTransaction(line, amountText))
                {
                    transactions.Add(ReadTransaction(line, amountText, description, accountCurrency!, statementDate));
                }
            }
        }

        Reconcile(transactions, previousBalance, statementBalance);
        return transactions;
    }

    private static List<Line> GroupIntoLines(IEnumerable<Word> words)
    {
        List<Line> lines = [];
        List<Word> current = [];

        foreach (var word in words.OrderByDescending(w => w.BoundingBox.Bottom))
        {
            if (current.Count > 0 && current[0].BoundingBox.Bottom - word.BoundingBox.Bottom > LineTolerance)
            {
                lines.Add(new Line([..current.OrderBy(w => w.BoundingBox.Left)]));
                current = [];
            }

            current.Add(word);
        }

        if (current.Count > 0)
        {
            lines.Add(new Line([..current.OrderBy(w => w.BoundingBox.Left)]));
        }

        return lines;
    }

    private static bool IsTableHeader(Line line) =>
        line.Words.Count > 1 && line.Words[0].Text == "Post" && line.Words.Any(w => w.Text == "Amount");

    // The header reads "Amount (HKD)", which is the billing currency for every row.
    private static string ReadAccountCurrency(Line header)
    {
        var words = header.Words.Select(w => w.Text).ToList();
        var index = words.IndexOf("Amount");
        if (index < 0 || index + 1 >= words.Count)
        {
            throw new InvalidDataException("Could not find the billing currency in the statement table header.");
        }

        return words[index + 1].Trim('(', ')');
    }

    // "Statement date" is printed as a label with the date on the line beneath it, e.g. "21 OCT 2023".
    private static DateOnly ReadStatementDate(Page page)
    {
        var words = page.GetWords().ToList();
        var labelIndex = words.FindIndex(w => w.Text == "Statement");
        while (labelIndex >= 0 && (labelIndex + 1 >= words.Count || words[labelIndex + 1].Text != "date"))
        {
            labelIndex = words.FindIndex(labelIndex + 1, w => w.Text == "Statement");
        }

        if (labelIndex < 0)
        {
            throw new InvalidDataException("Could not find the statement date on the first page.");
        }

        var label = words[labelIndex].BoundingBox;
        var labelRight = words[labelIndex + 1].BoundingBox.Right;

        var value = string.Join(' ', words
            .Where(w => w.BoundingBox.Bottom < label.Bottom - LineTolerance
                        && w.BoundingBox.Bottom > label.Bottom - 20
                        && w.BoundingBox.Left > label.Left - 20
                        && w.BoundingBox.Left < labelRight + 20)
            .OrderBy(w => w.BoundingBox.Left)
            .Select(w => w.Text));

        return DateOnly.ParseExact(value, "d MMM yyyy", CultureInfo.InvariantCulture);
    }

    // Transaction rows start with a post date and a transaction date (e.g. "23SEP 22SEP") and end with an amount.
    // Continuation lines (Apple Pay device, exchange rate, reference numbers) have neither and are skipped.
    private static bool IsTransaction(Line line, string amountText) =>
        line.Words.Count > 2
        && line.Words[0].BoundingBox.Left < TransDateX && DayMonth().IsMatch(line.Words[0].Text)
        && line.Words[1].BoundingBox.Left < MerchantX && DayMonth().IsMatch(line.Words[1].Text)
        && amountText.Length > 0;

    private static Transaction ReadTransaction(
        Line line, string amountText, string description, string accountCurrency, DateOnly statementDate)
    {
        var isCredit = amountText.EndsWith("CR", StringComparison.Ordinal);
        var location = line.Text(LocationX, CountryX);
        var country = line.Text(CountryX, ForeignCurrencyX);

        // Rows without a country (payments, fee reversals) are free text across the whole description area.
        var hasMerchantColumns = country.Length > 0;

        return new Transaction
        {
            PostDate = ParseDayMonth(line.Words[0].Text, statementDate),
            TransactionDate = ParseDayMonth(line.Words[1].Text, statementDate),
            Currency = accountCurrency,
            Amount = ParseAmount(amountText),
            Description = description,
            Merchant = hasMerchantColumns ? line.Text(MerchantX, LocationX) : description,
            Area = hasMerchantColumns && location.Length > 0 ? location : null,
            Country = hasMerchantColumns ? country : null,
            Side = isCredit ? CreditOrDebit.Credit : CreditOrDebit.Debit,
        };
    }

    // Dates carry no year; a month later than the statement month belongs to the previous year (Dec rows on a Jan statement).
    private static DateOnly ParseDayMonth(string text, DateOnly statementDate)
    {
        var date = DateOnly.ParseExact($"{text}{statementDate.Year}", "ddMMMyyyy", CultureInfo.InvariantCulture);
        return date.Month > statementDate.Month ? date.AddYears(-1) : date;
    }

    private static decimal ParseAmount(string text) =>
        decimal.Parse(text.Replace("CR", string.Empty), NumberStyles.Number, CultureInfo.InvariantCulture);

    // Balances are owed amounts; a "CR" balance means the bank owes the cardholder.
    private static decimal ParseBalance(string text) =>
        text.EndsWith("CR", StringComparison.Ordinal) ? -ParseAmount(text) : ParseAmount(text);

    // The statement must balance: previous balance + debits - credits = statement balance.
    // A mismatch means a row was missed or misread, so fail rather than import partial data.
    private static void Reconcile(List<Transaction> transactions, decimal? previousBalance, decimal? statementBalance)
    {
        if (previousBalance is null || statementBalance is null)
        {
            throw new InvalidDataException("Could not find the previous and statement balances to reconcile against.");
        }

        var net = transactions.Sum(t => t.Side == CreditOrDebit.Credit ? -t.Amount : t.Amount);
        if (previousBalance + net != statementBalance)
        {
            throw new InvalidDataException(
                $"Statement does not reconcile: previous balance {previousBalance} + transactions {net} " +
                $"!= statement balance {statementBalance}. {transactions.Count} transactions were read.");
        }
    }
}
