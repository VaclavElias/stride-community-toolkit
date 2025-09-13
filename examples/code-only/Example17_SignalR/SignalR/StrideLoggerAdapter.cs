using Microsoft.Extensions.Logging;
using Stride.Core.Diagnostics;

namespace Example17_SignalR.SignalR;

/// <summary>
/// Bridges Microsoft.Extensions.Logging ILogger to Stride's logging system.
/// Messages are forwarded to the provided Stride <see cref="Logger"/>.
/// </summary>
/// <typeparam name="T">Category type for the logger.</typeparam>
public sealed class StrideLoggerAdapter<T> : ILogger<T>
{
    private readonly Logger _strideLogger;

    public StrideLoggerAdapter(Logger strideLogger)
    {
        _strideLogger = strideLogger ?? throw new ArgumentNullException(nameof(strideLogger));
    }

    public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true; // Stride filtering can be done via listeners

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (formatter is null) return;

        var message = formatter(state, exception);
        if (string.IsNullOrEmpty(message) && exception is null) return;

        switch (logLevel)
        {
            case LogLevel.Trace:
            case LogLevel.Debug:
                if (exception is null) _strideLogger.Debug(message);
                else _strideLogger.Debug($"{message}\n{exception}");
                break;
            case LogLevel.Information:
                if (exception is null) _strideLogger.Info(message);
                else _strideLogger.Info($"{message}\n{exception}");
                break;
            case LogLevel.Warning:
                if (exception is null) _strideLogger.Warning(message);
                else _strideLogger.Warning($"{message}\n{exception}");
                break;
            case LogLevel.Error:
                if (exception is null) _strideLogger.Error(message);
                else _strideLogger.Error($"{message}\n{exception}");
                break;
            case LogLevel.Critical:
                if (exception is null) _strideLogger.Fatal(message);
                else _strideLogger.Fatal($"{message}\n{exception}");
                break;
            case LogLevel.None:
            default:
                break;
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}
