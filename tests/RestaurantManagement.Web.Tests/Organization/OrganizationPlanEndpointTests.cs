using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Organization.Data;
using RestaurantManagement.Shared;
using Shouldly;

namespace RestaurantManagement.Web.Tests;

/// <summary>The plan of a new organization is always the 30-day trial, whatever the request body says.</summary>
public class OrganizationPlanEndpointTests : IClassFixture<PostgresDatabaseFixture>
{
    private static readonly Guid AnaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");

    private readonly PostgresDatabaseFixture _postgres;

    public OrganizationPlanEndpointTests(PostgresDatabaseFixture postgres) => _postgres = postgres;

    [Fact]
    [Trait("Rule", "OR2")]
    public async Task CreateOrganization_BodyHasPlanAndTrialFields_StoresAndReturnsTrialPlan()
    {
        // Arrange
        await using var factory = await TenantApiFactory.CreateAsync(_postgres);
        await factory.SeedUserAsync(AnaId, "ana@cantinadanonna.com.br", "Ana");
        var client = factory.ClientFor(AnaId, organizationId: null);
        var body = """
            {
              "name": "Pizzaria Vila Madalena",
              "currency": "BRL",
              "planType": "ENTERPRISE",
              "maxLocations": 50,
              "trialEndDate": "2030-01-01",
              "isTrialActive": false
            }
            """;

        // Act
        var response = await client.PostAsync("/api/organizations/create", new StringContent(body, Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var returned = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        var organizationId = returned["id"]!.GetValue<Guid>();
        var stored = await factory.InOrganizationAsync<OrganizationDbContext, Modules.Organization.Entities.OrganizationSettings>(
            organizationId, db => db.OrganizationSettings.IgnoreQueryFilters().SingleAsync(s => s.OrganizationId == organizationId));

        returned["planType"]!.GetValue<string>().ShouldBe("TRIAL", "the returned plan ignores the plan sent in the body");
        returned["maxLocations"]!.GetValue<int>().ShouldBe(1, "the returned plan ignores the location limit sent in the body");
        returned["isTrialActive"]!.GetValue<bool>().ShouldBeTrue("the returned trial is active whatever the body says");
        returned["trialEndDate"]!.GetValue<DateTime>().ShouldBe(DateTime.UtcNow.AddDays(30), TimeSpan.FromMinutes(5), "the returned trial ends about 30 days from now, not on the date sent");
        stored.PlanType.ShouldBe("TRIAL", "the stored plan ignores the plan sent in the body");
        stored.MaxLocations.ShouldBe(1, "the stored plan ignores the location limit sent in the body");
        stored.IsTrialActive.ShouldBeTrue("the stored trial is active whatever the body says");
        stored.TrialEndDate.ShouldBe(DateTime.UtcNow.AddDays(30), TimeSpan.FromMinutes(5), "the stored trial ends about 30 days from now, not on the date sent");
    }
}
