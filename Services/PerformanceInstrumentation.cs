using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Label_CRM_demo.Services;

internal static class PerformanceInstrumentation
{
    private static readonly Stopwatch SessionStopwatch = Stopwatch.StartNew();

    public static TimedOperation Measure(string operation, params (string Key, object? Value)[] metadata)
        => new TimedOperation(operation, metadata);

    public static void Log(string operation, params (string Key, object? Value)[] metadata)
        => Write(operation, null, metadata);

    private static void Write(string operation, TimeSpan? elapsed, (string Key, object? Value)[] metadata)
    {
        var messageBuilder = new StringBuilder(160);
        messageBuilder.Append("[Perf] ");
        messageBuilder.Append(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture));
        messageBuilder.Append(" sessionMs=");
        messageBuilder.Append(SessionStopwatch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture));
        messageBuilder.Append(' ');
        messageBuilder.Append(operation);

        if (elapsed.HasValue)
        {
            messageBuilder.Append(" elapsedMs=");
            messageBuilder.Append(elapsed.Value.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture));
        }

        foreach (var (key, value) in metadata)
        {
            if (string.IsNullOrWhiteSpace(key) || value is null)
            {
                continue;
            }

            messageBuilder.Append(' ');
            messageBuilder.Append(key);
            messageBuilder.Append('=');
            messageBuilder.Append(Sanitize(value));
        }

        var message = messageBuilder.ToString();
        Trace.WriteLine(message);
        Debug.WriteLine(message);
    }

    private static string Sanitize(object value)
    {
        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        return text
            .Replace("\r\n", "|", StringComparison.Ordinal)
            .Replace('\n', '|')
            .Replace(' ', '_');
    }

    internal sealed class TimedOperation : IDisposable
    {
        private readonly string operation;
        private readonly Stopwatch stopwatch = Stopwatch.StartNew();
        private readonly (string Key, object? Value)[] baseMetadata;
        private bool isDisposed;

        internal TimedOperation(string operation, (string Key, object? Value)[] baseMetadata)
        {
            this.operation = operation;
            this.baseMetadata = baseMetadata;
        }

        public void Checkpoint(string checkpoint, params (string Key, object? Value)[] metadata)
            => Write(operation + "." + checkpoint, stopwatch.Elapsed, CombineMetadata(metadata));

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            stopwatch.Stop();
            Write(operation, stopwatch.Elapsed, baseMetadata);
        }

        private (string Key, object? Value)[] CombineMetadata((string Key, object? Value)[] metadata)
        {
            if (metadata.Length == 0)
            {
                return baseMetadata;
            }

            if (baseMetadata.Length == 0)
            {
                return metadata;
            }

            var combined = new (string Key, object? Value)[baseMetadata.Length + metadata.Length];
            baseMetadata.CopyTo(combined, 0);
            metadata.CopyTo(combined, baseMetadata.Length);
            return combined;
        }
    }
}