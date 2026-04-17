using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using WinRecorder.Logging;
using WinRecorder.Models;
using WinRecorder.Services;

namespace WinRecorder.Hooks;

public sealed class KeyboardHook : IDisposable
{
    private readonly Action<UiEvent> _onEvent;
    private readonly KeyboardTextTranslator _translator;

    // When true, details may include text="...".
    private readonly bool _captureKeysText;

    private readonly object _gate = new();
    private IntPtr _hookHandle;
    private LowLevelKeyboardProc? _callback;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    // Modifier virtual keys
    private const uint VK_SHIFT = 0x10;
    private const uint VK_CONTROL = 0x11;
    private const uint VK_MENU = 0x12; // Alt
    private const uint VK_LSHIFT = 0xA0;
    private const uint VK_RSHIFT = 0xA1;
    private const uint VK_LCONTROL = 0xA2;
    private const uint VK_RCONTROL = 0xA3;
    private const uint VK_LMENU = 0xA4;
    private const uint VK_RMENU = 0xA5;
    private const uint VK_LWIN = 0x5B;
    private const uint VK_RWIN = 0x5C;

    private const uint LLKHF_REPEAT = 0x4000;

    public KeyboardHook(Action<UiEvent> onEvent, KeyboardTextTranslator translator, bool captureKeysText)
    {
        _onEvent = onEvent;
        _translator = translator;
        _captureKeysText = captureKeysText;
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_hookHandle != IntPtr.Zero)
                return;

            _callback = HookCallback;

            var procModule = Process.GetCurrentProcess().MainModule;
            var moduleName = procModule?.ModuleName;
            var hMod = string.IsNullOrWhiteSpace(moduleName) ? IntPtr.Zero : GetModuleHandle(moduleName);

            _hookHandle = SetWindowsHookEx(13 /* WH_KEYBOARD_LL */, _callback, hMod, 0);
            if (_hookHandle == IntPtr.Zero)
                throw new InvalidOperationException("SetWindowsHookEx(WH_KEYBOARD_LL) failed.");
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (_hookHandle == IntPtr.Zero)
                return;

            UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
            _callback = null;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        IntPtr hhk;
        lock (_gate)
            hhk = _hookHandle;

        try
        {
            if (nCode >= 0)
            {
                var msg = (uint)wParam.ToInt64();
                if (msg == 0x0100 /* WM_KEYDOWN */ || msg == 0x0104 /* WM_SYSKEYDOWN */)
                {
                    var kbd = Marshal.PtrToStructure<WinRecorder.Win32.KBDLLHOOKSTRUCT>(lParam);

                    // Ignore key repeats while holding.
                    if ((kbd.flags & LLKHF_REPEAT) != 0)
                        return CallNextHookEx(hhk, nCode, wParam, lParam);

                    var vkCode = kbd.vkCode;

                    // Skip logging modifier keys alone; their combination will be handled on other keydowns.
                    if (IsModifierKey(vkCode))
                        return CallNextHookEx(hhk, nCode, wParam, lParam);

                    // Fetch current keyboard state for modifier detection.
                    var keyboardState = new byte[256];
                    GetKeyboardState(keyboardState);

                    var modifiers = new List<string>(capacity: 4);
                    if (IsDown(keyboardState, VK_CONTROL)) modifiers.Add("Ctrl");
                    if (IsDown(keyboardState, VK_MENU)) modifiers.Add("Alt");
                    if (IsDown(keyboardState, VK_SHIFT)) modifiers.Add("Shift");
                    if (IsDown(keyboardState, VK_LWIN) || IsDown(keyboardState, VK_RWIN)) modifiers.Add("Win");

                    var keyName = GetKeyName(vkCode);
                    var combo = modifiers.Count == 0 ? keyName : string.Join("+", modifiers) + "+" + keyName;
                    string? text = null;
                    if (_captureKeysText)
                    {
                        try
                        {
                            text = _translator.TranslateToText(vkCode);
                        }
                        catch (Exception tex)
                        {
                            ErrorLog.Write("KeyboardHook.TranslateToText", tex);
                        }
                    }

                    string? details = null;
                    if (_captureKeysText && !string.IsNullOrEmpty(text))
                    {
                        var first = text[0];
                        if (!char.IsControl(first) && modifiers.Count == 0)
                            details = $"text=\"{KeyboardTextTranslator.EscapeTextForMarkdown(text)}\"";
                    }

                    var shortcut = GetShortcutName(vkCode, modifiers);
                    string eventCode;
                    if (shortcut is not null)
                    {
                        eventCode = $"shortcut:{shortcut}";
                    }
                    else if (!string.IsNullOrEmpty(details) && modifiers.Count == 0)
                    {
                        // Raw character input; AppContext will merge consecutive chars into text chunks.
                        eventCode = "input:char";
                    }
                    else
                    {
                        eventCode = $"key:{combo}";
                    }

                    _onEvent(new UiEvent(
                        timestamp: DateTimeOffset.Now,
                        type: UiEventType.Keyboard,
                        processName: null,
                        windowTitle: null,
                        eventCode: eventCode,
                        details: details));
                }
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Write("KeyboardHook", ex);
        }

        return CallNextHookEx(hhk, nCode, wParam, lParam);
    }

    private static bool IsDown(byte[] keyboardState, uint vkCode)
    {
        var v = keyboardState[vkCode];
        return (v & 0x80) != 0;
    }

    private static string GetKeyName(uint vkCode)
    {
        // Keep common keys human-readable for AI.
        return vkCode switch
        {
            0x20 => "Space",
            0x0D => "Enter",
            0x1B => "Esc",
            0x08 => "Backspace",
            0x09 => "Tab",
            0x2E => "Delete",
            _ => ((Keys)vkCode).ToString()
        };
    }

    private static bool IsModifierKey(uint vkCode)
    {
        return vkCode == VK_SHIFT || vkCode == VK_CONTROL || vkCode == VK_MENU ||
               vkCode == VK_LSHIFT || vkCode == VK_RSHIFT ||
               vkCode == VK_LCONTROL || vkCode == VK_RCONTROL ||
               vkCode == VK_LMENU || vkCode == VK_RMENU ||
               vkCode == VK_LWIN || vkCode == VK_RWIN;
    }

    private static string? GetShortcutName(uint vkCode, List<string> modifiers)
    {
        if (modifiers.Count == 0)
            return null;

        var ctrl = modifiers.Contains("Ctrl");
        if (!ctrl)
            return null;

        return vkCode switch
        {
            0x43 => "Copy",      // C
            0x56 => "Paste",     // V
            0x58 => "Cut",       // X
            0x5A => "Undo",      // Z
            0x59 => "Redo",      // Y
            0x41 => "SelectAll", // A
            0x53 => "Save",      // S
            0x46 => "Find",      // F
            _ => null
        };
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }

    private static IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam)
        => NativeCallNextHookEx(hhk, nCode, wParam, lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", EntryPoint = "CallNextHookEx", SetLastError = true)]
    private static extern IntPtr NativeCallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    private static extern bool GetKeyboardState(byte[] lpKeyState);
}

