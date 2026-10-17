using System.ComponentModel.DataAnnotations;
using RestaurantManagement.Modules.Organization.Models;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Organization.Tests
{
    /// <summary>
    /// The create-organization request's currency (spec Rule 1): BRL, USD or CAD, chosen at creation, no default.
    /// </summary>
    public class OrganizationCurrencyValidationTests
    {
        private static List<ValidationResult> Validate(object request)
        {
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
            return results;
        }

        // Scenario 1
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("EUR")]
        [InlineData("1")]
        [Trait("Rule", "OR21")]
        public void Validate_CurrencyMissingOrNotSupported_ReturnsCurrencyError(string? currency)
        {
            // Arrange
            var request = new CreateOrganizationRequest { Name = "Cantina da Nonna", Currency = currency };

            // Act
            var results = Validate(request);

            // Assert
            results.SelectMany(r => r.MemberNames).ShouldContain(nameof(CreateOrganizationRequest.Currency));
        }
    }
}
