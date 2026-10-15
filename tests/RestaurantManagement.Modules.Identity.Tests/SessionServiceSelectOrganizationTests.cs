using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using NSubstitute;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Modules.Identity.Services;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Options;
using RestaurantManagement.Shared.Services.Organization;
using RestaurantManagement.Shared.Utils.Exceptions;
using Sentry;
using Shouldly;

namespace RestaurantManagement.Modules.Identity.Tests
{
    public class SessionServiceSelectOrganizationTests
    {
        private const string Password = "Password1!";
        private const string UserEmail = "maria@cantinadanonna.com.br";
        private const string OtherUserEmail = "paolo@trattoriadelsole.com.br";
        private const string OwnerRole = "Owner";
        private const string AdminRole = "Admin";
        private const string StaffRole = "Staff";
        private const string CantinaName = "Cantina da Nonna";
        private const string TrattoriaName = "Trattoria do Sol";
        private const string NotMemberKey = "UserNotMemberOfOrganization";
        private const string OrganizationRequiredKey = "OrganizationRequired";
        private const int ForbiddenStatusCode = 403;
        private const int UnauthorizedStatusCode = 401;
        private const int BadRequestStatusCode = 400;

        private static readonly Guid CantinaId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
        private static readonly Guid TrattoriaId = Guid.Parse("b2b2b2b2-0000-4000-8000-000000000002");
        private static readonly Guid DeletedOrganizationId = Guid.Parse("c3c3c3c3-0000-4000-8000-000000000003");

        private static OrganizationSummaryDto Cantina(bool isActive = true) =>
            new() { Id = CantinaId, Name = CantinaName, IsActive = isActive };

        private static OrganizationSummaryDto Trattoria(bool isActive = true) =>
            new() { Id = TrattoriaId, Name = TrattoriaName, IsActive = isActive };

        // Behaves like the real lookup: returns only the requested organizations that exist.
        private static IOrganizationLookup CreateLookup(params OrganizationSummaryDto[] existing)
        {
            var lookup = Substitute.For<IOrganizationLookup>();
            lookup.GetOrganizationsAsync(Arg.Any<IReadOnlyCollection<Guid>>())
                .Returns(call => existing.Where(o => call.Arg<IReadOnlyCollection<Guid>>().Contains(o.Id)).ToList());
            return lookup;
        }

        private static SessionService CreateSut(IdentityDbContext context, IOrganizationLookup lookup)
        {
            var jwtOptions = Options.Create(new JwtOptions
            {
                Key = "test-signing-key-at-least-32-characters-long",
                Issuer = "RestaurantManagementTests",
                Audience = "RestaurantManagementTests",
                ExpireMinutes = 60,
                RefreshTokenExpireDays = 30
            });

            return new SessionService(context, TestSecurityOptions.Create(), jwtOptions, TestLocalizer.Create(), Substitute.For<IHub>(), lookup);
        }

        private static async Task<Guid> SeedUserAsync(
            IdentityTestDatabase database,
            UserRole[] roles,
            string email = UserEmail,
            bool isActive = true,
            bool isVerified = true,
            DateTime? lockedUntil = null)
        {
            using var context = database.CreateContext();
            var user = new User
            {
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                FirstName = "Maria",
                LastName = "Souza",
                IsActive = isActive,
                IsVerified = isVerified,
                LockedUntil = lockedUntil
            };
            foreach (var role in roles)
                user.Roles.Add(role);

            context.Users.Add(user);
            await context.SaveChangesAsync();
            return user.Id;
        }

        private static UserRole[] RolesInBothOrganizations() => new[]
        {
            new UserRole { Role = OwnerRole, OrganizationId = CantinaId },
            new UserRole { Role = StaffRole, OrganizationId = TrattoriaId }
        };

        private static async Task<AuthResponse> LoginAsync(IdentityTestDatabase database, IOrganizationLookup lookup)
        {
            using var context = database.CreateContext();
            return await CreateSut(context, lookup).LoginAsync(new LoginRequest { Email = UserEmail, Password = Password });
        }

        private static async Task<AuthResponse> SelectAsync(IdentityTestDatabase database, IOrganizationLookup lookup, Guid userId, Guid organizationId)
        {
            using var context = database.CreateContext();
            return await CreateSut(context, lookup).SelectOrganizationAsync(userId, new SelectOrganizationRequest { OrganizationId = organizationId });
        }

