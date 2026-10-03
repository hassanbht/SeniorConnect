using Microsoft.EntityFrameworkCore;
using SeniorConnect.Modules.Community.Domain;

namespace SeniorConnect.Modules.Community.Application;

public interface ICommunityDbContext
{
    DbSet<CommunityGroup> CommunityGroups { get; }
    DbSet<CommunityCategory> CommunityCategories { get; }
    DbSet<GroupMembership> GroupMemberships { get; }
    DbSet<CommunityInvitation> CommunityInvitations { get; }
    DbSet<CommunityEvent> CommunityEvents { get; }
    DbSet<EventRegistration> EventRegistrations { get; }
    DbSet<CommunityPost> CommunityPosts { get; }
    DbSet<CommunityComment> CommunityComments { get; }
    DbSet<CommunityReport> CommunityReports { get; }
    DbSet<CommunityModerationAction> CommunityModerationActions { get; }
    DbSet<MessageThread> MessageThreads { get; }
    DbSet<ThreadMessage> ThreadMessages { get; }
    DbSet<CommunityEventOccurrenceCancellation> EventOccurrenceCancellations { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
