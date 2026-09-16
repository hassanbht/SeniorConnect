using Microsoft.EntityFrameworkCore;
using SeniorConnect.Modules.Community.Domain;

namespace SeniorConnect.Modules.Community.Application;

public interface ICommunityDbContext
{
    DbSet<CommunityGroup> CommunityGroups { get; }
    DbSet<GroupMembership> GroupMemberships { get; }
    DbSet<CommunityEvent> CommunityEvents { get; }
    DbSet<EventRegistration> EventRegistrations { get; }
    DbSet<MessageThread> MessageThreads { get; }
    DbSet<ThreadMessage> ThreadMessages { get; }
    DbSet<CommunityEventOccurrenceCancellation> EventOccurrenceCancellations { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
