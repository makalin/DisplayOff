# DisplayOff

A tiny, lightweight Windows utility that turns off the notebook display **without putting the computer to sleep**.

The application should be designed primarily for an HP Windows 11 notebook but should work on standard Windows 10/11 systems.

## Goal

I currently use an HDMI/display switch to turn the notebook screen off while keeping the computer running.

DisplayOff should replace this physical switch.

When activated:

- Turn the display off immediately.
- DO NOT put Windows to sleep.
- DO NOT hibernate.
- DO NOT lock Windows.
- DO NOT suspend running applications.
- Network connections must remain active.
- Background applications must continue running.
- Downloads, servers, Ollama/local AI models, etc. must continue running.
- Normal Windows mouse/keyboard activity should wake the display again.

## Technology

Use:

- C#
- .NET 8
- WinForms
- Native Windows API / PInvoke

Keep the application extremely small and lightweight.

Do NOT use:

- Electron
- WebView
- external UI frameworks
- unnecessary NuGet dependencies

The final application should be publishable as a single Windows `.exe`.

## Core Display-Off Function

Use the Windows API:

```csharp
[DllImport("user32.dll")]
static extern IntPtr SendMessage(
    IntPtr hWnd,
    uint Msg,
    IntPtr wParam,
    IntPtr lParam
);

const int HWND_BROADCAST = 0xFFFF;
const int WM_SYSCOMMAND = 0x0112;
const int SC_MONITORPOWER = 0xF170;
```

Turn the monitor off with:

```csharp
SendMessage(
    (IntPtr)HWND_BROADCAST,
    WM_SYSCOMMAND,
    (IntPtr)SC_MONITORPOWER,
    (IntPtr)2
);
```

The computer itself must remain fully awake.

## System Tray

The application should normally run only in the Windows notification area.

No main window is necessary.

Tray menu:

```text
DisplayOff
──────────────
Turn Display Off
Settings
──────────────
Start with Windows  ✓
──────────────
Exit
```

Double-clicking the tray icon should immediately turn the display off.

## Global Hotkey

Default global shortcut:

```text
Ctrl + Alt + F12
```

Pressing it from anywhere in Windows should turn the display off.

Use the Windows `RegisterHotKey()` API.

The shortcut should work even when another application is active.

Allow changing the shortcut from Settings.

## Settings

Create a very small native Windows settings window.

Options:

```text
DisplayOff Settings

Hotkey:
[ Ctrl + Alt + F12 ]

[✓] Start with Windows

Display off delay:
[ Immediately ▼ ]

      Save
```

Delay options:

```text
Immediately
3 seconds
5 seconds
10 seconds
```

The delay is useful when activating DisplayOff with the mouse so mouse movement immediately after clicking does not wake the display again.

## Important Wake Behaviour

After the monitor has been turned off:

- Mouse movement should wake it.
- Mouse click should wake it.
- Keyboard input should wake it.

Do not implement custom wake handling unless Windows requires it.

Let Windows handle normal monitor wake behaviour.

## Prevent Accidental Immediate Wake

When DisplayOff is triggered from the tray icon, wait approximately 500–1000 ms before sending `SC_MONITORPOWER`.

This prevents the mouse click used to activate DisplayOff from immediately waking the screen again.

For example:

```csharp
await Task.Delay(750);
TurnDisplayOff();
```

## Start With Windows

Implement optional automatic startup.

Prefer the standard current-user Windows startup mechanism.

No administrator privileges should be required.

For example:

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
```

Do not install a Windows service.

## Single Instance

Only one DisplayOff process should run.

If the user launches the application again while it is already running, do not create another tray icon.

Use a named Mutex or another simple Windows single-instance mechanism.

## Resource Usage

The program should be essentially idle while running.

Target:

```text
CPU: ~0%
RAM: minimal
Network: none
Disk activity: none while idle
```

No polling loops.

Use Windows events/messages instead.

## Application Icon

Create a minimal icon representing:

```text
monitor + power/sleep symbol
```

Simple monochrome design suitable for the Windows system tray.

Include:

```text
16x16
20x20
24x24
32x32
48x48
256x256
```

Provide an `.ico` containing the necessary sizes.

## Project Structure

Keep the project simple:

```text
DisplayOff/
│
├── DisplayOff.sln
├── DisplayOff/
│   ├── Program.cs
│   ├── DisplayManager.cs
│   ├── HotkeyManager.cs
│   ├── StartupManager.cs
│   ├── SettingsManager.cs
│   ├── TrayApplicationContext.cs
│   ├── SettingsForm.cs
│   ├── NativeMethods.cs
│   ├── Assets/
│   │   └── DisplayOff.ico
│   └── DisplayOff.csproj
│
├── README.md
└── LICENSE
```

## Publishing

Configure the project so it can be built as a single self-contained Windows executable.

Example:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Output should effectively be:

```text
DisplayOff.exe
```

No installer should be required for the portable version.

## Optional Command-Line Support

Also support:

```bash
DisplayOff.exe --off
```

This should turn the display off and exit without starting the tray application.

This allows DisplayOff to be called from scripts, Stream Deck, shortcuts, Task Scheduler, etc.

## Future Features

Keep the architecture simple enough to later add:

```text
Turn display off
Lock + display off
Display off after X minutes
Disable mouse wake
Keyboard-only wake
Multiple monitor selection
External monitor control
DDC/CI brightness control
```

Do not implement these features in the first version.

## UX Principle

DisplayOff should feel like a native Windows utility, not a full application.

The normal workflow should be:

```text
Ctrl + Alt + F12
        ↓
Display turns off
        ↓
Computer continues running normally
        ↓
Move mouse / press key
        ↓
Display instantly returns
```

No dialogs, confirmations, notifications, or unnecessary UI should appear when turning the display off.