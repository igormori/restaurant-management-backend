namespace RestaurantManagement.Modules.Identity.Models.Sessions
{
    // Stub (tester): what the refresh token service hands back after issuing or rotating (task T2).
    public class IssuedRefreshToken
    {
        public string Token { get; init; } = string.Empty;
        public DateTime ExpiresAt { get; init; }
        public Guid UserId { get; init; }
        public Guid? OrganizationId { get; init; }
        public Guid FamilyId { get; init; }
    }
}
