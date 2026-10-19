using Microsoft.Extensions.Logging;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>Keeps every log line (message, level and exception text) so tests can check what reached the logs.</summary>
    public sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = new();

        public List<LogLevel> Levels { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Levels.Add(logLevel);
            Messages.Add(formatter(state, exception) + " " + exception?.ToString());
        }
    }
}
