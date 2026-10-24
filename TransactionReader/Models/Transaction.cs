#nullable enable

using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace TransactionReader.Models;

public class Transaction
{
    public int Id { get; set; }

    [Required]
    public required DateOnly TransactionDate { get; set; }

    [Required]
    public required DateOnly PostDate { get; set; }
    
    [Required]
    [StringLength(3, MinimumLength = 3)]
    public required string Currency { get; set; }

    [Required]
    [Precision(19, 4)]
    public required decimal Amount { get; set; }

    public string? Description { get; set; }

    public string? Status { get; set; }

    public string? Merchant { get; set; }

    public string? Country { get; set; }

    public string? Area { get; set; }

    public CreditOrDebit? Side { get; set; }
}