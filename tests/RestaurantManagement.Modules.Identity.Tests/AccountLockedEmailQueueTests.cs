using RestaurantManagement.Modules.Identity.Entities.Registration;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// When a login locks Maria's whole account, one email waits in the queue for her; an address lock sends
    /// none, and neither do attempts made during a lock. Nothing is sent during the login request itself.
    /// </summary>
    public class AccountLockedEmailQueueTests
    {
        [Fact]
        [Trait("Rule", "L28")]
        public async Task LoginAsync_AccountLimitReached_QueuesOneAccountLockedEmailForTheOwner()
        {
            // Arrange
            using var harness = await LockoutHarness.CreateAsync();
            harness.AccountMax = 2;

            // Act
            await harness.FailAsync(LockoutHarness.Tablet, 1);
            await harness.FailAsync(LockoutHarness.Laptop, 1);

            // Assert
            var queued = (await harness.ReadQueueAsync()).ShouldHaveSingleItem("locking the whole account queues exactly one email");
            queued.Email.ShouldBe(LockoutHarness.MariaEmail, "the email goes to the locked account");
            ((int)queued.Purpose).ShouldBe(3, "the request is marked as an account-locked email");
        }

        [Fact]
        [Trait("Rule", "L29")]
        public async Task LoginAsync_OnlyTheAddressIsLocked_QueuesNoEmail()
        {
            // Arrange
            using var harness = await LockoutHarness.CreateAsync();

            // Act
            await harness.FailAsync(LockoutHarness.Tablet, 5);

            // Assert
            (await harness.ReadQueueAsync()).ShouldBeEmpty("an address lock sends no email");
        }

        [Fact]
        [Trait("Rule", "L28")]
        public async Task LoginAsync_WrongPasswordsDuringTheAccountLock_QueueNoFurtherEmail()
        {
            // Arrange
            using var harness = await LockoutHarness.CreateAsync();
            harness.AccountMax = 2;
            await harness.FailAsync(LockoutHarness.Tablet, 1);
            await harness.FailAsync(LockoutHarness.Laptop, 1);

            // Act
            await harness.FailAsync(LockoutHarness.At("203.0.113.101"), 3);

            // Assert
            (await harness.ReadQueueAsync()).Count(r => r.Purpose == PendingEmailPurpose.AccountLocked)
                .ShouldBe(1, "attempts during a lock are not counted, so they never lock the account again or queue another email");
        }

        [Fact]
        [Trait("Rule", "L36")]
        public async Task LoginAsync_AccountLockedAgainMoreThan24HoursAfterTheLastEmail_QueuesANewEmail()
        {
            // Arrange
            using var harness = await LockoutHarness.CreateAsync();
            harness.AccountMax = 2;
            await harness.FailAsync(LockoutHarness.Tablet, 1);
            await harness.FailAsync(LockoutHarness.Laptop, 1);
            await harness.SendQueuedEmailsAsync();
            await harness.RewindAsync(minutes: 24 * 60 + 1);
            await harness.RewindLastLockEmailAsync(minutes: 24 * 60 + 1);

            // Act
            await harness.FailAsync(LockoutHarness.Tablet, 1);
            await harness.FailAsync(LockoutHarness.Laptop, 1);

            // Assert
            (await harness.ReadQueueAsync()).Count(r => r.Purpose == PendingEmailPurpose.AccountLocked)
                .ShouldBe(1, "a lock more than 24 hours after the last lock email queues a new one");
        }

        [Fact]
        [Trait("Rule", "L36")]
        public async Task LoginAsync_AccountLockedAgainWithin24HoursOfTheLastEmail_QueuesNoNewEmail()
        {
            // Arrange
            using var harness = await LockoutHarness.CreateAsync();
            harness.AccountMax = 2;
            await harness.FailAsync(LockoutHarness.Tablet, 1);
            await harness.FailAsync(LockoutHarness.Laptop, 1);
            await harness.SendQueuedEmailsAsync();
            await harness.RewindAsync(minutes: 16);

            // Act
            await harness.FailAsync(LockoutHarness.Tablet, 1);
            await harness.FailAsync(LockoutHarness.Laptop, 1);

            // Assert
            (await harness.ReadUserAsync()).LockedUntil.ShouldNotBeNull("the account is locked again");
            (await harness.ReadQueueAsync()).ShouldBeEmpty("at most one lock email per account per 24 hours, even though the first one was already sent and removed");
        }
    }
}
