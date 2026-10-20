using RestaurantManagement.Modules.Identity.Entities;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// Builds memberships for seeding. A membership is stored with
    /// <c>context.Set&lt;OrganizationMember&gt;().Add(...)</c> next to the user.
    /// </summary>
    public static class TestMembers
    {
        private static readonly DateTime CreationTime = new(2026, 10, 20, 18, 30, 0, DateTimeKind.Utc);

        public static OrganizationMember Owner(Guid userId, Guid organizationId) => Build(userId, organizationId, isOwner: true);

        /// <summary>A member who is not the Owner (access comes only from stored levels).</summary>
        public static OrganizationMember Member(Guid userId, Guid organizationId) => Build(userId, organizationId, isOwner: false);

        private static OrganizationMember Build(Guid userId, Guid organizationId, bool isOwner) => new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrganizationId = organizationId,
            IsOwner = isOwner,
            CreatedAt = CreationTime,
            UpdatedAt = CreationTime
        };
    }
}
