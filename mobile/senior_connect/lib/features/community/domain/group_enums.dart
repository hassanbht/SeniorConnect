// lib/features/community/domain/group_enums.dart
//
// Community Groups enums. Order MUST mirror the backend C# enum declaration
// order exactly — no `[JsonStringEnumConverter]` is registered for these
// types (see EventRsvpStatus usage in event_detail_notifier.dart, which
// already round-trips as a plain int), so System.Text.Json serializes and
// expects the ordinal (int) value, not the name.
//
// Backend source of truth:
//   backend/modules/Community/Domain/CommunityGroup.cs
//   backend/modules/Community/Domain/GroupMembership.cs
//   backend/modules/Community/Domain/CommunityPost.cs
//   backend/modules/Community/Domain/CommunityReport.cs

enum CreatorType {
  individual,
  volunteer,
  organizationStaff,
  verifiedOrganization,
}

enum GroupType {
  seniorCommunity,
  migrantLanguageCommunity,
  interestGroup,
  learningGroup,
  familyGroup,
  volunteerGroup,
  neighborhoodGroup,
  organizationCommunity,
  supportGroup,
}

enum GroupVisibility { public, private, organizationPrivate }

enum GroupStatus { draft, active, paused, archived, blocked }

enum GroupVerificationStatus {
  unverified,
  organizationVerified,
  platformVerified,
}

enum GroupMemberRole { owner, admin, moderator, member }

enum GroupMembershipStatus {
  pending,
  active,
  rejected,
  left,
  removed,
  muted,
  banned,
}

enum CommunityPostType { post, announcement }

enum CommunityPostStatus { visible, pendingReview, hidden, removed, archived }

enum CommunityReportTargetType { group, member, post, comment, event }

enum CommunityReportCategory {
  spam,
  harassment,
  hate,
  inappropriateContent,
  impersonation,
  unsafeBehavior,
  other,
}

/// Decodes a backend enum ordinal (int) into its Dart equivalent. Falls back
/// to matching a string name defensively, then to [fallback] if unrecognized
/// — an unknown value from a newer backend must never crash the app.
T enumFromJson<T extends Enum>(List<T> values, Object? raw, T fallback) {
  if (raw is int && raw >= 0 && raw < values.length) return values[raw];
  if (raw is String) {
    for (final v in values) {
      if (v.name.toLowerCase() == raw.toLowerCase()) return v;
    }
  }
  return fallback;
}

T? enumFromJsonOrNull<T extends Enum>(List<T> values, Object? raw) {
  if (raw == null) return null;
  if (raw is int && raw >= 0 && raw < values.length) return values[raw];
  if (raw is String) {
    for (final v in values) {
      if (v.name.toLowerCase() == raw.toLowerCase()) return v;
    }
  }
  return null;
}
