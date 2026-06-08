using System.Text.Json;
using Microsoft.Extensions.Logging;
using NetZeroGateway.Infrastructure.HttpClients.Models;

namespace NetZeroGateway.Infrastructure.HttpClients;

public class ReveSmsClient(HttpClient http, ILogger<ReveSmsClient> logger) : IReveSmsClient
{
    public async Task<ReveSmsResponse> SendAsync(
        string apiKey, string secretKey, string callerID,
        string toUser, string message, CancellationToken ct = default)
    {
        logger.LogInformation("[REVE_SMS] POST /sendtext to={To}", toUser);

        var form = new FormUrlEncodedContent([
            new("apikey",         apiKey),
            new("secretkey",      secretKey),
            new("callerID",       callerID),
            new("toUser",         toUser),
            new("messageContent", message)
        ]);

        var resp = await http.PostAsync("/sendtext", form, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        logger.LogDebug("[REVE_SMS] Raw response: {Body}", body);

        try
        {
            return JsonSerializer.Deserialize<ReveSmsResponse>(body)
                ?? new ReveSmsResponse { Status = "ERR", Text = "null_response" };
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "[REVE_SMS] JSON parse error. Body={Body}", body);
            return new ReveSmsResponse { Status = "ERR", Text = ex.Message };
        }
    }
}
