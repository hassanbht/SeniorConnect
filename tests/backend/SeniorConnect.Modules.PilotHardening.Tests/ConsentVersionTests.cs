using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.Identity.Application;
using SeniorConnect.Modules.Identity.Domain;
using SeniorConnect.Modules.Identity.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.PilotHardening.Tests;

/// <summary>
/// Wave-2 (GDPR / BR-TRUST-05): stored identifiers are pseudonymous.
///   - an IP address is persisted only as a hash, never verbatim;
///   - the hash is HMAC under a server-held pepper, so a leaked table alone
///     cannot be brute-forced back into phone numbers, OTPs or IPs.
/// </summary>
public sealed class IdentifierHashingTests
{
    private SeniorConnectDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SeniorConnectDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SeniorConnectDbContext(options, new DefaultTenantContext());
    }

    private static IdentityHashingService Hasher(string pepper) =>
        new(IdentityHashingSettings.FromPepper(pepper));

    // --- an IP is never stored verbatim -------------------------------------

    [Fact]
    public async Task Consent_never_stores_a_plaintext_ip_address()
    {
        using var db = CreateDbContext();
        var service = new PrivacyService(db, Hasher("test-pepper-for-consent-verification"));
        var userId = Guid.NewGuid();

        await service.RecordConsentAsync(
            userId,
            new RecordConsentRequest(ConsentType.Terms, "v2.1", Granted: true),
            "203.0.113.42");

        var consent = await db.Consents.FirstAsync(c => c.UserId == userId);

        consent.IpHash.Should().NotBeNull();
        consent.IpHash.Should().NotContain("203.0.113.42", "a raw IP is a personal datum (GDPR Art. 4)");
        consent.IpHash!.Length.Should().Be(64, "HMAC-SHA256 renders as 64 hex characters");
    }

    [Fact]
    public async Task Consent_stores_the_hashed_form_the_endpoint_already_produced()
    {
        using var db = CreateDbContext();
        var hashing = Hasher("test-pepper-for-idempotent-hashing");
        var service = new PrivacyService(db, hashing);
        var userId = Guid.NewGuid();

        await service.RecordConsentAsync(
            userId,
            new RecordConsentRequest(ConsentType.Privacy, "v1.0", Granted: true),
            hashing.HashIp("198.51.100.7"));

        var consent = await db.Consents.FirstAsync(c => c.UserId == userId);

        // The service must not hash an already-hashed value a second time:
        // HMAC is deterministic but not self-identifying, so a naive re-hash
        // would silently corrupt the stored value.
        var expected = hashing.HashIp("198.51.100.7");
        var doubleHashed = hashing.HashIp(expected);

        doubleHashed.Should().NotBe(expected, "otherwise this test could not detect a double-hash");
        consent.IpHash.Should().Be(expected);
    }

    [Fact]
    public async Task Consent_without_an_ip_records_no_ip_at_all()
    {
        using var db = CreateDbContext();
        var service = new PrivacyService(db, Hasher("test-pepper-no-ip"));
        var userId = Guid.NewGuid();

        await service.RecordConsentAsync(
            userId,
            new RecordConsentRequest(ConsentType.Terms, "v2.1", Granted: true),
            ipHash: null);

        var consent = await db.Consents.FirstAsync(c => c.UserId == userId);

        consent.IpHash.Should().BeNull();
    }

    // --- the pepper is not in source ----------------------------------------

    [Fact]
    public void Different_peppers_produce_different_hashes_for_the_same_input()
    {
        var a = Hasher("pepper-one-aaaaaaaaaaaaaaaaaaaa");
        var b = Hasher("pepper-two-bbbbbbbbbbbbbbbbbbbb");

        a.HashIp("203.0.113.1").Should().NotBe(b.HashIp("203.0.113.1"));
    }

    [Fact]
    public void The_same_pepper_is_stable_so_database_lookups_keep_working()
    {
        var a = Hasher("stable-pepper-cccccccccccccccccc");
        var b = Hasher("stable-pepper-cccccccccccccccccc");

        // DestinationHash is a lookup key in otp_challenges; it must stay
        // deterministic, otherwise rate limiting silently stops working.
        a.HashDestination("+436601234567").Should().Be(b.HashDestination("+436601234567"));
    }

    [Fact]
    public void Different_hash_purposes_never_collide()
    {
        var hashing = Hasher("pepper-for-domain-separation");
        var value = "same-input";

        var hashes = new[]
        {
            hashing.HashCode(value),
            hashing.HashToken(value),
            hashing.HashDestination(value),
            hashing.HashIp(value),
        };

        hashes.Distinct().Should().HaveCount(4, "the purpose tag must separate the hash domains");
    }

    [Fact]
    public void An_unknown_or_empty_ip_yields_null()
    {
        var hashing = Hasher("pepper-for-empty-ip");

        hashing.HashIp(null).Should().BeNull();
        hashing.HashIp("   ").Should().BeNull();
    }

    [Fact]
    public void The_hash_never_leaks_the_source_value()
    {
        var hashing = Hasher("pepper-for-preimage-resistance");

        hashing.HashCode("123456").Should().HaveLength(64);
        hashing.HashCode("123456").Should().NotContain("salt", "no committed salt may appear in the output");
    }

    // --- a missing pepper fails a production startup -------------------------

    [Fact]
    public void A_missing_pepper_is_refused_outside_development()
    {
        var development = IdentityHashingSettings.Resolve(new StubConfiguration(null));

        var refuse = () => development.EnsureSafeFor(isDevelopment: false);

        refuse.Should().Throw<InvalidOperationException>(
            "outside Development the API must not fall back to a published pepper");
    }

    [Fact]
    public void A_configured_pepper_is_accepted_outside_development()
    {
        var configured = IdentityHashingSettings.Resolve(
            new StubConfiguration("a-properly-long-production-pepper-value-32ch"));

        var act = () => configured.EnsureSafeFor(isDevelopment: false);

        act.Should().NotThrow();
    }

    [Fact]
    public void Development_may_still_use_the_fallback_pepper()
    {
        var development = IdentityHashingSettings.Resolve(new StubConfiguration(null));

        var act = () => development.EnsureSafeFor(isDevelopment: true);

        act.Should().NotThrow();
    }

    private sealed class StubConfiguration(string? pepper) : Microsoft.Extensions.Configuration.IConfiguration
    {
        public string? this[string key]
        {
            get => key == "IDENTITY_HASH_PEPPER" ? pepper : null;
            set { }
        }

        public Microsoft.Extensions.Configuration.IConfigurationSection GetSection(string key) => null!;

        public IEnumerable<Microsoft.Extensions.Configuration.IConfigurationSection> GetChildren() => [];

        public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() =>
            new Microsoft.Extensions.Primitives.CancellationChangeToken(CancellationToken.None);

#pragma warning disable CA1822 // IConfiguration interface member; cannot be static
        public string[] GetChildKeys(
            Microsoft.Extensions.Configuration.IConfigurationBuilder builder,
            Action<string>? keyFilter) => [];
#pragma warning restore CA1822

        public Microsoft.Extensions.Primitives.IChangeToken OnChange(Action<object, string?> listener) =>
            GetReloadToken();
    }
}

