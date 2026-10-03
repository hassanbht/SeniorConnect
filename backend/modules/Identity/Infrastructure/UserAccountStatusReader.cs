using Microsoft.EntityFrameworkCore;
using SeniorConnect.Modules.Identity.Application;
using SeniorConnect.Modules.Identity.Contracts;
using SeniorConnect.Modules.Identity.Domain;

namespace SeniorConnect.Modules.Identity.Infrastructure;

public sealed class UserAccountStatusReader : IUserAccountStatusReader
{
    private readonly IIdentityDbContext _db;

    public UserAccountStatusReader(IIdentityDbContext db)
    {
        _db = db;
    }

    public async Task<bool> IsActiveAsync(Guid userId, CancellationToken ct = default)
    {
        return await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.Status == UserStatus.Active)
            .FirstOrDefaultAsync(ct);
    }
}
