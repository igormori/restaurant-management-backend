using System.ComponentModel.DataAnnotations;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Modules.Identity.Services;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// The single password hashing interface: BCrypt at work factor 12, and the 64-character
    /// password maximum that keeps BCrypt from silently cutting a longer password at 72 bytes.
    /// </summary>
    public class BcryptPasswordHasherTests
    {
        private const string Password = "Massa@Fresca2026";
        private readonly IPasswordHasher _sut = new BcryptPasswordHasher();

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Verify_BlankPassword_ReturnsFalse(string? password)
        {
            // Act
            var result = _sut.Verify(password!, _sut.Hash(Password));

            // Assert
            result.ShouldBeFalse();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not-a-bcrypt-hash")]
        public void Verify_BlankOrMalformedHash_ReturnsFalse(string? hash)
        {
            // Act
            var result = _sut.Verify(Password, hash!);

            // Assert
            result.ShouldBeFalse();
        }

        private static readonly string Password72Bytes = "Massa@Fresca26" + new string('é', 29);
        private static readonly string Password73Bytes = Password72Bytes + "x";

        [Fact]
        public void Hash_PasswordOver72Bytes_Throws()
        {
            // Act
            var act = () => _sut.Hash(Password73Bytes);

            // Assert
            Should.Throw<ArgumentException>(act);
        }

        [Fact]
        public void Verify_PasswordOver72Bytes_ReturnsFalseEvenWhenFirst72BytesMatch()
        {
            // Arrange
            var hash = _sut.Hash(Password72Bytes);

            // Act
            var result = _sut.Verify(Password73Bytes, hash);

            // Assert
            result.ShouldBeFalse();
        }

        [Fact]
        public void Hash_PasswordOfExactly72Bytes_HashesAndVerifies()
        {
            // Act
            var hash = _sut.Hash(Password72Bytes);

            // Assert
            _sut.Verify(Password72Bytes, hash).ShouldBeTrue();
        }

        [Fact]
        public void Verify_WrongPassword_ReturnsFalse()
        {
            // Arrange
            var hash = _sut.Hash(Password);

            // Act
            var result = _sut.Verify("Massa@Fresca2027", hash);

            // Assert
            result.ShouldBeFalse();
        }

        [Fact]
        public void Hash_Password_UsesWorkFactor12AndVerifies()
        {
            // Act
            var hash = _sut.Hash(Password);

            // Assert
            hash.ShouldStartWith("$2a$12$");
            _sut.Verify(Password, hash).ShouldBeTrue();
        }

        [Fact]
        public void Hash_SamePasswordTwice_ReturnsDifferentHashes()
        {
            // Act
            var first = _sut.Hash(Password);
            var second = _sut.Hash(Password);

            // Assert
            first.ShouldNotBe(second);
        }

        [Fact]
        public void Validate_LoginPasswordOver64Characters_ReturnsPasswordError()
        {
            // Arrange
            var request = new LoginRequest { Email = "maria@cantinadanonna.com.br", Password = new string('a', 65) };
            var results = new List<ValidationResult>();

            // Act
            var isValid = Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);

            // Assert
            isValid.ShouldBeFalse();
            results.ShouldContain(r => r.MemberNames.Contains("Password"));
        }

        [Fact]
        public void Validate_CurrentPasswordOver64Characters_ReturnsCurrentPasswordError()
        {
            // Arrange
            var request = new ChangePasswordRequest { CurrentPassword = new string('a', 65), NewPassword = "Massa@Fresca2026" };
            var results = new List<ValidationResult>();

            // Act
            var isValid = Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);

            // Assert
            isValid.ShouldBeFalse();
            results.ShouldContain(r => r.MemberNames.Contains("CurrentPassword"));
        }
    }
}
