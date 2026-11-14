using System.ComponentModel.DataAnnotations;

namespace TransactionReader.Finverse;

/// <summary>
/// Settings for the Finverse API, bound from the "Finverse" configuration section.
/// ClientId and ClientSecret belong in User Secrets (local) or environment variables
/// (Finverse__ClientId, Finverse__ClientSecret) — never in appsettings.json.
/// </summary>
public class FinverseOptions
{
    public const string SectionName = "Finverse";

    [Required]
    public string ClientId { get; set; } = string.Empty;

    [Required]
    public string ClientSecret { get; set; } = string.Empty;

    [Required]
    public Uri? BaseUrl { get; set; }
    
    [Required]
    public string RedirectUri { get; set; } = string.Empty;
}