        private static async Task<AuthResponse> RefreshAsync(IdentityTestDatabase database, IOrganizationLookup lookup, string refreshToken)
        {
            using var context = database.CreateContext();
            return await CreateSut(context, lookup).RefreshTokenAsync(new RefreshRequest { Email = UserEmail, RefreshToken = refreshToken });
        }

        private static string? OrganizationClaimOf(string? token)
        {
            return new JwtSecurityTokenHandler().ReadJwtToken(token).Claims
                .SingleOrDefault(c => c.Type == AppClaimTypes.OrganizationId)?.Value;
        }

        private static List<string> RoleClaimsOf(string? token)
        {
            return new JwtSecurityTokenHandler().ReadJwtToken(token).Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList();
        }

        // Scenario 1
        [Fact]
        public async Task SelectOrganizationAsync_EmptyOrganizationId_ThrowsOrganizationRequiredWith400()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var userId = await SeedUserAsync(database, RolesInBothOrganizations());
            var lookup = CreateLookup(Cantina(), Trattoria());

            // Act
            var act = () => SelectAsync(database, lookup, userId, Guid.Empty);

            // Assert
            var thrown = await Should.ThrowAsync<BusinessException>(act);
            thrown.StatusCode.ShouldBe(BadRequestStatusCode);
            thrown.Message.ShouldBe(OrganizationRequiredKey);
        }

        // Scenario 2
        [Fact]
        public async Task SelectOrganizationAsync_UserWithNoRoleInOrganization_ThrowsNotMemberWith403()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var userId = await SeedUserAsync(database, new[] { new UserRole { Role = OwnerRole, OrganizationId = CantinaId } });
            var lookup = CreateLookup(Cantina(), Trattoria());

            // Act
            var act = () => SelectAsync(database, lookup, userId, TrattoriaId);

