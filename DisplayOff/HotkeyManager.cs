using System.Runtime.InteropServices;

namespace DisplayOff;

internal readonly struct HotkeyInput
{
    public bool Captured { get; init; }

    public bool NeedsModifier { get; init; }

    public int Modifiers { get; init; }

    public int VirtualKey { get; init; }
}

internal static class HotkeyText
{
    public static string Format(int modifiers, int virtualKey)
    {
        var parts = new List<string>(5);
        if ((modifiers & NativeMethods.MOD_CONTROL) != 0)
            parts.Add("Ctrl");
        if ((modifiers & NativeMethods.MOD_ALT) != 0)
            parts.Add("Alt");
        if ((modifiers & NativeMethods.MOD_SHIFT) != 0)
            parts.Add("Shift");
        if ((modifiers & NativeMethods.MOD_WIN) != 0)
            parts.Add("Win");

        parts.Add(FormatKey((Keys)virtualKey));
        return string.Join(" + ", parts);
    }

    public static string TrayTip(int modifiers, int virtualKey)
    {
        string tip = "DisplayOff (" + Format(modifiers, virtualKey) + ")";
        return tip.Length <= 63 ? tip : tip[..63];
    }

    public static HotkeyInput Read(KeyEventArgs e)
    {
        if (IsModifier(e.KeyCode) || e.KeyCode is Keys.None or Keys.Packet)
            return default;

        int modifiers = 0;
        if (e.Control)
            modifiers |= (int)NativeMethods.MOD_CONTROL;
        if (e.Alt)
            modifiers |= (int)NativeMethods.MOD_ALT;
        if (e.Shift)
            modifiers |= (int)NativeMethods.MOD_SHIFT;

        if (modifiers == 0)
            return new HotkeyInput { NeedsModifier = true };

        int virtualKey = (int)e.KeyCode;
        if (virtualKey is < 1 or > 255)
            return default;

        return new HotkeyInput
        {
            Captured = true,
            Modifiers = modifiers,
            VirtualKey = virtualKey
        };
    }

    private static bool IsModifier(Keys key) => key is
        Keys.ControlKey or Keys.LControlKey or Keys.RControlKey or
        Keys.Menu or Keys.LMenu or Keys.RMenu or
        Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey or
        Keys.LWin or Keys.RWin;

    private static string FormatKey(Keys key) => key switch
    {
        Keys.D0 => "0",
        Keys.D1 => "1",
        Keys.D2 => "2",
        Keys.D3 => "3",
        Keys.D4 => "4",
        Keys.D5 => "5",
        Keys.D6 => "6",
        Keys.D7 => "7",
        Keys.D8 => "8",
        Keys.D9 => "9",
        Keys.NumPad0 => "Num 0",
        Keys.NumPad1 => "Num 1",
        Keys.NumPad2 => "Num 2",
        Keys.NumPad3 => "Num 3",
        Keys.NumPad4 => "Num 4",
        Keys.NumPad5 => "Num 5",
        Keys.NumPad6 => "Num 6",
        Keys.NumPad7 => "Num 7",
        Keys.NumPad8 => "Num 8",
        Keys.NumPad9 => "Num 9",
        Keys.Oemcomma => ",",
        Keys.OemPeriod => ".",
        Keys.OemMinus => "-",
        Keys.Oemplus => "=",
        Keys.OemQuestion => "/",
        Keys.OemSemicolon => ";",
        Keys.OemQuotes => "'",
        Keys.OemOpenBrackets => "[",
        Keys.OemCloseBrackets => "]",
        Keys.OemPipe => "\\",
        Keys.Oemtilde => "`",
        Keys.Next => "Page Down",
        Keys.Prior => "Page Up",
        Keys.Capital => "Caps Lock",
        Keys.Scroll => "Scroll Lock",
        Keys.PrintScreen => "Print Screen",
        Keys.Back => "Backspace",
        Keys.Return => "Enter",
        Keys.Space => "Space",
        Keys.Escape => "Esc",
        Keys.Delete => "Delete",
        Keys.Insert => "Insert",
        Keys.Apps => "Menu",
        _ => key.ToString()
    };
}

internal sealed class HotkeyManager : NativeWindow, IDisposable
{
    private const int HotkeyId = 1;
    private const int HwndMessage = -3;

    private bool _registered;
    private bool _disposed;

    public event Action? Pressed;

    public HotkeyManager()
    {
        CreateHandle(new CreateParams
        {
            Caption = "DisplayOff",
            Parent = (IntPtr)HwndMessage
        });
    }

    public void Unregister()
    {
        if (!_registered || Handle == IntPtr.Zero)
            return;

        NativeMethods.UnregisterHotKey(Handle, HotkeyId);
        _registered = false;
    }

    public bool TryRegister(int modifiers, int virtualKey, out string? error)
    {
        error = null;
        Unregister();

        if (Handle == IntPtr.Zero)
        {
            error = "Couldn't create the hotkey window.";
            return false;
        }

        uint flags = (uint)modifiers | NativeMethods.MOD_NOREPEAT;
        if (!NativeMethods.RegisterHotKey(Handle, HotkeyId, flags, (uint)virtualKey))
        {
            int code = Marshal.GetLastWin32Error();
            error = code == NativeMethods.ERROR_HOTKEY_ALREADY_REGISTERED
                ? "That shortcut is already in use. Choose a different one."
                : $"Couldn't register the shortcut (Windows error {code}).";
            return false;
        }

        _registered = true;
        return true;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_HOTKEY && m.WParam == (IntPtr)HotkeyId)
            Pressed?.Invoke();

        base.WndProc(ref m);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Unregister();
        if (Handle != IntPtr.Zero)
            DestroyHandle();
    }
}
