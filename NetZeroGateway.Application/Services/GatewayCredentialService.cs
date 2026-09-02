using Microsoft.Extensions.Logging;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Exceptions;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Domain.Entities;

namespace NetZeroGateway.Application.Services;

public class GatewayCredentialService(
    IGatewayCredentialRepository repo,
    IEncryptionService enc,
    ILogger<GatewayCredentialService> logger
) : IGatewayCredentialService
{
    /// <summary>Never persisted as a key value, whoever sends it.</summary>
    private const string MaskedValue = "***";

    /// <summary>Short keys are worth rejecting here — a one-character secret is a typo.</summary>
    private const int MinimumKeyLength = 8;

    public async Task<GatewayCredentialDto> GetAsync()
    {
        var entity = await repo.GetCurrentAsync()
            ?? throw new NotFoundException(
                "No gateway credentials exist yet. They are created by the seeder on first "
                + "startup — check Seed:GatewayCredentials in appsettings.");

        return ToDto(entity);
    }

    public async Task<GatewayCredentialDto> UpdateAsync(UpdateGatewayCredentialRequest request)
    {
        var entity = await repo.GetCurrentAsync()
            ?? throw new NotFoundException("No gateway credentials exist yet.");

        var changed = new List<string>();

        // Each field is applied only when it was actually sent. A null field means "not
        // in this request", and the mask means "the form is echoing back what it was
        // shown" — neither is a value the caller intends to store.
        if (Supplied(request.Username) is { } username && username != entity.Username)
        {
            entity.Username = username;
            changed.Add("username");
        }

        if (Supplied(request.ApiKey) is { } apiKey)
        {
            Validate(apiKey, nameof(request.ApiKey));
            entity.ApiKeyEncrypted = enc.Encrypt(apiKey);
            changed.Add("apikey");
        }

        if (Supplied(request.SecretKey) is { } secretKey)
        {
            Validate(secretKey, nameof(request.SecretKey));
            entity.SecretKeyEncrypted = enc.Encrypt(secretKey);
            changed.Add("secretkey");
        }

        if (changed.Count == 0) return ToDto(entity);

        repo.Update(entity);
        await repo.SaveChangesAsync();

        // Names the fields, never the values — this log ends up in Seq.
        logger.LogWarning(
            "[GATEWAY_AUTH] Credentials updated. Fields={Fields}. Integrators using the "
            + "previous values will start receiving 101 REJECTD.",
            string.Join(",", changed));

        return ToDto(entity);
    }

    /// <summary>
    /// Returns the trimmed value, or null when the caller did not supply one. A literal
    /// "***" still counts as "not supplied": the read endpoint no longer masks, but any
    /// client that renders a placeholder and posts it back would otherwise store the
    /// placeholder as the live key and lock every integrator out.
    /// </summary>
    private static string? Supplied(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed == MaskedValue ? null : trimmed;
    }

    private static void Validate(string value, string field)
    {
        if (value.Length < MinimumKeyLength)
            throw new InvalidOperationException(
                $"{field} must be at least {MinimumKeyLength} characters.");
    }

    /// <summary>
    /// Decrypts both keys for the admin panel. A SuperAdmin needs the exact pair to hand
    /// to an integrator, and this is the only place it can be read back — the column is
    /// ciphertext and the seeded value is wrong the moment anyone rotates.
    /// </summary>
    private GatewayCredentialDto ToDto(GatewayCredential c)
    {
        var dto = new GatewayCredentialDto
        {
            Id = c.Id,
            Username = c.Username,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt
        };

        try
        {
            dto.ApiKey = enc.Decrypt(c.ApiKeyEncrypted);
            dto.SecretKey = enc.Decrypt(c.SecretKeyEncrypted);
        }
        catch (Exception ex)
        {
            // Almost always a changed encryption key. Two empty boxes would read as
            // "no credentials set", which is a different problem with a different fix.
            logger.LogError(ex,
                "[GATEWAY_AUTH] Stored gateway credentials could not be decrypted for display.");
            dto.IsReadable = false;
            dto.ApiKey = string.Empty;
            dto.SecretKey = string.Empty;
        }

        return dto;
    }
}
