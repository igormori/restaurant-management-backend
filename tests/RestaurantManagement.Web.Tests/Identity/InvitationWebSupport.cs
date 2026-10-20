using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Services;
using RestaurantManagement.Modules.Organization.Entities;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Data;
using RestaurantManagement.Shared.Services.Email;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// The world of the member access and invitation endpoint tests, on a real PostgreSQL database.
/// Cantina da Nonna (Batel, Mercado Municipal) has Ana the Owner, Bia a Waiter at Batel, Maria an Admin
/// with Members = Manage at Batel only, and Rita, who has an account and belongs to Trattoria do Sol.
/// Trattoria do Sol (Centro) has Tereza the Owner and Paolo a member. Both organizations have the five
/// default roles; Trattoria also has a role Sommelier that Cantina does not.
/// </summary>
internal static class InvitationWebSupport
{
    public static readonly Guid CantinaId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
    public static readonly Guid TrattoriaId = Guid.Parse("b2b2b2b2-0000-4000-8000-000000000002");
    public static readonly Guid BatelId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000001");
    public static readonly Guid MercadoId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000002");
    public static readonly Guid CentroId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000003");
    public static readonly Guid AnaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
    public static readonly Guid BiaId = Guid.Parse("22222222-bbbb-4bbb-8bbb-00000000000b");
    public static readonly Guid MariaId = Guid.Parse("66666666-ffff-4fff-8fff-00000000000f");
    public static readonly Guid RitaId = Guid.Parse("44444444-dddd-4ddd-8ddd-00000000000d");
    public static readonly Guid TerezaId = Guid.Parse("77777777-0000-4000-8000-0000000000e1");
    public static readonly Guid PaoloId = Guid.Parse("33333333-cccc-4ccc-8ccc-00000000000c");
    public const string RitaEmail = "rita@exemplo.com.br";
    public const string BrunoEmail = "bruno@exemplo.com.br";

    public static (AccessModule Module, AccessLevel Level)[] WaiterLevels => new[]
    {
        (AccessModule.Locations, AccessLevel.Read), (AccessModule.Menus, AccessLevel.Read), (AccessModule.Orders, AccessLevel.Manage)
    };

    public static (AccessModule Module, AccessLevel Level)[] CashierLevels => new[]
    {
        (AccessModule.Locations, AccessLevel.Read), (AccessModule.Menus, AccessLevel.Read),
        (AccessModule.Cashier, AccessLevel.Manage), (AccessModule.Orders, AccessLevel.Read)
    };

    public static async Task<TenantApiFactory> CreateFactoryAsync(PostgresDatabaseFixture postgres)
    {
        var factory = await TenantApiFactory.CreateAsync(postgres);
        await factory.SeedOrganizationAsync(CantinaId, "Cantina da Nonna", Currency.BRL);
        await factory.SeedOrganizationAsync(TrattoriaId, "Trattoria do Sol", Currency.BRL);
        await factory.SeedLocationAsync(CantinaId, BatelId, "Batel", "BATEL", "America/Sao_Paulo");
        await factory.SeedLocationAsync(CantinaId, MercadoId, "Mercado Municipal", "MERCADO", "America/Sao_Paulo");
        await factory.SeedLocationAsync(TrattoriaId, CentroId, "Centro", "CENTRO", "America/Sao_Paulo");

        await factory.SeedUserAsync(AnaId, "ana@cantinadanonna.com.br", "Ana");
        await factory.SeedUserAsync(TerezaId, "tereza@trattoriadosol.com.br", "Tereza");
        await SetUpOwnerWithRolesAsync(factory, CantinaId, AnaId);
        await SetUpOwnerWithRolesAsync(factory, TrattoriaId, TerezaId);
        await AddRoleAsync(factory, TrattoriaId, "Sommelier");

        await factory.SeedUserAsync(BiaId, "bia@cantinadanonna.com.br", "Bia", TenantApiFactory.MemberOf(CantinaId));
        await factory.GrantAsync(BiaId, CantinaId, BatelId, WaiterLevels);
        await LabelAsync(factory, CantinaId, BiaId, BatelId, "Waiter");

        await factory.SeedUserAsync(MariaId, "maria@cantinadanonna.com.br", "Maria", TenantApiFactory.MemberOf(CantinaId));
        await factory.GrantAsync(MariaId, CantinaId, BatelId,
            (AccessModule.Locations, AccessLevel.Manage), (AccessModule.Menus, AccessLevel.Manage), (AccessModule.Orders, AccessLevel.Manage),
            (AccessModule.Members, AccessLevel.Manage));

        await factory.SeedUserAsync(RitaId, RitaEmail, "Rita", TenantApiFactory.MemberOf(TrattoriaId));
        await factory.SeedUserAsync(PaoloId, "paolo@trattoriadosol.com.br", "Paolo", TenantApiFactory.MemberOf(TrattoriaId));
        await factory.GrantAsync(PaoloId, TrattoriaId, CentroId, (AccessModule.Menus, AccessLevel.Read));
        return factory;
    }

