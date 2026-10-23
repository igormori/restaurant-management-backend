using NSubstitute;
using NSubstitute.Core;
using RestaurantManagement.Modules.Identity.Models.Sessions;
using RestaurantManagement.Shared.Utils.Exceptions;
using Sentry;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// Login lock per address and per account. Maria signs in at Cantina da Nonna from the kitchen tablet
    /// (203.0.113.10) and the manager's laptop (198.51.100.24); an attacker who knows her email guesses from
    /// other addresses.
    /// </summary>
    public class SessionServiceLockoutTests
    {
        private static readonly string Tablet = LockoutHarness.Tablet.IpAddress!;

        [Fact]
        [Trait("Rule", "L22")]
        public async Task LoginAsync_AddressLocked_CorrectPasswordFromAnotherAddressSignsIn()
        {
            // Arrange
            using var harness = await LockoutHarness.CreateAsync();
            await harness.FailAsync(LockoutHarness.Tablet, 5);

            // Act
            var response = await harness.LoginAsync(LockoutHarness.Laptop);

            // Assert
            response.Auth.Token.ShouldNotBeNullOrEmpty("one address being locked out never keeps the owner out from another address");
        }

        [Fact]
        [Trait("Rule", "L23")]
        public async Task LoginAsync_NineteenWrongPasswordsFromFiveAddresses_DoesNotLockTheAccount()
        {
            // Arrange
            using var harness = await LockoutHarness.CreateAsync();
            await harness.FailFromAddressesAsync(addressCount: 4, timesEach: 4);
            await harness.FailAsync(LockoutHarness.At("203.0.113.105"), 3);

            // Act
            var response = await harness.LoginAsync(LockoutHarness.Laptop);

            // Assert
            response.Auth.Token.ShouldNotBeNullOrEmpty("19 wrong passwords across addresses stay under the account limit of 20, so no lock applies");
        }

        [Fact]
        [Trait("Rule", "L23")]
        public async Task LoginAsync_TwentyWrongPasswordsFromFiveAddresses_LocksTheAccountForEveryAddress()
        {
            // Arrange
            using var harness = await LockoutHarness.CreateAsync();

            // Act
            await harness.FailFromAddressesAsync(addressCount: 5, timesEach: 4);

            // Assert
            (await harness.ReadFailuresAsync()).Count.ShouldBe(20, "every counted wrong password is stored");
            var maria = await harness.ReadUserAsync();
            maria.LockedUntil.ShouldNotBeNull("the 20th wrong password locks the whole account");
            maria.LockedUntil.Value.ShouldBe(DateTime.UtcNow.AddMinutes(15), TimeSpan.FromMinutes(1), "the account lock lasts 15 minutes");
            await Should.ThrowAsync<BusinessException>(() => harness.LoginAsync(LockoutHarness.Laptop));
        }

        [Fact]
        [Trait("Rule", "L35")]
        public async Task LoginAsync_OneAddressRepeatsFiveWrongPasswordsAfterEachLockExpires_NeverLocksTheAccount()
        {
            // Arrange
            using var harness = await LockoutHarness.CreateAsync();
            await harness.FailAsync(LockoutHarness.Tablet, 5);
            await harness.RewindAsync(minutes: 16);
            await harness.FailAsync(LockoutHarness.Tablet, 5);
            await harness.RewindAsync(minutes: 16);
            await harness.FailAsync(LockoutHarness.Tablet, 5);
            await harness.RewindAsync(minutes: 16);

            // Act
            await harness.FailAsync(LockoutHarness.Tablet, 5);

            // Assert
            (await harness.ReadUserAsync()).LockedUntil.ShouldBeNull("one address adds at most 5 to the account count, so 20 wrong passwords from it alone never lock the account");
            var response = await harness.LoginAsync(LockoutHarness.Laptop);
            response.Auth.Token.ShouldNotBeNullOrEmpty("the owner still signs in from another address");
        }

        [Fact]
        [Trait("Rule", "L10")]
        public async Task LoginAsync_AddressLocked_WrongPasswordsAreNotCountedAndDoNotExtendTheLock()
        {
            // Arrange
            using var harness = await LockoutHarness.CreateAsync();
            harness.AccountMax = 6;
            await harness.FailAsync(LockoutHarness.Tablet, 5);
            var lockEndBefore = (await harness.ReadFailuresAsync()).Max(f => f.AddressLockedUntil);

            // Act
            await harness.FailAsync(LockoutHarness.Tablet, 3);

            // Assert
            var failures = await harness.ReadFailuresAsync();
            failures.Count.ShouldBe(5, "wrong passwords while the address is locked are not stored or counted");
            failures.Max(f => f.AddressLockedUntil).ShouldBe(lockEndBefore, "wrong passwords while locked do not extend the lock");
            (await harness.ReadUserAsync()).LockedUntil.ShouldBeNull("attempts during the address lock do not push the account over its limit");
        }

        [Fact]
        [Trait("Rule", "L4")]
        public async Task LoginAsync_CorrectPasswordAfterFourWrong_RestartsThatAddressCount()
        {
            // Arrange
            using var harness = await LockoutHarness.CreateAsync();
            await harness.FailAsync(LockoutHarness.Tablet, 4);
            await harness.LoginAsync(LockoutHarness.Tablet);
            await harness.FailAsync(LockoutHarness.Tablet, 4);

            // Act
            var response = await harness.LoginAsync(LockoutHarness.Tablet);

            // Assert
            response.Auth.Token.ShouldNotBeNullOrEmpty("a correct password clears the address count, so four more wrong passwords do not lock it");
        }

        [Fact]
        [Trait("Rule", "L24")]
        public async Task LoginAsync_CorrectPasswordFromOneAddress_LeavesTheAccountCountUntouched()
        {
            // Arrange
            using var harness = await LockoutHarness.CreateAsync();
            await harness.FailAsync(LockoutHarness.Tablet, 4);
            await harness.LoginAsync(LockoutHarness.Tablet);

            // Act
            await harness.FailFromAddressesAsync(addressCount: 4, timesEach: 4);

            // Assert
            (await harness.ReadFailuresAsync()).Count.ShouldBe(20, "the tablet's four failures stay stored for the account limit after its correct password");
            (await harness.ReadUserAsync()).LockedUntil.ShouldNotBeNull("a correct password from one address does not reduce the account's count, so 20 in total lock it");
        }

        [Fact]
        [Trait("Rule", "L25")]
        public async Task LoginAsync_AnotherAccountFailedFromTheSameAddress_DoesNotCountForThisAccount()
        {
            // Arrange
            using var harness = await LockoutHarness.CreateAsync();
            await harness.AddUserAsync("bruno@cantinadanonna.com.br", "Bruno");
            await harness.FailAsync(LockoutHarness.Tablet, 4, "bruno@cantinadanonna.com.br");
            await harness.FailAsync(LockoutHarness.Tablet, 1);

            // Act
            var response = await harness.LoginAsync(LockoutHarness.Tablet);

            // Assert
            response.Auth.Token.ShouldNotBeNullOrEmpty("Bruno's failures from the same address never count toward Maria's lock");
        }

        [Fact]
        [Trait("Rule", "L26")]
        public async Task LoginAsync_NineteenWrongPasswordsMoreThanAnHourAgo_DoNotCountTowardTheAccountLimit()
        {
            // Arrange
            using var harness = await LockoutHarness.CreateAsync();
            await harness.FailFromAddressesAsync(addressCount: 4, timesEach: 4);
            await harness.FailAsync(LockoutHarness.At("203.0.113.105"), 3);
            await harness.RewindAsync(minutes: 61);
            await harness.FailAsync(LockoutHarness.At("203.0.113.106"), 1);

            // Act
            var response = await harness.LoginAsync(LockoutHarness.Laptop);

            // Assert
            response.Auth.Token.ShouldNotBeNullOrEmpty("failures older than the 60-minute window no longer count, so the 20th overall does not lock");
        }

        [Fact]
        [Trait("Rule", "L26")]
        public async Task LoginAsync_NewFailureAfterTheWindow_RemovesFailuresOlderThanTheWindow()
        {
            // Arrange
            using var harness = await LockoutHarness.CreateAsync();
            await harness.FailAsync(LockoutHarness.Tablet, 3);
            await harness.RewindAsync(minutes: 61);

            // Act
            await harness.FailAsync(LockoutHarness.Laptop, 1);

            // Assert
            var failures = await harness.ReadFailuresAsync();
            failures.ShouldHaveSingleItem("failures older than the window with no running address lock are deleted when a new one is stored");
            failures.Single().IpAddress.ShouldBe(LockoutHarness.Laptop.IpAddress);
        }

        [Fact]
        [Trait("Rule", "L11")]
        public async Task LoginAsync_AddressLockExpired_CorrectPasswordFromThatAddressSignsIn()
        {
            // Arrange
            using var harness = await LockoutHarness.CreateAsync();
            await harness.FailAsync(LockoutHarness.Tablet, 5);
            await harness.RewindAsync(minutes: 16);

            // Act
            var response = await harness.LoginAsync(LockoutHarness.Tablet);

            // Assert
            response.Auth.Token.ShouldNotBeNullOrEmpty("once the 15-minute address lock has expired the correct password signs in");
        }

        [Fact]
        [Trait("Rule", "L27")]
        public async Task LoginAsync_WrongPasswordWithNoKnownAddress_IsStoredAsUnknown()
        {
            // Arrange
            using var harness = await LockoutHarness.CreateAsync();

            // Act
            await harness.FailAsync(LockoutHarness.At(null), 1);

            // Assert
            var failure = (await harness.ReadFailuresAsync()).ShouldHaveSingleItem("the wrong password is counted even without an address");
            failure.IpAddress.ShouldBe("unknown", "a missing address is stored as unknown so all such attempts share one count");
        }

        [Fact]
        [Trait("Rule", "L18")]
        public async Task LoginAsync_AccountLimitReached_SentryMessageHasUserIdAndNoEmail()
        {
            // Arrange
            using var harness = await LockoutHarness.CreateAsync();
            harness.AccountMax = 2;
            var hub = Substitute.For<IHub>();
            hub.IsEnabled.Returns(true);
            harness.Hub = hub;
            var maria = await harness.ReadUserAsync();

            // Act
            await harness.FailAsync(LockoutHarness.Tablet, 1);
            await harness.FailAsync(LockoutHarness.Laptop, 1);

            // Assert
            var message = CapturedEvents(hub).ShouldHaveSingleItem("locking the whole account sends one alert").Message?.Formatted;
            message.ShouldNotBeNull();
            message.ShouldContain(maria.Id.ToString(), customMessage: "the alert identifies the user by ID");
            message.ShouldNotContain(LockoutHarness.MariaEmail, customMessage: "the alert never contains the email");
            message.ShouldNotContain(Tablet, customMessage: "the alert never contains an IP address");
        }

        private static List<SentryEvent> CapturedEvents(IHub hub)
            => hub.ReceivedCalls()
                .Where(call => call.GetMethodInfo().Name == nameof(IHub.CaptureEvent))
                .Select(call => (SentryEvent)call.GetArguments()[0]!)
                .ToList();
    }
}
