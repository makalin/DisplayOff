namespace DisplayOff;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _startupItem;
    private readonly HotkeyManager _hotkeys;
    private readonly Icon _formIcon;
    private AppSettings _settings;
    private int _turnOffRequest;
    private bool _exiting;

    public TrayApplicationContext()
    {
        _settings = SettingsManager.Load();
        bool startupApplied = StartupManager.TryApply(_settings.StartWithWindows, out string? startupError);

        _formIcon = AppIcon.Load(32);
        Icon trayIcon = AppIcon.Load(Math.Max(16, SystemInformation.SmallIconSize.Width));

        _menu = new ContextMenuStrip();
        var header = new ToolStripLabel("DisplayOff");
        var turnOff = new ToolStripMenuItem("Turn Display Off", null, (_, _) => ScheduleTurnOff(fromPointer: true));
        var settingsItem = new ToolStripMenuItem("Settings", null, (_, _) => ShowSettings());
        _startupItem = new ToolStripMenuItem("Start with Windows", null, (_, _) => ToggleStartup())
        {
            Checked = _settings.StartWithWindows
        };
        var exit = new ToolStripMenuItem("Exit", null, (_, _) => Exit());

        _menu.Items.Add(header);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(turnOff);
        _menu.Items.Add(settingsItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(_startupItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(exit);

        _notifyIcon = new NotifyIcon
        {
            Icon = trayIcon,
            Text = HotkeyText.TrayTip(_settings.Modifiers, _settings.VirtualKey),
            Visible = true,
            ContextMenuStrip = _menu
        };
        _notifyIcon.DoubleClick += (_, _) => ScheduleTurnOff(fromPointer: true);

        _hotkeys = new HotkeyManager();
        _hotkeys.Pressed += () => ScheduleTurnOff(fromPointer: false);
        if (!_hotkeys.TryRegister(_settings.Modifiers, _settings.VirtualKey, out _))
        {
            _notifyIcon.ShowBalloonTip(
                4000,
                "DisplayOff",
                "The shortcut couldn't be registered. Open Settings to choose another.",
                ToolTipIcon.Warning);
        }

        if (!startupApplied)
        {
            _notifyIcon.ShowBalloonTip(
                4000,
                "DisplayOff",
                startupError ?? "Couldn't update Start with Windows.",
                ToolTipIcon.Warning);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _exiting = true;
            _turnOffRequest++;
            _hotkeys.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _menu.Dispose();
            _formIcon.Dispose();
        }

        base.Dispose(disposing);
    }

    private void Exit()
    {
        _exiting = true;
        _turnOffRequest++;
        ExitThread();
    }

    private async void ScheduleTurnOff(bool fromPointer)
    {
        int request = ++_turnOffRequest;
        int delay = DisplayManager.ResolveDelay(_settings.Delay, fromPointer);

        try
        {
            if (delay > 0)
                await Task.Delay(delay).ConfigureAwait(true);
        }
        catch (Exception)
        {
            return;
        }

        if (request != _turnOffRequest || _exiting)
            return;

        DisplayManager.TurnOff();
    }

    private void ShowSettings()
    {
        _hotkeys.Unregister();

        using var form = new SettingsForm(_settings.Copy(), _formIcon);
        if (form.ShowDialog() != DialogResult.OK)
        {
            RestoreHotkey();
            return;
        }

        AppSettings updated = form.Settings;
        if (!StartupManager.TryApply(updated.StartWithWindows, out string? startupError))
        {
            MessageBox.Show(startupError, "DisplayOff", MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
            RestoreHotkey();
            return;
        }

        if (!_hotkeys.TryRegister(updated.Modifiers, updated.VirtualKey, out string? hotkeyError))
        {
            MessageBox.Show(hotkeyError, "DisplayOff", MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
            StartupManager.TryApply(_settings.StartWithWindows, out _);
            RestoreHotkey();
            return;
        }

        if (!SettingsManager.TrySave(updated, out string? saveError))
        {
            MessageBox.Show(saveError, "DisplayOff", MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
            StartupManager.TryApply(_settings.StartWithWindows, out _);
            _hotkeys.Unregister();
            RestoreHotkey();
            return;
        }

        _settings = updated;
        UpdateAppearance();
    }

    private void ToggleStartup()
    {
        bool enabled = !_settings.StartWithWindows;
        if (!StartupManager.TryApply(enabled, out string? error))
        {
            MessageBox.Show(error, "DisplayOff", MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
            return;
        }

        bool previous = _settings.StartWithWindows;
        _settings.StartWithWindows = enabled;
        if (!SettingsManager.TrySave(_settings, out error))
        {
            _settings.StartWithWindows = previous;
            StartupManager.TryApply(previous, out _);
            MessageBox.Show(error, "DisplayOff", MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
            return;
        }

        _startupItem.Checked = enabled;
    }

    private void RestoreHotkey()
    {
        if (_hotkeys.TryRegister(_settings.Modifiers, _settings.VirtualKey, out _))
            return;

        _notifyIcon.ShowBalloonTip(
            4000,
            "DisplayOff",
            "The shortcut couldn't be registered. Open Settings to choose another.",
            ToolTipIcon.Warning);
    }

    private void UpdateAppearance()
    {
        _notifyIcon.Text = HotkeyText.TrayTip(_settings.Modifiers, _settings.VirtualKey);
        _startupItem.Checked = _settings.StartWithWindows;
    }
}

internal static class AppIcon
{
    private const string ResourceName = "DisplayOff.Assets.DisplayOff.ico";

    public static Icon Load(int size)
    {
        try
        {
            using Stream? stream = typeof(AppIcon).Assembly.GetManifestResourceStream(ResourceName);
            if (stream is not null)
                return new Icon(stream, new Size(size, size));
        }
        catch (ArgumentException)
        {
        }

        return (Icon)SystemIcons.Application.Clone();
    }
}
