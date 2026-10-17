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
        [Trait("Rule", "T7")]
        public void Verify_BlankPassword_ReturnsFalse(string? password)
        {
            // Act
            var result = _sut.Verify(password!, _sut.Hash(Password));

            // Assert
            result.ShouldBeFalse("a blank password never matches a stored hash");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not-a-bcrypt-hash")]
        [Trait("Rule", "T7")]
        public void Verify_BlankOrMalformedHash_ReturnsFalse(string? hash)
        {
            // Act
            var result = _sut.Verify(Password, hash!);

            // Assert
            result.ShouldBeFalse("a blank or malformed stored hash never matches any password");
        }

        private static readonly string Password72Bytes = "Massa@Fresca26" + new string('é', 29);
        private static readonly string Password73Bytes = Password72Bytes + "x";

        [Fact]
        [Trait("Rule", "T7")]
        public void Hash_PasswordOver72Bytes_Throws()
        {
            // Act
            var act = () => _sut.Hash(Password73Bytes);

            // Assert
            Should.Throw<ArgumentException>(act);
        }

        [Fact]
        [Trait("Rule", "T7")]
        public void Verify_PasswordOver72Bytes_ReturnsFalseEvenWhenFirst72BytesMatch()
        {
            // Arrange
            var hash = _sut.Hash(Password72Bytes);

            // Act
            var result = _sut.Verify(Password73Bytes, hash);

            // Assert
            result.ShouldBeFalse("a password over 72 bytes must not match just because its first 72 bytes do");
        }

        [Fact]
        [Trait("Rule", "T7")]
        public void Hash_PasswordOfExactly72Bytes_HashesAndVerifies()
        {
            // Act
            var hash = _sut.Hash(Password72Bytes);

            // Assert
            _sut.Verify(Password72Bytes, hash).ShouldBeTrue("a password of exactly 72 bytes is stored and still verifies");
        }

        [Fact]
        [Trait("Rule", "T7")]
        public void Verify_WrongPassword_ReturnsFalse()
        {
            // Arrange
            var hash = _sut.Hash(Password);

            // Act
            var result = _sut.Verify("Massa@Fresca2027", hash);

            // Assert
            result.ShouldBeFalse("a wrong password never matches");
        }

        [Fact]
        [Trait("Rule", "R13")]
        public void Hash_Password_UsesWorkFactor12AndVerifies()
        {
            // Act
            var hash = _sut.Hash(Password);

            // Assert
            hash.ShouldStartWith("$2a$12$", customMessage: "passwords are stored only as bcrypt hashes with work factor 12");
            _sut.Verify(Password, hash).ShouldBeTrue();
        }

        [Fact]
        [Trait("Rule", "T7")]
        public void Hash_SamePasswordTwice_ReturnsDifferentHashes()
        {
            // Act
            var first = _sut.Hash(Password);
            var second = _sut.Hash(Password);

            // Assert
            first.ShouldNotBe(second, "each hash is salted, so the same password never produces the same stored value");
        }

        [Fact]
        [Trait("Rule", "L13")]
        public void Validate_LoginPasswordOver64Characters_ReturnsPasswordError()
        {
            // Arrange
            var request = new LoginRequest { Email = "maria@cantinadanonna.com.br", Password = new string('a', 65) };
            var results = new List<ValidationResult>();

            // Act
            var isValid = Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);

            // Assert
            isValid.ShouldBeFalse("a login password over 64 characters is refused");
            results.ShouldContain(r => r.MemberNames.Contains("Password"), "the error is reported on the password field");
        }

        [Fact]
        [Trait("Rule", "C7")]
        public void Validate_CurrentPasswordOver64Characters_ReturnsCurrentPasswordError()
        {
            // Arrange
            var request = new ChangePasswordRequest { CurrentPassword = new string('a', 65), NewPassword = "Massa@Fresca2026" };
            var results = new List<ValidationResult>();

            // Act
            var isValid = Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);

            // Assert
            isValid.ShouldBeFalse("a current password over 64 characters is refused before any check");
            results.ShouldContain(r => r.MemberNames.Contains("CurrentPassword"), "the error is reported on the current password field");
        }
    }
}
