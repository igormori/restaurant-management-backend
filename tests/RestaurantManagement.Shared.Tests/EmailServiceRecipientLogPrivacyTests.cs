using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RestaurantManagement.Shared.Options;
using RestaurantManagement.Shared.Services.Email;
using Shouldly;

namespace RestaurantManagement.Shared.Tests
{
    /// <summary>
    /// Email addresses are personal data and must never reach the logs, whether the send works or fails.
    /// </summary>
    public class EmailServiceRecipientLogPrivacyTests
    {
        private const string Recipient = "maria@cantinadanonna.com.br";

        private sealed class CapturingLogger : ILogger<EmailService>
        {
            public List<string> Messages { get; } = new();

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                Messages.Add(formatter(state, exception));
                if (exception != null)
                {
                    Messages.Add(exception.ToString());
                }
            }
        }

        private static EmailService CreateSut(CapturingLogger logger, int port)
        {
            var environment = Substitute.For<IHostEnvironment>();
            environment.EnvironmentName.Returns(Environments.Production);
            var options = Microsoft.Extensions.Options.Options.Create(new EmailOptions
            {
                SmtpHost = "127.0.0.1",
                SmtpPort = port,
                EnableSsl = false,
                SmtpUser = string.Empty,
                FromEmail = "no-reply@cantinadanonna.com.br"
            });
            return new EmailService(options, logger, environment);
        }

        // Minimal SMTP (Simple Mail Transfer Protocol) server that accepts one message.
        private static async Task RunFakeSmtpServerAsync(TcpListener listener)
        {
            using var client = await listener.AcceptTcpClientAsync();
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII);
            using var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true, NewLine = "\r\n" };
            await writer.WriteLineAsync("220 localhost ready");
            var inData = false;
            while (await reader.ReadLineAsync() is { } line)
            {
                if (inData)
                {
                    if (line == ".")
                    {
                        inData = false;
                        await writer.WriteLineAsync("250 queued");
                    }
                    continue;
                }

                if (line.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase)) await writer.WriteLineAsync("250 localhost");
                else if (line.StartsWith("DATA", StringComparison.OrdinalIgnoreCase)) { inData = true; await writer.WriteLineAsync("354 go"); }
                else if (line.StartsWith("QUIT", StringComparison.OrdinalIgnoreCase)) { await writer.WriteLineAsync("221 bye"); return; }
                else await writer.WriteLineAsync("250 ok");
            }
        }

        [Fact]
        [Trait("Rule", "SH28")]
        public async Task SendEmailAsync_SendSucceeds_NoLogLineContainsTheRecipientAddress()
        {
            // Arrange
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var server = RunFakeSmtpServerAsync(listener);
            var logger = new CapturingLogger();
            var sut = CreateSut(logger, ((IPEndPoint)listener.LocalEndpoint).Port);

            // Act
            await sut.SendEmailAsync(Recipient, "Reset your password", "<p>Your code is 482913</p>");
            await server.WaitAsync(TimeSpan.FromSeconds(10));
            listener.Stop();

            // Assert
            logger.Messages.ShouldContain(m => m.Contains("Email sent successfully"), customMessage: "the send must have reached the success path for this test to mean anything");
            logger.Messages.ShouldNotContain(m => m.Contains(Recipient), customMessage: "email addresses are personal data and must never be written to the log");
        }

        [Fact]
        [Trait("Rule", "SH28")]
        public async Task SendEmailAsync_SendFails_NoLogLineContainsTheRecipientAddress()
        {
            // Arrange: nothing listens on the port, so connecting fails
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var closedPort = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            var logger = new CapturingLogger();
            var sut = CreateSut(logger, closedPort);

            // Act
            await Should.ThrowAsync<Exception>(() => sut.SendEmailAsync(Recipient, "Reset your password", "<p>Your code is 482913</p>"));

            // Assert
            logger.Messages.ShouldNotBeEmpty("the failure must be logged");
            logger.Messages.ShouldNotContain(m => m.Contains(Recipient), customMessage: "a failed send must not write the recipient's email address to the log");
        }
    }
}