    private static Task SetUpOwnerWithRolesAsync(TenantApiFactory factory, Guid organizationId, Guid ownerId)
        => factory.InOrganizationAsync<IdentityDbContext, bool>(organizationId, async db =>
        {
            await new OrganizationAccessSetup(db).AddOwnerWithDefaultRolesAsync(ownerId, organizationId);
            return true;
        });

    private static Task AddRoleAsync(TenantApiFactory factory, Guid organizationId, string name)
        => factory.InOrganizationAsync<IdentityDbContext, bool>(organizationId, async db =>
        {
            db.Set<OrganizationRole>().Add(new OrganizationRole { OrganizationId = organizationId, Name = name });
            await db.SaveChangesAsync();
            return true;
        });

    /// <summary>Stores the role label shown next to a member's levels at a location.</summary>
    public static Task LabelAsync(TenantApiFactory factory, Guid organizationId, Guid userId, Guid locationId, string roleName)
        => factory.InOrganizationAsync<IdentityDbContext, bool>(organizationId, async db =>
        {
            var role = await db.Set<OrganizationRole>().SingleAsync(r => r.OrganizationId == organizationId && r.Name == roleName);
            db.Set<MemberLocationRole>().Add(new MemberLocationRole
            {
                OrganizationId = organizationId, UserId = userId, LocationId = locationId, RoleId = role.Id
            });
            await db.SaveChangesAsync();
            return true;
        });

    /// <summary>A member's stored cells at a location as "Module:Level", ordered by module.</summary>
    public static Task<List<string>> CellsAsync(TenantApiFactory factory, Guid organizationId, Guid userId, Guid locationId)
        => factory.InOrganizationAsync<IdentityDbContext, List<string>>(organizationId, async db =>
            (await db.Set<MemberLocationLevel>().Where(c => c.UserId == userId && c.LocationId == locationId).ToListAsync())
                .OrderBy(c => c.Module).Select(c => $"{c.Module}:{c.Level}").ToList());

    public static List<string> Cells(params (AccessModule Module, AccessLevel Level)[] levels)
        => levels.OrderBy(l => l.Module).Select(l => $"{l.Module}:{l.Level}").ToList();

    public static Task<bool> IsMemberAsync(TenantApiFactory factory, Guid organizationId, Guid userId)
        => factory.InOrganizationAsync<IdentityDbContext, bool>(organizationId,
            db => db.Set<OrganizationMember>().AnyAsync(m => m.UserId == userId && m.OrganizationId == organizationId));

    public static Task<List<AuditLogEntry>> AuditEntriesAsync(TenantApiFactory factory, Guid organizationId, string action)
        => factory.InOrganizationAsync<AuditLogDbContext, List<AuditLogEntry>>(organizationId,
            db => db.AuditLogEntries.Where(e => e.Action == action).ToListAsync());

    public static Task<List<OrganizationInvitation>> InvitationsAsync(TenantApiFactory factory, Guid organizationId)
        => factory.InOrganizationAsync<IdentityDbContext, List<OrganizationInvitation>>(organizationId,
            db => db.Set<OrganizationInvitation>().ToListAsync());

    public static object InviteBody(string email, Guid locationId, string[] roleNames, Dictionary<string, string>? levels = null)
        => new
        {
            email,
            locations = new[] { new { locationId, roleNames, levels = levels ?? new Dictionary<string, string>() } },
            isOwner = true
        };

    public static async Task<Guid> InviteAsync(TenantApiFactory factory, Guid callerId, string email, Guid locationId, string[] roleNames,
        Dictionary<string, string>? levels = null)
    {
        var response = await factory.ClientFor(callerId, CantinaId).PostAsJsonAsync("/api/invitations", InviteBody(email, locationId, roleNames, levels));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InvitationReply>())!.Id;
    }

    public sealed record InvitationReply(Guid Id, string Email, DateTime ExpiresAt);

    /// <summary>The one-time token carried by the link of the latest email sent to the address.</summary>
    public static string LatestTokenSentTo(TenantApiFactory factory, string email)
    {
        var html = factory.EmailService.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IEmailService.SendEmailAsync))
            .Select(c => c.GetArguments())
            .Last(a => (string)a[0]! == email)[2]!.ToString()!;
        return Uri.UnescapeDataString(Regex.Match(html, @"token=([A-Za-z0-9_\-%.~]+)").Groups[1].Value);
    }
}
