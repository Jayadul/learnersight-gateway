using System.Linq.Expressions;
using Microsoft.Extensions.Logging.Abstractions;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Application.Services;
using NetZeroGateway.Domain.Entities;

namespace NetZeroGateway.Tests;

/// <summary>
/// The partial-update contract. Release 2 shipped a bug where saving one field of a
/// channel config blanked the rest; these credentials are what every integrator
/// authenticates with, so the same mistake here would take the gateway down for all of
/// them at once.
/// </summary>
public class GatewayCredentialServiceTests
{
    private const string Mask = "***";

    private static (GatewayCredentialService Service, GatewayCredential Row) Build()
    {
        var row = new GatewayCredential
        {
            Username = "netzero-gateway",
            // PlainTextEncryption is the identity, so the stored value reads as the key.
            ApiKeyEncrypted = "original-api-key",
            SecretKeyEncrypted = "original-secret-key"
        };

        var service = new GatewayCredentialService(
            new FakeGatewayCredentialRepository(row),
            new PlainTextEncryption(),
            NullLogger<GatewayCredentialService>.Instance);

        return (service, row);
    }

    [Fact]
    public async Task Reading_returns_the_decrypted_keys_for_the_admin_panel()
    {
        var (service, _) = Build();

        var dto = await service.GetAsync();

        // SuperAdmin has to be able to read the pair back to hand it to an integrator;
        // nothing else can, since the column is ciphertext.
        Assert.Equal("netzero-gateway", dto.Username);
        Assert.Equal("original-api-key", dto.ApiKey);
        Assert.Equal("original-secret-key", dto.SecretKey);
        Assert.True(dto.IsReadable);
    }

    [Fact]
    public async Task Unreadable_ciphertext_is_flagged_rather_than_shown_as_empty_keys()
    {
        var row = new GatewayCredential
        {
            Username = "netzero-gateway",
            ApiKeyEncrypted = "whatever",
            SecretKeyEncrypted = "whatever"
        };
        var service = new GatewayCredentialService(
            new FakeGatewayCredentialRepository(row),
            new ThrowingEncryption(),
            NullLogger<GatewayCredentialService>.Instance);

        var dto = await service.GetAsync();

        // Two blank boxes would read as "no credentials set" — a different problem with a
        // different fix. The flag lets the UI say which one it actually is.
        Assert.False(dto.IsReadable);
        Assert.Equal("", dto.ApiKey);
        Assert.Equal("", dto.SecretKey);
    }

    [Fact]
    public async Task Updating_only_the_username_leaves_both_keys_intact()
    {
        var (service, row) = Build();

        await service.UpdateAsync(new UpdateGatewayCredentialRequest { Username = "acme-billing" });

        Assert.Equal("acme-billing", row.Username);
        Assert.Equal("original-api-key", row.ApiKeyEncrypted);
        Assert.Equal("original-secret-key", row.SecretKeyEncrypted);
    }

    [Fact]
    public async Task Updating_only_the_api_key_leaves_the_secret_and_username_intact()
    {
        var (service, row) = Build();

        await service.UpdateAsync(new UpdateGatewayCredentialRequest { ApiKey = "rotated-api-key" });

        Assert.Equal("rotated-api-key", row.ApiKeyEncrypted);
        Assert.Equal("original-secret-key", row.SecretKeyEncrypted);
        Assert.Equal("netzero-gateway", row.Username);
    }

    [Fact]
    public async Task Posting_the_placeholder_back_still_keeps_the_real_keys()
    {
        var (service, row) = Build();

        // Reads are no longer masked, but any client that renders "***" as a placeholder
        // and posts it back must not store the placeholder as the live key.
        await service.UpdateAsync(new UpdateGatewayCredentialRequest
        {
            Username = "netzero-gateway",
            ApiKey = Mask,
            SecretKey = Mask
        });

        Assert.Equal("original-api-key", row.ApiKeyEncrypted);
        Assert.Equal("original-secret-key", row.SecretKeyEncrypted);
    }

    [Fact]
    public async Task Blank_fields_are_treated_as_absent_rather_than_as_an_erasure()
    {
        var (service, row) = Build();

        await service.UpdateAsync(new UpdateGatewayCredentialRequest
        {
            Username = "  ",
            ApiKey = "",
            SecretKey = null
        });

        Assert.Equal("netzero-gateway", row.Username);
        Assert.Equal("original-api-key", row.ApiKeyEncrypted);
        Assert.Equal("original-secret-key", row.SecretKeyEncrypted);
    }

    [Fact]
    public async Task Both_keys_can_still_be_rotated_in_one_request()
    {
        var (service, row) = Build();

        await service.UpdateAsync(new UpdateGatewayCredentialRequest
        {
            ApiKey = "new-api-key-value",
            SecretKey = "new-secret-key-value"
        });

        Assert.Equal("new-api-key-value", row.ApiKeyEncrypted);
        Assert.Equal("new-secret-key-value", row.SecretKeyEncrypted);
    }

    [Fact]
    public async Task A_key_short_enough_to_be_a_typo_is_refused()
    {
        var (service, row) = Build();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateAsync(new UpdateGatewayCredentialRequest { ApiKey = "abc" }));

        Assert.Equal("original-api-key", row.ApiKeyEncrypted);
    }
}

/// <summary>Stands in for a changed encryption key: stored ciphertext no longer opens.</summary>
internal sealed class ThrowingEncryption : IEncryptionService
{
    public string Encrypt(string plainText) => plainText;
    public string Decrypt(string cipherText) => throw new InvalidOperationException("bad key");
}

internal sealed class FakeGatewayCredentialRepository(GatewayCredential? current)
    : IGatewayCredentialRepository
{
    public Task<GatewayCredential?> GetCurrentAsync() => Task.FromResult(current);

    public void Update(GatewayCredential entity) { }
    public Task<int> SaveChangesAsync() => Task.FromResult(1);

    public Task<GatewayCredential?> GetByIdAsync(Guid id) => Unused.Fail<Task<GatewayCredential?>>();
    public Task<List<GatewayCredential>> GetAllAsync() => Unused.Fail<Task<List<GatewayCredential>>>();
    public Task<List<GatewayCredential>> FindAsync(Expression<Func<GatewayCredential, bool>> predicate)
        => Unused.Fail<Task<List<GatewayCredential>>>();
    public Task AddAsync(GatewayCredential entity) => Unused.Fail<Task>();
    public void Delete(GatewayCredential entity) => Unused.Fail<object>();
}
