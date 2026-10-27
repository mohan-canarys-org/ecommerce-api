using System.Text;
using Microsoft.Extensions.Logging;

namespace ecommerce_api.Logging;

public sealed class DailyFileLoggerProvider : ILoggerProvider
{
    private readonly object _sync = new();
    private readonly string _directory;
    private StreamWriter? _writer;
    private DateOnly? _currentDate;
    private bool _disposed;

    public DailyFileLoggerProvider(string? directory = null)
    {
        _directory = directory ?? Path.Combine(Directory.GetCurrentDirectory(), "Logs");
    }

    public ILogger CreateLogger(string categoryName) => new DailyFileLogger(this, categoryName);

    internal void Write(string category, LogLevel logLevel, string message, Exception? exception)
    {
        var timestamp = DateTimeOffset.Now;
        var date = DateOnly.FromDateTime(timestamp.DateTime);

        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            if (_currentDate != date)
            {
                _writer?.Dispose();
                Directory.CreateDirectory(_directory);
                var filePath = Path.Combine(_directory, $"application-{date:yyyy-MM-dd}.txt");
                _writer = new StreamWriter(
                    new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
                {
                    AutoFlush = true
                };
                _currentDate = date;
            }

            _writer!.WriteLine($"{timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{logLevel}] {category}: {message}");
            if (exception is not null)
            {
                _writer.WriteLine(exception);
            }
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _writer?.Dispose();
            _writer = null;
            _disposed = true;
        }
    }

    private sealed class DailyFileLogger : ILogger
    {
        private readonly DailyFileLoggerProvider _provider;
        private readonly string _category;

        public DailyFileLogger(DailyFileLoggerProvider provider, string category)
        {
            _provider = provider;
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            ArgumentNullException.ThrowIfNull(formatter);
            _provider.Write(_category, logLevel, formatter(state, exception), exception);
        }
    }
}
