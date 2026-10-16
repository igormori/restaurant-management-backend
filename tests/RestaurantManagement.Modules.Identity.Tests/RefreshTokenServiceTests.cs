using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Modules.Identity.Services;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// Lifecycle of per-device refresh tokens: issue, rotate, reuse detection and revocation.
    /// Each service call uses a fresh context so nothing is read back from the change tracker.
    /// </summary>
    public class RefreshTokenServiceTests
    {
        private const string TabletDevice = "Mozilla/5.0 (iPad; CPU OS 17_4 like Mac OS X) Safari/604.1";
        private const string TabletIp = "203.0.113.10";
        private const string LaptopDevice = "Mozilla/5.0 (Macintosh; Intel Mac OS X 14_4) Chrome/124.0";
        private const string LaptopIp = "198.51.100.24";

        private static readonly Guid MariaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
        private static readonly Guid JoaoId = Guid.Parse("22222222-bbbb-4bbb-8bbb-00000000000b");
        private static readonly Guid CantinaOrganizationId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
        private static readonly Guid TrattoriaOrganizationId = Guid.Parse("b2b2b2b2-0000-4000-8000-000000000002");

        private static async Task<IdentityTestDatabase> CreateDatabaseWithUsersAsync()
        {
            var database = new IdentityTestDatabase();
            using var context = database.CreateContext();
            context.Users.AddRange(
                new User { Id = MariaId, Email = "maria@cantinadanonna.com.br", PasswordHash = "hash", FirstName = "Maria", LastName = "Souza", IsVerified = true },
                new User { Id = JoaoId, Email = "joao@trattoriadoze.com.br", PasswordHash = "hash", FirstName = "João", LastName = "Lima", IsVerified = true });
            await context.SaveChangesAsync();
            return database;
        }

        private static async Task<IssuedRefreshToken> IssueAsync(
            IdentityTestDatabase database,
            Guid userId,
            Guid? organizationId = null,
            string? deviceDescription = TabletDevice,
            string? ipAddress = TabletIp)
        {
            using var context = database.CreateContext();
            return await new RefreshTokenService(context, TestJwtOptions.Create())
                .IssueAsync(userId, organizationId, deviceDescription, ipAddress);
        }

        private static async Task<IssuedRefreshToken?> RotateAsync(IdentityTestDatabase database, string rawToken, Guid? organizationId = null, Guid? expectedUserId = null)
        {
            using var context = database.CreateContext();
            return await new RefreshTokenService(context, TestJwtOptions.Create()).RotateAsync(rawToken, organizationId, expectedUserId);
        }

        private static async Task<bool> RevokeAsync(IdentityTestDatabase database, string rawToken)
        {
            using var context = database.CreateContext();
            return await new RefreshTokenService(context, TestJwtOptions.Create()).RevokeAsync(rawToken);
        }

        private static async Task<int> RevokeAllAsync(IdentityTestDatabase database, Guid userId)
        {
            using var context = database.CreateContext();
            return await new RefreshTokenService(context, TestJwtOptions.Create()).RevokeAllForUserAsync(userId);
        }

        private static async Task<List<RefreshToken>> ReadTokensAsync(IdentityTestDatabase database)
        {
            using var context = database.CreateContext();
            return await context.Set<RefreshToken>().AsNoTracking().OrderBy(t => t.CreatedAt).ToListAsync();
        }

        private static async Task<RefreshToken> ReadFamilyMemberAsync(IdentityTestDatabase database, Guid familyId, bool active)
        {
            using var context = database.CreateContext();
            return await context.Set<RefreshToken>().AsNoTracking()
                .SingleAsync(t => t.FamilyId == familyId && (t.RevokedAt == null) == active);
        }

        private static async Task ExpireAllTokensAsync(IdentityTestDatabase database)
        {
            using var context = database.CreateContext();
            var tokens = await context.Set<RefreshToken>().ToListAsync();
            tokens.ForEach(t => t.ExpiresAt = DateTime.UtcNow.AddMinutes(-1));
            await context.SaveChangesAsync();
        }

        // Scenario 9
        [Fact]
        [Trait("Rule", "F2")]
        public async Task RotateAsync_UnknownToken_ReturnsNullAndChangesNothing()
        {
            // Arrange
            using var database = await CreateDatabaseWithUsersAsync();
            await IssueAsync(database, MariaId);

            // Act
            var rotated = await RotateAsync(database, "bm90LWEtcmVhbC10b2tlbi1mb3ItY2FudGluYQ==");

            // Assert
            rotated.ShouldBeNull();
            (await ReadTokensAsync(database)).ShouldHaveSingleItem().RevokedAt.ShouldBeNull();
        }

        // Scenario 10
        [Fact]
        [Trait("Rule", "F2")]
        public async Task RotateAsync_ExpiredToken_ReturnsNullWithoutRevokingIt()
        {
            // Arrange
            using var database = await CreateDatabaseWithUsersAsync();
            var issued = await IssueAsync(database, MariaId);
            await ExpireAllTokensAsync(database);

            // Act
            var rotated = await RotateAsync(database, issued.Token);

            // Assert
            rotated.ShouldBeNull();
            var token = (await ReadTokensAsync(database)).ShouldHaveSingleItem();
            token.RevokedAt.ShouldBeNull();
        }

        // Scenario 11
        [Fact]
        [Trait("Rule", "F3")]
        public async Task RotateAsync_AlreadyRotatedToken_ReturnsNull()
        {
            // Arrange
            using var database = await CreateDatabaseWithUsersAsync();
            var issued = await IssueAsync(database, MariaId);
            await RotateAsync(database, issued.Token);

            // Act
            var reused = await RotateAsync(database, issued.Token);

            // Assert
            reused.ShouldBeNull();
        }

        // Scenario 12
        [Fact]
        [Trait("Rule", "F3")]
        public async Task RotateAsync_AlreadyRotatedToken_RevokesWholeFamilyAsReuseDetected()
        {
            // Arrange
            using var database = await CreateDatabaseWithUsersAsync();
            var issued = await IssueAsync(database, MariaId);
            await RotateAsync(database, issued.Token);

            // Act
            await RotateAsync(database, issued.Token);

            // Assert
            var family = (await ReadTokensAsync(database)).Where(t => t.FamilyId == issued.FamilyId).ToList();
            family.Count.ShouldBe(2);
            family.ShouldAllBe(t => t.RevokedAt != null);
            family.ShouldContain(t => t.RevocationReason == RefreshToken.ReasonReuseDetected);
        }

        // Scenario 13
        [Fact]
        [Trait("Rule", "F3")]
        public async Task RotateAsync_ReuseInOneFamily_OtherDeviceOfSameUserStaysActive()
        {
            // Arrange
            using var database = await CreateDatabaseWithUsersAsync();
            var tablet = await IssueAsync(database, MariaId);
            var laptop = await IssueAsync(database, MariaId, deviceDescription: LaptopDevice, ipAddress: LaptopIp);
            await RotateAsync(database, tablet.Token);

            // Act
            await RotateAsync(database, tablet.Token);

            // Assert
            var laptopToken = await ReadFamilyMemberAsync(database, laptop.FamilyId, active: true);
            laptopToken.RevokedAt.ShouldBeNull();
        }

        // Scenario 14
        [Fact]
        [Trait("Rule", "O1")]
        public async Task RevokeAsync_UnknownToken_ReturnsFalseAndChangesNothing()
        {
            // Arrange
            using var database = await CreateDatabaseWithUsersAsync();
            await IssueAsync(database, MariaId);

            // Act
            var revoked = await RevokeAsync(database, "bm90LWEtcmVhbC10b2tlbi1mb3ItY2FudGluYQ==");

            // Assert
            revoked.ShouldBeFalse();
            (await ReadTokensAsync(database)).ShouldHaveSingleItem().RevokedAt.ShouldBeNull();
        }

        // Scenario 15
        [Fact]
        [Trait("Rule", "L15")]
        public async Task IssueAsync_NewSignIn_StoresOneActiveTokenWithDeviceIpAndExpiry()
        {
            // Arrange
            using var database = await CreateDatabaseWithUsersAsync();

            // Act
            var issued = await IssueAsync(database, MariaId);

            // Assert
            var token = (await ReadTokensAsync(database)).ShouldHaveSingleItem();
            token.UserId.ShouldBe(MariaId);
            token.OrganizationId.ShouldBeNull();
            token.FamilyId.ShouldBe(issued.FamilyId);
            token.RevokedAt.ShouldBeNull();
            token.DeviceDescription.ShouldBe(TabletDevice);
            token.IpAddress.ShouldBe(TabletIp);
            token.ExpiresAt.ShouldBe(DateTime.UtcNow.AddDays(TestJwtOptions.RefreshTokenExpireDays), TimeSpan.FromMinutes(1));
            issued.ExpiresAt.ShouldBe(token.ExpiresAt, TimeSpan.FromSeconds(1));
        }

        // Scenario 16
        [Fact]
        [Trait("Rule", "T8")]
        public async Task IssueAsync_NewSignIn_StoredFingerprintDiffersFromIssuedToken()
        {
            // Arrange
            using var database = await CreateDatabaseWithUsersAsync();

            // Act
            var issued = await IssueAsync(database, MariaId);

            // Assert
            issued.Token.ShouldNotBeNullOrWhiteSpace();
            var token = (await ReadTokensAsync(database)).ShouldHaveSingleItem();
            token.TokenHash.ShouldNotBeNullOrWhiteSpace();
            token.TokenHash.ShouldNotBe(issued.Token);
        }

        // Scenario 17
        [Fact]
        [Trait("Rule", "L15")]
        public async Task IssueAsync_SameUserOnTwoDevices_StartsTwoActiveFamilies()
        {
            // Arrange
            using var database = await CreateDatabaseWithUsersAsync();

            // Act
            var tablet = await IssueAsync(database, MariaId);
            var laptop = await IssueAsync(database, MariaId, deviceDescription: LaptopDevice, ipAddress: LaptopIp);

            // Assert
            tablet.FamilyId.ShouldNotBe(laptop.FamilyId);
            tablet.Token.ShouldNotBe(laptop.Token);
            (await ReadTokensAsync(database)).Count(t => t.RevokedAt == null).ShouldBe(2);
        }

        // Scenario 18
        [Fact]
        [Trait("Rule", "L15")]
        public async Task IssueAsync_DeviceDescriptionLongerThan512Characters_StoresFirst512()
        {
            // Arrange
            using var database = await CreateDatabaseWithUsersAsync();
            var longUserAgent = new string('A', 600);

            // Act
            await IssueAsync(database, MariaId, deviceDescription: longUserAgent);

            // Assert
            var token = (await ReadTokensAsync(database)).ShouldHaveSingleItem();
            token.DeviceDescription.ShouldBe(new string('A', 512));
        }

        // Scenario 19
        [Fact]
        [Trait("Rule", "F1")]
        public async Task RotateAsync_ActiveToken_OldTokenRevokedAsRotatedAndPointsToReplacement()
        {
            // Arrange
            using var database = await CreateDatabaseWithUsersAsync();
            var issued = await IssueAsync(database, MariaId);

            // Act
            var rotated = await RotateAsync(database, issued.Token);

            // Assert
            rotated.ShouldNotBeNull();
            var oldToken = await ReadFamilyMemberAsync(database, issued.FamilyId, active: false);
            var newToken = await ReadFamilyMemberAsync(database, issued.FamilyId, active: true);
            oldToken.RevocationReason.ShouldBe(RefreshToken.ReasonRotated);
            oldToken.ReplacedByTokenId.ShouldBe(newToken.Id);
        }

        // Scenario 20
        [Fact]
        [Trait("Rule", "F1")]
        public async Task RotateAsync_ActiveToken_ReturnsNewTokenActiveInSameFamilyWithRenewedExpiry()
        {
            // Arrange
            using var database = await CreateDatabaseWithUsersAsync();
            var issued = await IssueAsync(database, MariaId);

            // Act
            var rotated = await RotateAsync(database, issued.Token);

            // Assert
            rotated.ShouldNotBeNull();
            rotated.Token.ShouldNotBe(issued.Token);
            rotated.FamilyId.ShouldBe(issued.FamilyId);
            rotated.UserId.ShouldBe(MariaId);
            rotated.ExpiresAt.ShouldBe(DateTime.UtcNow.AddDays(TestJwtOptions.RefreshTokenExpireDays), TimeSpan.FromMinutes(1));
            var newToken = await ReadFamilyMemberAsync(database, issued.FamilyId, active: true);
            newToken.TokenHash.ShouldNotBe(rotated.Token);
        }

        // Scenario 21
        [Fact]
        [Trait("Rule", "S7")]
        public async Task RotateAsync_WithChosenOrganization_ReplacementCarriesThatOrganization()
        {
            // Arrange
            using var database = await CreateDatabaseWithUsersAsync();
            var issued = await IssueAsync(database, MariaId);

            // Act
            var rotated = await RotateAsync(database, issued.Token, CantinaOrganizationId);

            // Assert
            rotated.ShouldNotBeNull();
            rotated.OrganizationId.ShouldBe(CantinaOrganizationId);
            (await ReadFamilyMemberAsync(database, issued.FamilyId, active: true)).OrganizationId.ShouldBe(CantinaOrganizationId);
        }

        // Scenario 22
        [Fact]
        [Trait("Rule", "F6")]
        public async Task RotateAsync_WithoutOrganization_ReplacementKeepsTokenOrganization()
        {
            // Arrange
            using var database = await CreateDatabaseWithUsersAsync();
            var issued = await IssueAsync(database, MariaId, organizationId: CantinaOrganizationId);

            // Act
            var rotated = await RotateAsync(database, issued.Token);

            // Assert
            rotated.ShouldNotBeNull();
            rotated.OrganizationId.ShouldBe(CantinaOrganizationId);
        }

        // Scenario 23
        [Fact]
        [Trait("Rule", "F7")]
        public async Task RotateAsync_TwoDevicesOfOneUser_BothRotateIndependently()
        {
            // Arrange
            using var database = await CreateDatabaseWithUsersAsync();
            var tablet = await IssueAsync(database, MariaId);
            var laptop = await IssueAsync(database, MariaId, deviceDescription: LaptopDevice, ipAddress: LaptopIp);

            // Act
            var rotatedTablet = await RotateAsync(database, tablet.Token);
            var rotatedLaptop = await RotateAsync(database, laptop.Token);

            // Assert
            rotatedTablet.ShouldNotBeNull();
            rotatedLaptop.ShouldNotBeNull();
            rotatedTablet.FamilyId.ShouldBe(tablet.FamilyId);
            rotatedLaptop.FamilyId.ShouldBe(laptop.FamilyId);
        }

        // Scenario 24
        [Fact]
        [Trait("Rule", "O2")]
        public async Task RevokeAsync_OneOfTwoDevices_OnlyThatTokenRevokedAsLogout()
        {
            // Arrange
            using var database = await CreateDatabaseWithUsersAsync();
            var tablet = await IssueAsync(database, MariaId);
            var laptop = await IssueAsync(database, MariaId, deviceDescription: LaptopDevice, ipAddress: LaptopIp);

            // Act
            var revoked = await RevokeAsync(database, tablet.Token);

            // Assert
            revoked.ShouldBeTrue();
            (await ReadFamilyMemberAsync(database, tablet.FamilyId, active: false)).RevocationReason.ShouldBe(RefreshToken.ReasonLogout);
            (await ReadFamilyMemberAsync(database, laptop.FamilyId, active: true)).RevokedAt.ShouldBeNull();
        }

        // Scenario 25
        [Fact]
        [Trait("Rule", "O5")]
        public async Task RevokeAllForUserAsync_UserOnTwoDevices_NoActiveTokenLeftAndReasonLogoutEverywhere()
        {
            // Arrange
            using var database = await CreateDatabaseWithUsersAsync();
            await IssueAsync(database, MariaId, organizationId: CantinaOrganizationId);
            await IssueAsync(database, MariaId, deviceDescription: LaptopDevice, ipAddress: LaptopIp);

            // Act
            var revokedCount = await RevokeAllAsync(database, MariaId);

            // Assert
            revokedCount.ShouldBe(2);
            var tokens = await ReadTokensAsync(database);
            tokens.ShouldAllBe(t => t.RevokedAt != null && t.RevocationReason == RefreshToken.ReasonLogoutEverywhere);
        }

        // Scenario 26
        [Fact]
        [Trait("Rule", "O5")]
        public async Task RevokeAllForUserAsync_AnotherUserInAnotherOrganization_KeepsActiveToken()
        {
            // Arrange
            using var database = await CreateDatabaseWithUsersAsync();
            await IssueAsync(database, MariaId, organizationId: CantinaOrganizationId);
            var joao = await IssueAsync(database, JoaoId, organizationId: TrattoriaOrganizationId, deviceDescription: LaptopDevice, ipAddress: LaptopIp);

            // Act
            await RevokeAllAsync(database, MariaId);

            // Assert
            var joaoToken = await ReadFamilyMemberAsync(database, joao.FamilyId, active: true);
            joaoToken.UserId.ShouldBe(JoaoId);
        }

        // Scenario 75
        [Fact]
        [Trait("Rule", "O3")]
        public async Task RevokeAsync_AlreadyRotatedToken_RevokesWholeFamilyAsReuseDetected()
        {
            // Arrange
            using var database = await CreateDatabaseWithUsersAsync();
            var issued = await IssueAsync(database, MariaId);
            await RotateAsync(database, issued.Token);

            // Act
            var revoked = await RevokeAsync(database, issued.Token);

            // Assert
            revoked.ShouldBeFalse();
            var family = (await ReadTokensAsync(database)).Where(t => t.FamilyId == issued.FamilyId).ToList();
            family.Count.ShouldBe(2);
            family.ShouldAllBe(t => t.RevokedAt != null);
            family.ShouldContain(t => t.RevocationReason == RefreshToken.ReasonReuseDetected);
        }

        // Scenario 78
        [Fact]
        [Trait("Rule", "F9")]
        public async Task RotateAsync_TokenOfAnotherUser_ReturnsNullAndLeavesItActiveAndUnreplaced()
        {
            // Arrange
            using var database = await CreateDatabaseWithUsersAsync();
            var joao = await IssueAsync(database, JoaoId, organizationId: TrattoriaOrganizationId);

            // Act
            var rotated = await RotateAsync(database, joao.Token, CantinaOrganizationId, expectedUserId: MariaId);

            // Assert
            rotated.ShouldBeNull();
            var token = (await ReadTokensAsync(database)).ShouldHaveSingleItem();
            token.RevokedAt.ShouldBeNull();
            token.ReplacedByTokenId.ShouldBeNull();
            token.OrganizationId.ShouldBe(TrattoriaOrganizationId);
        }
    }
}
