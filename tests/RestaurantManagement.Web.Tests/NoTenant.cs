using NSubstitute;
using RestaurantManagement.Shared.Data;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// A tenant with no organization, for database-level tests (migrations, delete rules) that build a
/// module DbContext directly and read rows back with IgnoreQueryFilters().
/// </summary>
public static class NoTenant
{
    public static ITenantContext Context => Substitute.For<ITenantContext>();
}
