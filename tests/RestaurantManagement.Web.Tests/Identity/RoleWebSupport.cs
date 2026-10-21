using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Shared.Auth;
using static RestaurantManagement.Web.Tests.InvitationWebSupport;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Extra setup for the role endpoint tests, on top of <see cref="InvitationWebSupport"/>: Cantina da Nonna and
/// Trattoria do Sol each have the five default roles; Trattoria also has Sommelier. Maria is an Admin with
/// Members = Manage at Batel only, so <see cref="MakeMariaAdminAtBothLocationsAsync"/> gives her Mercado too.
/// </summary>
internal static class RoleWebSupport
{
    public static readonly Guid CaioId = Guid.Parse("88888888-0000-4000-8000-0000000000a1");

    public sealed record RoleReply(Guid Id, string Name, Dictionary<string, string> Levels);

    public static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    public static Task MakeMariaAdminAtBothLocationsAsync(TenantApiFactory factory)
        => factory.GrantAsync(MariaId, CantinaId, MercadoId,
            (AccessModule.Locations, AccessLevel.Manage), (AccessModule.Menus, AccessLevel.Manage), (AccessModule.Orders, AccessLevel.Manage),
            (AccessModule.Members, AccessLevel.Manage));

    /// <summary>Caio, a member of Cantina whose levels at Batel are the default levels of the given role.</summary>
    public static async Task SeedCaioWithRoleAsync(TenantApiFactory factory, string role)
    {
        await factory.SeedUserAsync(CaioId, "caio@cantinadanonna.com.br", "Caio", TenantApiFactory.MemberOf(CantinaId));
        await factory.GrantAsync(CaioId, CantinaId, BatelId, DefaultLevelsOf(role));
    }

    public static Task<Guid> RoleIdAsync(TenantApiFactory factory, Guid organizationId, string name)
        => factory.InOrganizationAsync<IdentityDbContext, Guid>(organizationId,
            async db => (await db.Set<OrganizationRole>().SingleAsync(r => r.OrganizationId == organizationId && r.Name == name)).Id);

    public static Task<List<string>> RoleNamesAsync(TenantApiFactory factory, Guid organizationId)
        => factory.InOrganizationAsync<IdentityDbContext, List<string>>(organizationId,
            db => db.Set<OrganizationRole>().Where(r => r.OrganizationId == organizationId).Select(r => r.Name).ToListAsync());

    /// <summary>A role's stored default levels as "Module:Level", ordered by module.</summary>
    public static Task<List<string>> RoleLevelsAsync(TenantApiFactory factory, Guid organizationId, Guid roleId)
        => factory.InOrganizationAsync<IdentityDbContext, List<string>>(organizationId, async db =>
            (await db.Set<OrganizationRoleLevel>().Where(l => l.RoleId == roleId).ToListAsync())
                .OrderBy(l => l.Module).Select(l => $"{l.Module}:{l.Level}").ToList());

    public static (AccessModule, AccessLevel)[] DefaultLevelsOf(string role) => role switch
    {
        "Manager" => new[]
        {
            (AccessModule.Locations, AccessLevel.Read), (AccessModule.Menus, AccessLevel.Manage), (AccessModule.Orders, AccessLevel.Read),
            (AccessModule.Kitchen, AccessLevel.Read), (AccessModule.Reports, AccessLevel.Manage)
        },
        "Cashier" => CashierLevels,
        "Waiter" => WaiterLevels,
        _ => new[]
        {
            (AccessModule.Locations, AccessLevel.Read), (AccessModule.Menus, AccessLevel.Read), (AccessModule.Orders, AccessLevel.Read),
            (AccessModule.Kitchen, AccessLevel.Manage)
        }
    };

    public static async Task<List<RoleReply>> ListRolesAsync(HttpClient client)
        => (await client.GetFromJsonAsync<List<RoleReply>>("/api/roles"))!;
}
