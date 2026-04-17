namespace WinRecorder.Models;

public sealed record UiEvent
{
    public UiEvent(
        DateTimeOffset timestamp,
        UiEventType type,
        string? processName,
        string? windowTitle,
        string eventCode,
        string? details)
    {
        Timestamp = timestamp;
        Type = type;
        ProcessName = processName;
        WindowTitle = windowTitle;
        EventCode = eventCode;
        Details = details;
    }

    public DateTimeOffset Timestamp { get; }
    public UiEventType Type { get; }
    public string? ProcessName { get; }
    public string? WindowTitle { get; }

    // Example: "mouse:leftClick", "key:Ctrl+K"
    public string EventCode { get; }

    // Free-form extra that EntryFormatter will render after EventCode.
    // Example: "text=\"a\"", "x=120 y=310", "button=left".
    public string? Details { get; }
}

