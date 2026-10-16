using System.ComponentModel.DataAnnotations;
using RestaurantManagement.Modules.Identity.Models;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>Required fields and length limits of the registration request.</summary>
    public class RegisterRequestValidationTests
    {
        private static RegisterRequest ValidRequest() => new()
        {
            Email = "ana@cantinadanonna.com.br",
            Password = "Massa@Fresca2026",
            FirstName = "Ana",
            LastName = "Souza"
        };

        private static List<ValidationResult> Validate(RegisterRequest request)
        {
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
            return results;
        }

        public static TheoryData<string, Action<RegisterRequest>> InvalidFields => new()
        {
            { "Email", r => r.Email = null! },
            { "FirstName", r => r.FirstName = null! },
            { "LastName", r => r.LastName = null! },
            { "FirstName", r => r.FirstName = new string('a', 101) },
            { "LastName", r => r.LastName = new string('a', 101) },
            { "Email", r => r.Email = new string('a', 309) + "@example.com" }, // 321 characters
            { "PhoneNumber", r => r.PhoneNumber = "+5541999990000123456789" }, // 23 characters
        };

        [Theory]
        [MemberData(nameof(InvalidFields))]
        public void Validate_MissingOrTooLongField_ReturnsErrorOnThatField(string field, Action<RegisterRequest> breakField)
        {
            // Arrange
            var request = ValidRequest();
            breakField(request);

            // Act
            var results = Validate(request);

            // Assert
            results.ShouldContain(r => r.MemberNames.Contains(field));
        }

        [Fact]
        public void Validate_NamesOfExactly100Characters_ReturnsNoError()
        {
            // Arrange
            var request = ValidRequest();
            request.FirstName = new string('a', 100);
            request.LastName = new string('b', 100);

            // Act
            var results = Validate(request);

            // Assert
            results.ShouldBeEmpty();
        }
    }
}
