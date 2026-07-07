using SoundMinimum.Models;
using SoundMinimum.Services;
using SoundMinimum.Themes;

namespace SoundMinimum.Forms;

public class PlaylistEditorForm : Form
{
    private readonly MasterSettingsService _master;
    private readonly ListBox _listBox;
    private readonly Button _addBtn, _removeBtn, _upBtn, _downBtn, _closeBtn;
    private bool _dragTitle;
    private Point _dragStart;

    public PlaylistEditorForm(MasterSettingsService master)
    {
        _master = master;
        Text = Loc.Get("BgEditor_Title");
        Size = new Size(520, 440);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = DarkTheme.BgMain;
        FormBorderStyle = FormBorderStyle.None;
        DoubleBuffered = true;

        Paint += OnPaint;
        MouseDown += OnTitleMouseDown;
        MouseMove += OnTitleMouseMove;
        MouseUp += (_, _) => _dragTitle = false;

        var label = new Label
        {
            Text = Loc.Get("BgEditor_Header"),
            Location = new Point(12, 46),
            Size = new Size(460, 20),
            ForeColor = DarkTheme.FgText,
            BackColor = DarkTheme.BgMain,
            Font = DarkTheme.FontBold
        };
        Controls.Add(label);

        _listBox = new ListBox
        {
            Location = new Point(12, 72),
            Size = new Size(380, 290),
            BackColor = DarkTheme.BgInput,
            ForeColor = DarkTheme.FgText,
            Font = DarkTheme.FontNormal,
            BorderStyle = BorderStyle.FixedSingle,
            IntegralHeight = false,
            AllowDrop = true
        };
        _listBox.DragEnter += (_, e) => { if (e.Data!.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
        _listBox.DragDrop += (_, e) =>
        {
            if (e.Data!.GetData(DataFormats.FileDrop) is string[] files)
                AddTracks(files);
        };
        RefreshList();
        Controls.Add(_listBox);

        _addBtn = new Button { Text = Loc.Get("BgEditor_Add"), Location = new Point(405, 72), Size = new Size(95, 30) };
        _addBtn.Click += (_, _) => AddTrack();
        StyleBtn(_addBtn);
        Controls.Add(_addBtn);

        _removeBtn = new Button { Text = Loc.Get("BgEditor_Del"), Location = new Point(405, 112), Size = new Size(95, 30) };
        _removeBtn.Click += (_, _) => RemoveTrack();
        StyleBtn(_removeBtn);
        Controls.Add(_removeBtn);

        _upBtn = new Button { Text = Loc.Get("BgEditor_Up"), Location = new Point(405, 152), Size = new Size(95, 30) };
        _upBtn.Click += (_, _) => MoveUp();
        StyleBtn(_upBtn);
        Controls.Add(_upBtn);

        _downBtn = new Button { Text = Loc.Get("BgEditor_Dn"), Location = new Point(405, 192), Size = new Size(95, 30) };
        _downBtn.Click += (_, _) => MoveDown();
        StyleBtn(_downBtn);
        Controls.Add(_downBtn);

        var clrBtn = new Button { Text = "✕ Clear", Location = new Point(405, 232), Size = new Size(95, 30) };
        clrBtn.Click += (_, _) =>
        {
            if (_master.Settings.BackgroundMusic.Tracks.Count == 0) return;
            if (MessageBox.Show("Clear all background tracks?", "Clear", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            _master.Settings.BackgroundMusic.Tracks.Clear();
            _master.Save();
            RefreshList();
        };
        StyleBtn(clrBtn);
        Controls.Add(clrBtn);

        var behPanel = new Panel
        {
            Location = new Point(12, 370),
            Size = new Size(380, 40),
            BackColor = DarkTheme.BgPanel,
            BorderStyle = BorderStyle.FixedSingle
        };
        Controls.Add(behPanel);

        var behLabel = new Label
        {
            Text = Loc.Get("BgEditor_Behavior"), Location = new Point(8, 7),
            Size = new Size(100, 26),
            ForeColor = DarkTheme.FgText, BackColor = DarkTheme.BgPanel, Font = DarkTheme.FontNormal,
            TextAlign = ContentAlignment.MiddleLeft
        };
        behPanel.Controls.Add(behLabel);

        var behCombo = new ComboBox
        {
            Location = new Point(108, 6), Size = new Size(260, 26),
            DropDownStyle = ComboBoxStyle.DropDownList,
            DrawMode = DrawMode.OwnerDrawFixed,
            BackColor = DarkTheme.BgInput, ForeColor = DarkTheme.FgText,
            Font = DarkTheme.FontNormal
        };
        behCombo.DrawItem += (s, e) =>
        {
            if (e.Index < 0) return;
            e.DrawBackground();
            using var tb = new SolidBrush(e.State.HasFlag(DrawItemState.Selected) ? DarkTheme.FgAccent : DarkTheme.FgText);
            var itemText = behCombo.Items[e.Index]?.ToString() ?? "";
            e.Graphics.DrawString(itemText, DarkTheme.FontNormal, tb, e.Bounds);
            e.DrawFocusRectangle();
        };
        behCombo.Items.AddRange(new[]
        {
            Loc.Get("BgBehavior_Mix"),
            Loc.Get("BgBehavior_MusicPausesSounds"),
            Loc.Get("BgBehavior_MusicStopsSounds"),
            Loc.Get("BgBehavior_SoundsPauseMusic"),
            Loc.Get("BgBehavior_SoundsStopMusic")
        });
        var behMap = new Dictionary<string, int>
        {
            ["mix"] = 0, ["musicPausesSounds"] = 1, ["musicStopsSounds"] = 2,
            ["soundsPauseMusic"] = 3, ["soundsStopMusic"] = 4
        };
        behCombo.SelectedIndex = behMap.GetValueOrDefault(_master.Settings.BackgroundMusic.Behavior, 0);
        behCombo.SelectedIndexChanged += (_, _) =>
        {
            var revMap = new[] { "mix", "musicPausesSounds", "musicStopsSounds", "soundsPauseMusic", "soundsStopMusic" };
            _master.Settings.BackgroundMusic.Behavior = revMap[behCombo.SelectedIndex];
            _master.Save();
        };
        behPanel.Controls.Add(behCombo);

        _closeBtn = new Button { Text = Loc.Get("BgEditor_Close"), Location = new Point(405, 370), Size = new Size(95, 40) };
        _closeBtn.Click += (_, _) => DialogResult = DialogResult.OK;
        StyleBtn(_closeBtn);
        Controls.Add(_closeBtn);
    }

    private void OnPaint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        var cx = ClientSize.Width;

        using var bg = new SolidBrush(DarkTheme.BgTitleBar);
        g.FillRectangle(bg, 0, 0, cx, DarkTheme.TitleBarHeight);

        using var fg = new SolidBrush(DarkTheme.FgText);
        g.DrawString(Loc.Get("BgMusic_Header"), DarkTheme.FontBold, fg, 10, 8);

        var closeR = new Rectangle(cx - 34, 7, 30, DarkTheme.TitleBarHeight - 14);
        using var closeFg = new SolidBrush(DarkTheme.FgRed);
        using var font = new Font("Segoe UI", 12f, FontStyle.Regular);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("✕", font, closeFg, closeR, sf);
    }

    private void OnTitleMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        if (new Rectangle(ClientSize.Width - 34, 7, 30, DarkTheme.TitleBarHeight - 14).Contains(e.Location))
        {
            DialogResult = DialogResult.Cancel;
            return;
        }
        if (e.Y <= DarkTheme.TitleBarHeight)
        {
            _dragTitle = true;
            _dragStart = e.Location;
        }
    }

    private void OnTitleMouseMove(object? sender, MouseEventArgs e)
    {
        if (_dragTitle)
        {
            Location = new Point(Location.X + e.X - _dragStart.X, Location.Y + e.Y - _dragStart.Y);
        }
    }

    private void StyleBtn(Button btn)
    {
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderColor = DarkTheme.BorderColor;
        btn.BackColor = DarkTheme.BgInput;
        btn.ForeColor = DarkTheme.FgText;
        btn.Font = DarkTheme.FontNormal;
    }

    private void RefreshList()
    {
        _listBox.Items.Clear();
        foreach (var t in _master.Settings.BackgroundMusic.Tracks)
            _listBox.Items.Add(t.DisplayName);
    }

    private void AddTrack()
    {
        using var ofd = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "Audio Files|*.mp3;*.wav|All Files|*.*"
        };
        if (ofd.ShowDialog(this) != DialogResult.OK) return;
        AddTracks(ofd.FileNames);
    }

    private void AddTracks(string[] files)
    {
        foreach (var f in files)
        {
            _master.Settings.BackgroundMusic.Tracks.Add(new BgTrackItem
            {
                FilePath = f,
                DisplayName = Path.GetFileNameWithoutExtension(f)
            });
        }
        _master.Save();
        RefreshList();
    }

    private void RemoveTrack()
    {
        if (_listBox.SelectedIndex < 0) return;
        _master.Settings.BackgroundMusic.Tracks.RemoveAt(_listBox.SelectedIndex);
        _master.Save();
        RefreshList();
    }

    private void MoveUp()
    {
        var i = _listBox.SelectedIndex;
        if (i <= 0) return;
        var tracks = _master.Settings.BackgroundMusic.Tracks;
        (tracks[i], tracks[i - 1]) = (tracks[i - 1], tracks[i]);
        _master.Save();
        RefreshList();
        _listBox.SelectedIndex = i - 1;
    }

    private void MoveDown()
    {
        var i = _listBox.SelectedIndex;
        if (i < 0 || i >= _master.Settings.BackgroundMusic.Tracks.Count - 1) return;
        var tracks = _master.Settings.BackgroundMusic.Tracks;
        (tracks[i], tracks[i + 1]) = (tracks[i + 1], tracks[i]);
        _master.Save();
        RefreshList();
        _listBox.SelectedIndex = i + 1;
    }
}
