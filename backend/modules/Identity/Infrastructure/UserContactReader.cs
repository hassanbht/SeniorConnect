using Microsoft.EntityFrameworkCore;
using SeniorConnect.Modules.Identity.Application;
using SeniorConnect.Modules.Identity.Contracts;

namespace SeniorConnect.Modules.Identity.Infrastructure;

public sealed class UserContactReader : IUserContactReader
{
    private readonly IIdentityDbContext _db;

    public UserContactReader(IIdentityDbContext db)
    {
        _db = db;
    }

    public async Task<UserContact?> GetContactAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => new { u.DisplayName, u.Phone })
            .FirstOrDefaultAsync(ct);

        return user is null ? null : new UserContact(user.DisplayName, user.Phone);
    }
}
