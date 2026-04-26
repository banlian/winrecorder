using WinRecorder.Models;

namespace WinRecorder.App;

public sealed class EventDeduplicator
{
    private readonly object _gate = new();
    private EventKey? _lastMouseEvent;

    public bool ShouldEmit(UiEvent ev)
    {
        // Keep existing behavior for non-mouse events.
        if (ev.Type != UiEventType.Mouse)
            return true;

        var current = EventKey.From(ev);

        lock (_gate)
        {
            if (_lastMouseEvent is not null && _lastMouseEvent.Equals(current))
                return false;

            _lastMouseEvent = current;
            return true;
        }
    }

    private sealed record EventKey(
        UiEventType Type,
        string ProcessName,
        string WindowTitle,
        string EventCode,
        string Details)
    {
        public static EventKey From(UiEvent ev)
        {
            return new EventKey(
                Type: ev.Type,
                ProcessName: (ev.ProcessName ?? string.Empty).Trim().ToLowerInvariant(),
                WindowTitle: ev.WindowTitle ?? string.Empty,
                EventCode: ev.EventCode ?? string.Empty,
                Details: ev.Details ?? string.Empty);
        }
    }
}
