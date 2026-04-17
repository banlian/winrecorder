using System.Runtime.InteropServices;
using System.Text;
using WinRecorder.Win32;

namespace WinRecorder.Services;

public sealed class KeyboardTextTranslator
{
    public string? TranslateToText(uint vkCode)
    {
        var keyboardState = new byte[256];
        if (!GetKeyboardState(keyboardState))
            return null;

        var hkl = GetKeyboardLayout(0);

        // ToUnicodeEx expects scan code computed from vkCode.
        var scanCode = MapVirtualKey(vkCode, 0);
        var sb = new StringBuilder(16);

        var result = ToUnicodeEx(
            wVirtKey: vkCode,
            wScanCode: scanCode,
            lpKeyState: keyboardState,
            pwszBuff: sb,
            cchBuff: sb.Capacity,
            wFlags: 0,
            dwhkl: hkl);

        // 1..n: successful translation length
        if (result > 0)
            return sb.ToString(0, result);

        // -1: dead key; best-effort ignore.
        return null;
    }

    public static string EscapeTextForMarkdown(string text)
    {
        if (text.Length == 0)
            return text;

        return text
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("\"", "\\\"");
    }

    [DllImport("user32.dll")]
    private static extern bool GetKeyboardState(byte[] lpKeyState);

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint idThread);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ToUnicodeEx(
        uint wVirtKey,
        uint wScanCode,
        byte[] lpKeyState,
        [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwszBuff,
        int cchBuff,
        uint wFlags,
        IntPtr dwhkl);
}

