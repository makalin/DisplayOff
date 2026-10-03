namespace DisplayOff;

internal sealed class SettingsForm : Form
{
    private readonly CaptureBox _hotkeyBox;
    private readonly Label _hint;
    private readonly CheckBox _startupCheck;
    private readonly ComboBox _delayCombo;
    private int _modifiers;
    private int _virtualKey;

    public SettingsForm(AppSettings draft, Icon icon)
    {
        Settings = draft;
        _modifiers = draft.Modifiers;
        _virtualKey = draft.VirtualKey;

        Text = "DisplayOff Settings";
        Icon = (Icon)icon.Clone();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = true;
        ShowIcon = true;
        TopMost = true;
        AutoScaleMode = AutoScaleMode.Font;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(16, 14, 16, 14);

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 4,
            Dock = DockStyle.Top,
            Margin = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
        for (int row = 0; row < 4; row++)
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var hotkeyLabel = new Label
        {
            Text = "Hotkey:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 8, 12, 0)
        };

        _hotkeyBox = new CaptureBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 0),
            ReadOnly = false,
            ShortcutsEnabled = false,
            TabStop = true,
            Text = HotkeyText.Format(_modifiers, _virtualKey),
            Cursor = Cursors.Hand
        };
        _hotkeyBox.HotkeyCaptured += OnHotkeyCaptured;
        _hotkeyBox.ModifierRequired += OnModifierRequired;

        _hint = new Label
        {
            Text = "Click the box and press a shortcut.",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 4, 0, 8),
            Dock = DockStyle.Fill
        };

        _startupCheck = new CheckBox
        {
            Text = "Start with Windows",
            AutoSize = true,
            Checked = draft.StartWithWindows,
            Margin = new Padding(0, 4, 0, 8)
        };

        var delayLabel = new Label
        {
            Text = "Display off delay:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 8, 12, 0)
        };

        _delayCombo = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0, 4, 0, 0)
        };
        foreach (DisplayOffDelay delay in Enum.GetValues<DisplayOffDelay>())
            _delayCombo.Items.Add(new DelayChoice(delay));

        _delayCombo.SelectedItem = new DelayChoice(draft.Delay);
        if (_delayCombo.SelectedIndex < 0)
            _delayCombo.SelectedIndex = 0;

        var save = new Button
        {
            Text = "Save",
            AutoSize = true,
            MinimumSize = new Size(84, 28),
            Margin = new Padding(0, 16, 0, 0)
        };
        save.Click += (_, _) => SaveAndClose();

        var buttonRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Dock = DockStyle.Fill,
            WrapContents = false,
            Margin = Padding.Empty
        };
        buttonRow.Controls.Add(save);

        layout.Controls.Add(hotkeyLabel, 0, 0);
        layout.Controls.Add(_hotkeyBox, 1, 0);
        layout.Controls.Add(_hint, 0, 1);
        layout.SetColumnSpan(_hint, 2);
        layout.Controls.Add(_startupCheck, 0, 2);
        layout.SetColumnSpan(_startupCheck, 2);
        layout.Controls.Add(delayLabel, 0, 3);
        layout.Controls.Add(_delayCombo, 1, 3);

        var stack = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty
        };
        stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        stack.Controls.Add(layout, 0, 0);
        stack.Controls.Add(buttonRow, 0, 1);

        Controls.Add(stack);
        AcceptButton = save;
        ActiveControl = _hotkeyBox;
    }

    public AppSettings Settings { get; }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
            Close();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void OnHotkeyCaptured(int modifiers, int virtualKey)
    {
        _modifiers = modifiers;
        _virtualKey = virtualKey;
        _hotkeyBox.Text = HotkeyText.Format(modifiers, virtualKey);
        _hint.Text = "Click the box and press a shortcut.";
    }

    private void OnModifierRequired()
    {
        _hint.Text = "Include Ctrl, Alt, or Shift.";
    }

    private void SaveAndClose()
    {
        if (_modifiers == 0 || _virtualKey is < 1 or > 255)
        {
            MessageBox.Show(this, "Choose a hotkey first.", "DisplayOff", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Settings.Modifiers = _modifiers;
        Settings.VirtualKey = _virtualKey;
        Settings.StartWithWindows = _startupCheck.Checked;
        Settings.Delay = _delayCombo.SelectedItem is DelayChoice choice
            ? choice.Delay
            : DisplayOffDelay.Immediately;
        DialogResult = DialogResult.OK;
        Close();
    }

    private readonly record struct DelayChoice(DisplayOffDelay Delay)
    {
        public override string ToString() => Delay.ToLabel();
    }

    private sealed class CaptureBox : TextBox
    {
        private const int WmPaste = 0x0302;

        public event Action<int, int>? HotkeyCaptured;

        public event Action? ModifierRequired;

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys code = keyData & Keys.KeyCode;
            Keys modifiers = keyData & Keys.Modifiers;
            bool navigate = code is Keys.Tab;
            bool save = code is Keys.Enter && modifiers == Keys.None;
            bool close = keyData == Keys.Escape;
            if (navigate || save || close)
                return base.ProcessCmdKey(ref msg, keyData);

            return false;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            e.SuppressKeyPress = true;
            e.Handled = true;

            HotkeyInput input = HotkeyText.Read(e);
            if (input.Captured)
                HotkeyCaptured?.Invoke(input.Modifiers, input.VirtualKey);
            else if (input.NeedsModifier)
                ModifierRequired?.Invoke();
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            e.Handled = true;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmPaste)
                return;

            base.WndProc(ref m);
        }
    }
}
