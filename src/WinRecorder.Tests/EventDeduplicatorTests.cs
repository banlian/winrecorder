using WinRecorder.App;
using WinRecorder.Models;

namespace WinRecorder.Tests;

public static class EventDeduplicatorTests
{
    public static void RunAll()
    {
        Suppress_AdjacentDuplicateMouseEvents();
        Keep_MouseEvents_WhenContentChanges();
        Keep_NonMouseEvents_EvenWhenSameContent();
    }

    private static void Suppress_AdjacentDuplicateMouseEvents()
    {
        var dedup = new EventDeduplicator();
        var ts = new DateTimeOffset(2026, 4, 25, 21, 40, 40, 0, TimeSpan.Zero);
        var ev = CreateMouseEvent(ts, "chrome", "mdkoss", "mouse:leftClick", "target=\"Chrome Legacy Window\" class=Chrome_RenderWidgetHostHWND");

        AssertTrue(dedup.ShouldEmit(ev), "first duplicate candidate should pass");
        AssertFalse(dedup.ShouldEmit(ev), "adjacent duplicate should be suppressed");
    }

    private static void Keep_MouseEvents_WhenContentChanges()
    {
        var dedup = new EventDeduplicator();
        var ts = new DateTimeOffset(2026, 4, 25, 21, 40, 40, 0, TimeSpan.Zero);
        var ev1 = CreateMouseEvent(ts, "chrome", "mdkoss", "mouse:leftClick", "target=\"A\" class=Chrome_RenderWidgetHostHWND");
        var ev2 = CreateMouseEvent(ts.AddMilliseconds(10), "chrome", "mdkoss", "mouse:leftClick", "target=\"B\" class=Chrome_RenderWidgetHostHWND");

        AssertTrue(dedup.ShouldEmit(ev1), "first mouse event should pass");
        AssertTrue(dedup.ShouldEmit(ev2), "changed mouse details should pass");
    }

    private static void Keep_NonMouseEvents_EvenWhenSameContent()
    {
        var dedup = new EventDeduplicator();
        var ts = new DateTimeOffset(2026, 4, 25, 21, 40, 40, 0, TimeSpan.Zero);
        var ev = new UiEvent(
            timestamp: ts,
            type: UiEventType.ForegroundChanged,
            processName: "chrome",
            windowTitle: "mdkoss",
            eventCode: "foreground",
            details: null);

        AssertTrue(dedup.ShouldEmit(ev), "first non-mouse event should pass");
        AssertTrue(dedup.ShouldEmit(ev), "duplicate non-mouse event should still pass");
    }

    private static UiEvent CreateMouseEvent(
        DateTimeOffset ts,
        string processName,
        string title,
        string eventCode,
        string details)
    {
        return new UiEvent(
            timestamp: ts,
            type: UiEventType.Mouse,
            processName: processName,
            windowTitle: title,
            eventCode: eventCode,
            details: details);
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new InvalidOperationException("AssertTrue failed: " + message);
    }

    private static void AssertFalse(bool value, string message)
    {
        if (value)
            throw new InvalidOperationException("AssertFalse failed: " + message);
    }
}
