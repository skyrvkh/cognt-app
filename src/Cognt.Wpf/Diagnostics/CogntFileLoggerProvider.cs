using System.Globalization;
using System.IO;
using Microsoft.Extensions.Logging;

namespace CogntApp.Diagnostics;

internal sealed class CogntFileLoggerProvider : ILoggerProvider
{
    private readonly CogntFileLogWriter writer;

    internal CogntFileLoggerProvider(string filePath)
    {
        writer = new CogntFileLogWriter(filePath);
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new CogntFileLogger(categoryName, writer);
    }

    public void Dispose()
    {
        writer.Dispose();
    }

    private sealed class CogntFileLogger(string categoryName, CogntFileLogWriter writer) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

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

            string message = formatter(state, exception);
            writer.Write(logLevel, categoryName, eventId, message, exception);
        }
    }

    private sealed class CogntFileLogWriter : IDisposable
    {
        private readonly object syncRoot = new();
        private readonly StreamWriter writer;
        private bool disposed;

        internal CogntFileLogWriter(string filePath)
        {
            string? directory = System.IO.Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            writer = new StreamWriter(new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            {
                AutoFlush = true,
            };
        }

        internal void Write(
            LogLevel logLevel,
            string categoryName,
            EventId eventId,
            string message,
            Exception? exception)
        {
            lock (syncRoot)
            {
                if (disposed)
                {
                    return;
                }

                string timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture);
                string eventText = eventId.Id == 0 && string.IsNullOrWhiteSpace(eventId.Name)
                    ? string.Empty
                    : $" [{eventId.Id}:{eventId.Name}]";

                writer.Write(timestamp);
                writer.Write(" ");
                writer.Write(logLevel.ToString().ToUpperInvariant().PadRight(11));
                writer.Write(" ");
                writer.Write(categoryName);
                writer.Write(eventText);
                writer.Write(" - ");
                writer.WriteLine(message);

                if (exception is not null)
                {
                    writer.WriteLine(exception);
                }
            }
        }

        public void Dispose()
        {
            lock (syncRoot)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                writer.Dispose();
            }
        }
    }
}