            // Assert
            var thrown = await Should.ThrowAsync<BusinessException>(act);
            thrown.StatusCode.ShouldBe(ForbiddenStatusCode);
            thrown.Message.ShouldBe(NotMemberKey);
        }

        // Scenario 3
        [Fact]
        public async Task SelectOrganizationAsync_OrganizationDoesNotExist_ThrowsSameRejectionAsNonMember()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var userId = await SeedUserAsync(database, new[] { new UserRole { Role = OwnerRole, OrganizationId = CantinaId } });
            var lookup = CreateLookup(Cantina());

            // Act
            var act = () => SelectAsync(database, lookup, userId, DeletedOrganizationId);

            // Assert
            var thrown = await Should.ThrowAsync<BusinessException>(act);
            thrown.StatusCode.ShouldBe(ForbiddenStatusCode);
            thrown.Message.ShouldBe(NotMemberKey);
        }

        // Scenario 4
        [Fact]
        public async Task SelectOrganizationAsync_UserHasRoleButOrganizationNoLongerExists_ThrowsSameRejectionAsNonMember()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var userId = await SeedUserAsync(database, new[]
            {
                new UserRole { Role = OwnerRole, OrganizationId = CantinaId },
                new UserRole { Role = StaffRole, OrganizationId = DeletedOrganizationId }
            });
            var lookup = CreateLookup(Cantina());

            // Act
            var act = () => SelectAsync(database, lookup, userId, DeletedOrganizationId);

            // Assert
            var thrown = await Should.ThrowAsync<BusinessException>(act);
            thrown.StatusCode.ShouldBe(ForbiddenStatusCode);
            thrown.Message.ShouldBe(NotMemberKey);
        }

        // Scenario 5
        [Fact]
        public async Task SelectOrganizationAsync_MemberOfInactiveOrganization_ThrowsSameRejectionAsNonMember()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var userId = await SeedUserAsync(database, RolesInBothOrganizations());
            var lookup = CreateLookup(Cantina(), Trattoria(isActive: false));

            // Act
            var act = () => SelectAsync(database, lookup, userId, TrattoriaId);

            // Assert
            var thrown = await Should.ThrowAsync<BusinessException>(act);
            thrown.StatusCode.ShouldBe(ForbiddenStatusCode);
            thrown.Message.ShouldBe(NotMemberKey);
        }

        // Scenario 6
        [Fact]
        public async Task SelectOrganizationAsync_OrganizationBelongsToAnotherUser_ThrowsNotMemberWith403()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var mariaId = await SeedUserAsync(database, new[] { new UserRole { Role = OwnerRole, OrganizationId = CantinaId } });
            await SeedUserAsync(database, new[] { new UserRole { Role = OwnerRole, OrganizationId = TrattoriaId } }, email: OtherUserEmail);
            var lookup = CreateLookup(Cantina(), Trattoria());

            // Act
            var act = () => SelectAsync(database, lookup, mariaId, TrattoriaId);

            // Assert
            var thrown = await Should.ThrowAsync<BusinessException>(act);
            thrown.StatusCode.ShouldBe(ForbiddenStatusCode);
            thrown.Message.ShouldBe(NotMemberKey);
        }

        // Scenario 7
        [Fact]
        public async Task SelectOrganizationAsync_UserIdDoesNotExist_ThrowsUnauthorized()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            await SeedUserAsync(database, RolesInBothOrganizations());
            var lookup = CreateLookup(Cantina(), Trattoria());

            // Act
            var act = () => SelectAsync(database, lookup, Guid.NewGuid(), CantinaId);

            // Assert
            var thrown = await Should.ThrowAsync<BusinessException>(act);
            thrown.StatusCode.ShouldBe(UnauthorizedStatusCode);
        }

        // Scenario 8
        [Fact]
        public async Task SelectOrganizationAsync_DeactivatedUser_ThrowsUnauthorized()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var userId = await SeedUserAsync(database, RolesInBothOrganizations(), isActive: false);
            var lookup = CreateLookup(Cantina(), Trattoria());

            // Act
            var act = () => SelectAsync(database, lookup, userId, CantinaId);

            // Assert
            var thrown = await Should.ThrowAsync<BusinessException>(act);
            thrown.StatusCode.ShouldBe(UnauthorizedStatusCode);
        }

        // Scenario 9
        [Fact]
        public async Task SelectOrganizationAsync_UnverifiedUser_ThrowsUnauthorized()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var userId = await SeedUserAsync(database, RolesInBothOrganizations(), isVerified: false);
            var lookup = CreateLookup(Cantina(), Trattoria());

            // Act
            var act = () => SelectAsync(database, lookup, userId, CantinaId);

            // Assert
            var thrown = await Should.ThrowAsync<BusinessException>(act);
            thrown.StatusCode.ShouldBe(UnauthorizedStatusCode);
        }

        // Scenario 10
        [Fact]
        public async Task SelectOrganizationAsync_LockedUser_ThrowsUnauthorized()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var userId = await SeedUserAsync(database, RolesInBothOrganizations(), lockedUntil: DateTime.UtcNow.AddMinutes(15));
            var lookup = CreateLookup(Cantina(), Trattoria());

            // Act
            var act = () => SelectAsync(database, lookup, userId, CantinaId);

            // Assert
            var thrown = await Should.ThrowAsync<BusinessException>(act);
            thrown.StatusCode.ShouldBe(UnauthorizedStatusCode);
        }

        // Scenario 11
        [Fact]
        public async Task SelectOrganizationAsync_MemberOrganization_TokenCarriesThatOrganizationId()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var userId = await SeedUserAsync(database, RolesInBothOrganizations());
            var lookup = CreateLookup(Cantina(), Trattoria());

            // Act
            var response = await SelectAsync(database, lookup, userId, TrattoriaId);

            // Assert
            OrganizationClaimOf(response.Token).ShouldBe(TrattoriaId.ToString());
        }

        // Scenario 12
        [Fact]
        public async Task SelectOrganizationAsync_OwnerInCantinaStaffInTrattoria_TokenHasOnlyRolesOfSelectedOrganization()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var userId = await SeedUserAsync(database, RolesInBothOrganizations());
            var lookup = CreateLookup(Cantina(), Trattoria());

            // Act
            var response = await SelectAsync(database, lookup, userId, TrattoriaId);

            // Assert
            RoleClaimsOf(response.Token).ShouldBe(new[] { StaffRole });
        }

        // Scenario 13
        [Fact]
        public async Task SelectOrganizationAsync_TwoRolesInSelectedOrganization_TokenHasBothRolesAndNoneFromOthers()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var userId = await SeedUserAsync(database, new[]
            {
                new UserRole { Role = OwnerRole, OrganizationId = CantinaId },
                new UserRole { Role = AdminRole, OrganizationId = CantinaId },
                new UserRole { Role = StaffRole, OrganizationId = TrattoriaId }
            });
            var lookup = CreateLookup(Cantina(), Trattoria());

            // Act
            var response = await SelectAsync(database, lookup, userId, CantinaId);

            // Assert
            RoleClaimsOf(response.Token).OrderBy(r => r).ToList().ShouldBe(new[] { AdminRole, OwnerRole });
        }

        // Scenario 14
        [Fact]
        public async Task SelectOrganizationAsync_UserWithOnlyOneOrganization_ReissuesTokenForThatOrganization()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var userId = await SeedUserAsync(database, new[] { new UserRole { Role = OwnerRole, OrganizationId = CantinaId } });
            var lookup = CreateLookup(Cantina());

            // Act
            var response = await SelectAsync(database, lookup, userId, CantinaId);

            // Assert
            OrganizationClaimOf(response.Token).ShouldBe(CantinaId.ToString());
        }

        // Scenario 15
        [Fact]
        public async Task SelectOrganizationAsync_MemberOrganization_ReturnsUsableRefreshTokenForSelectedOrganization()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var userId = await SeedUserAsync(database, RolesInBothOrganizations());
            var lookup = CreateLookup(Cantina(), Trattoria());
            var selected = await SelectAsync(database, lookup, userId, CantinaId);

            // Act
            var refreshed = await RefreshAsync(database, lookup, selected.RefreshToken!);

            // Assert
            OrganizationClaimOf(refreshed.Token).ShouldBe(CantinaId.ToString());
        }

        // Scenario 16
        [Fact]
        public async Task SelectOrganizationAsync_AfterLogin_PreviousRefreshTokenIsRejected()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var userId = await SeedUserAsync(database, RolesInBothOrganizations());
            var lookup = CreateLookup(Cantina(), Trattoria());
            var loginResponse = await LoginAsync(database, lookup);
            await SelectAsync(database, lookup, userId, CantinaId);

            // Act
            var act = () => RefreshAsync(database, lookup, loginResponse.RefreshToken!);

            // Assert
            var thrown = await Should.ThrowAsync<BusinessException>(act);
            thrown.StatusCode.ShouldBe(UnauthorizedStatusCode);
        }

        // Scenario 17
        [Fact]
        public async Task SelectOrganizationAsync_SecondOrganization_TokenCarriesSecondOrganizationAndRefreshKeepsIt()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var userId = await SeedUserAsync(database, RolesInBothOrganizations());
            var lookup = CreateLookup(Cantina(), Trattoria());
            await SelectAsync(database, lookup, userId, CantinaId);
            var switched = await SelectAsync(database, lookup, userId, TrattoriaId);

            // Act
            var refreshed = await RefreshAsync(database, lookup, switched.RefreshToken!);

            // Assert
            OrganizationClaimOf(switched.Token).ShouldBe(TrattoriaId.ToString());
            OrganizationClaimOf(refreshed.Token).ShouldBe(TrattoriaId.ToString());
        }

        // Scenario 18
        [Fact]
        public async Task RefreshTokenAsync_AfterSelectingOrganization_KeepsSelectedOrganizationAndItsRolesOnly()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var userId = await SeedUserAsync(database, RolesInBothOrganizations());
            var lookup = CreateLookup(Cantina(), Trattoria());
            var selected = await SelectAsync(database, lookup, userId, TrattoriaId);

            // Act
            var refreshed = await RefreshAsync(database, lookup, selected.RefreshToken!);

            // Assert
            OrganizationClaimOf(refreshed.Token).ShouldBe(TrattoriaId.ToString());
            RoleClaimsOf(refreshed.Token).ShouldBe(new[] { StaffRole });
        }

        // Scenario 19
        [Fact]
        public async Task RefreshTokenAsync_MembershipOfSelectedOrganizationRemoved_ThrowsUnauthorized()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var userId = await SeedUserAsync(database, RolesInBothOrganizations());
            var lookup = CreateLookup(Cantina(), Trattoria());
            var selected = await SelectAsync(database, lookup, userId, TrattoriaId);
            using (var context = database.CreateContext())
            {
                var staffRole = context.UserRoles.Single(r => r.UserId == userId && r.OrganizationId == TrattoriaId);
                context.UserRoles.Remove(staffRole);
                await context.SaveChangesAsync();
            }

            // Act
            var act = () => RefreshAsync(database, lookup, selected.RefreshToken!);

            // Assert
            var thrown = await Should.ThrowAsync<BusinessException>(act);
            thrown.StatusCode.ShouldBe(UnauthorizedStatusCode);
        }

        // Scenario 20
        [Fact]
        public async Task LoginAsync_UserWithTwoOrganizations_ListsBothChoicesWithNamesAndNoOrganizationClaim()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            await SeedUserAsync(database, RolesInBothOrganizations());
            var lookup = CreateLookup(Cantina(), Trattoria());

            // Act
            var response = await LoginAsync(database, lookup);

            // Assert
            OrganizationClaimOf(response.Token).ShouldBeNull();
            response.Organizations.ShouldNotBeNull();
            response.Organizations.Select(o => (o.Id, o.Name)).OrderBy(o => o.Name).ToList()
                .ShouldBe(new[] { (CantinaId, CantinaName), (TrattoriaId, TrattoriaName) });
        }

        // Scenario 21
        [Fact]
        public async Task LoginAsync_OneOfTwoOrganizationsIsInactive_ListsOnlyTheActiveOne()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            await SeedUserAsync(database, RolesInBothOrganizations());
            var lookup = CreateLookup(Cantina(), Trattoria(isActive: false));

            // Act
            var response = await LoginAsync(database, lookup);

            // Assert
            response.Organizations.ShouldNotBeNull();
            response.Organizations.Select(o => o.Id).ShouldBe(new[] { CantinaId });
        }

        // Scenario 22
        [Fact]
        public async Task LoginAsync_OtherUserBelongsToThirdOrganization_ListsOnlyOrganizationsOfTheLoggingInUser()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            await SeedUserAsync(database, RolesInBothOrganizations());
            await SeedUserAsync(database, new[] { new UserRole { Role = OwnerRole, OrganizationId = DeletedOrganizationId } }, email: OtherUserEmail);
            var thirdOrganization = new OrganizationSummaryDto { Id = DeletedOrganizationId, Name = "Pizzaria Vila Madalena", IsActive = true };
            var lookup = CreateLookup(Cantina(), Trattoria(), thirdOrganization);

            // Act
            var response = await LoginAsync(database, lookup);

            // Assert
            response.Organizations.ShouldNotBeNull();
            response.Organizations.Select(o => o.Id).OrderBy(id => id).ToList()
                .ShouldBe(new[] { CantinaId, TrattoriaId }.OrderBy(id => id).ToList());
        }

        // Scenario 23
        [Fact]
        public async Task LoginAsync_UserWithNoOrganizations_HasNoOrganizationClaimAndNoChoices()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            await SeedUserAsync(database, Array.Empty<UserRole>());
            var lookup = CreateLookup(Cantina(), Trattoria());

            // Act
            var response = await LoginAsync(database, lookup);

            // Assert
            OrganizationClaimOf(response.Token).ShouldBeNull();
            response.Organizations.ShouldNotBeNull();
            response.Organizations.ShouldBeEmpty();
        }

        // Scenario 24
        [Fact]
        public async Task LoginAsync_AfterSelectingOrganization_TokenHasNoOrganizationClaimAgain()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var userId = await SeedUserAsync(database, RolesInBothOrganizations());
            var lookup = CreateLookup(Cantina(), Trattoria());
            await SelectAsync(database, lookup, userId, CantinaId);

            // Act
            var response = await LoginAsync(database, lookup);

            // Assert
            OrganizationClaimOf(response.Token).ShouldBeNull();
        }

        // Scenario 25
        [Fact]
        public async Task RefreshTokenAsync_AfterNewLoginFollowingSelection_DoesNotRestorePreviousOrganization()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var userId = await SeedUserAsync(database, RolesInBothOrganizations());
            var lookup = CreateLookup(Cantina(), Trattoria());
            await SelectAsync(database, lookup, userId, CantinaId);
            var loginResponse = await LoginAsync(database, lookup);

            // Act
            var refreshed = await RefreshAsync(database, lookup, loginResponse.RefreshToken!);

            // Assert
            OrganizationClaimOf(refreshed.Token).ShouldBeNull();
        }
    }
}
