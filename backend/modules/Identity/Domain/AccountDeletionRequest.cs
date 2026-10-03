using System.Security.Cryptography;
using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Identity.Domain;

public enum DeletionTierStatus
{
    Tier1Requested = 0,
    Tier1Deactivated = 1,
    Tier2Purged = 2,
    Cancelled = 3
}

public sealed class AccountDeletionRequest : Entity
{
    private AccountDeletionRequest() { }

    [DataClass(DataClass.Operational)]
    public Guid UserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public DeletionTierStatus Status { get; private set; }

    [DataClass(DataClass.Operational)]
    public string ConfirmationToken { get; private set; } = string.Empty;

    [DataClass(DataClass.Operational)]
    public DateTimeOffset RequestedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? Tier1ExecutedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset ScheduledTier2PurgeUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? Tier2ExecutedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public string? Reason { get; private set; }

    public static AccountDeletionRequest Create(Guid userId, string? reason = null)
    {
        var token = RandomNumberGenerator.GetInt32(100000, 1000000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
        var now = DateTimeOffset.UtcNow;

        return new AccountDeletionRequest
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Status = DeletionTierStatus.Tier1Requested,
            ConfirmationToken = token,
            RequestedAtUtc = now,
            ScheduledTier2PurgeUtc = now.AddDays(30),
            Reason = reason?.Trim()
        };
    }

    public Result ConfirmAndExecuteTier1(string token)
    {
        if (Status != DeletionTierStatus.Tier1Requested)
            return Error.Conflict("INVALID_STATUS", "Deletion request is not in requested status.");

        if (ConfirmationToken != token.Trim())
            return Error.Validation("Invalid confirmation token.");

        Status = DeletionTierStatus.Tier1Deactivated;
        Tier1ExecutedAtUtc = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    public void ExecuteTier2Purge()
    {
        Status = DeletionTierStatus.Tier2Purged;
        Tier2ExecutedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Cancel()
    {
        Status = DeletionTierStatus.Cancelled;
    }
}