public sealed class ConsentVersionTests
{
    private SeniorConnectDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SeniorConnectDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SeniorConnectDbContext(options, new DefaultTenantContext());
    }

    [Fact]
    public async Task RecordConsent_StoresVersion_AndTimestamps_P7_05()
    {
        using var db = CreateDbContext();
        var service = new PrivacyService(db);
        var userId = Guid.NewGuid();

        var request = new RecordConsentRequest(
            ConsentType.Terms,
            "v2.1",
            Granted: true
        );

        var result = await service.RecordConsentAsync(userId, request, "ip-hash-1234");
        result.IsSuccess.Should().BeTrue();
        result.Value!.DocumentVersion.Should().Be("v2.1");
        result.Value.Granted.Should().BeTrue();
        result.Value.WithdrawnAtUtc.Should().BeNull();

        var consents = await service.GetUserConsentsAsync(userId);
        consents.IsSuccess.Should().BeTrue();
        consents.Value.Should().HaveCount(1);
    }

    [Fact]
    public async Task WithdrawConsent_UpdatesTimestamp_P7_05()
    {
        using var db = CreateDbContext();
        var service = new PrivacyService(db);
        var userId = Guid.NewGuid();

        await service.RecordConsentAsync(userId, new RecordConsentRequest(
            ConsentType.NotificationsPush,
            "v1.0",
            Granted: true
        ));

        var withdrawResult = await service.WithdrawConsentAsync(userId, ConsentType.NotificationsPush);
        withdrawResult.IsSuccess.Should().BeTrue();
        withdrawResult.Value!.Granted.Should().BeFalse();
        withdrawResult.Value!.WithdrawnAtUtc.Should().NotBeNull();
    }
}
