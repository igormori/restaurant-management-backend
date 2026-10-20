using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Auth;

namespace RestaurantManagement.Modules.Identity.Services
{
    /// <summary>
    /// The roles every new organization starts with and their default levels (spec table "Default
    /// levels per role"). A module not listed for a role is None, which is never stored.
    /// </summary>
    public static class OrganizationDefaultRoles
    {
        public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<AccessModule, AccessLevel>> Levels =
            new Dictionary<string, IReadOnlyDictionary<AccessModule, AccessLevel>>
            {
                [Roles.Admin] = new Dictionary<AccessModule, AccessLevel>
                {
                    [AccessModule.Locations] = AccessLevel.Manage,
                    [AccessModule.Menus] = AccessLevel.Manage,
                    [AccessModule.Cashier] = AccessLevel.Manage,
                    [AccessModule.Orders] = AccessLevel.Manage,
                    [AccessModule.Kitchen] = AccessLevel.Manage,
                    [AccessModule.Reports] = AccessLevel.Manage,
                    [AccessModule.Members] = AccessLevel.Manage
                },
                [Roles.Manager] = new Dictionary<AccessModule, AccessLevel>
                {
                    [AccessModule.Locations] = AccessLevel.Read,
                    [AccessModule.Menus] = AccessLevel.Manage,
                    [AccessModule.Orders] = AccessLevel.Read,
                    [AccessModule.Kitchen] = AccessLevel.Read,
                    [AccessModule.Reports] = AccessLevel.Manage
                },
                [Roles.Cashier] = new Dictionary<AccessModule, AccessLevel>
                {
                    [AccessModule.Locations] = AccessLevel.Read,
                    [AccessModule.Menus] = AccessLevel.Read,
                    [AccessModule.Cashier] = AccessLevel.Manage,
                    [AccessModule.Orders] = AccessLevel.Read
                },
                [Roles.Waiter] = new Dictionary<AccessModule, AccessLevel>
                {
                    [AccessModule.Locations] = AccessLevel.Read,
                    [AccessModule.Menus] = AccessLevel.Read,
                    [AccessModule.Orders] = AccessLevel.Manage
                },
                [Roles.Kitchen] = new Dictionary<AccessModule, AccessLevel>
                {
                    [AccessModule.Locations] = AccessLevel.Read,
                    [AccessModule.Menus] = AccessLevel.Read,
                    [AccessModule.Orders] = AccessLevel.Read,
                    [AccessModule.Kitchen] = AccessLevel.Manage
                }
            };
    }
}
