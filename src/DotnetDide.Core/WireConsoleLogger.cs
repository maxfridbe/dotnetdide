using Microsoft.Extensions.Logging;

namespace DotnetDide;

// Synchronous output stays inside the request/submission capture lifetime in WASM.
internal sealed class WireConsoleLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new WireConsoleLogger(categoryName);
    public void Dispose() { }

    private sealed class WireConsoleLogger(string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            var writer = logLevel >= LogLevel.Warning ? Console.Error : Console.Out;
            writer.WriteLine($"[{logLevel}] {category} ({eventId.Id}): {formatter(state, exception)}");
            if (exception is not null) writer.WriteLine(exception);
        }
    }
}
