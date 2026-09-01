#nullable enable

using System.ComponentModel.DataAnnotations;

namespace TransactionReader.Models;

public class Transaction
{
    [Required]
    public required DateOnly TransactionDate { get; set; }

    [Required]
    public required DateOnly PostDate { get; set; }
    
    [Required]
    [StringLength(3, MinimumLength = 3)]
    public required string Currency { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public required decimal Amount { get; set; }

    public string? Description { get; set; }

    public string? Status { get; set; }

    public string? Merchant { get; set; }

    public string? Country { get; set; }

    public string? Area { get; set; }

    public CreditOrDebit? Credit { get; set; }
}