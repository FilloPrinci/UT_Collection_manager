using Serilog.Core;
using Serilog.Events;

namespace UTLauncher.Core.Logging;

public sealed class InMemorySink(int capacity = 5000) : ILogEventSink
{
    private readonly object _gate = new();
    private readonly Queue<LogEntry> _entries = new();

    public event EventHandler<LogEntry>? EntryWritten;

    public void Emit(LogEvent logEvent)
    {
        var entry = new LogEntry(
            logEvent.Timestamp,
            logEvent.Level.ToString(),
            logEvent.RenderMessage(),
            logEvent.Exception?.ToString());

        lock (_gate)
        {
            _entries.Enqueue(entry);
            while (_entries.Count > capacity)
            {
                _entries.Dequeue();
            }
        }

        EntryWritten?.Invoke(this, entry);
    }

    public IReadOnlyList<LogEntry> Snapshot()
    {
        lock (_gate)
        {
            return [.. _entries];
        }
    }
}
