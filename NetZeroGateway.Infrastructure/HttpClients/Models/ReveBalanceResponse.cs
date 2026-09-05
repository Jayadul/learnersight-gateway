using System.Text.Json.Serialization;

namespace NetZeroGateway.Infrastructure.HttpClients.Models;

/// <summary>
/// REVE's client balance. Unlike the SMS providers added in Release 3, REVE reports a
/// credit limit alongside the balance.
/// </summary>
public class ReveBalanceResponse
{
    [JsonPropertyName("Balance")] public decimal? Balance { get; set; }
    [JsonPropertyName("CreditLimit")] public decimal? CreditLimit { get; set; }

    /// <summary>Set by the client when the response could not be read, not by REVE.</summary>
    [JsonIgnore] public string? Error { get; set; }

    [JsonIgnore] public bool IsSuccess => Error is null && Balance is not null;
}
