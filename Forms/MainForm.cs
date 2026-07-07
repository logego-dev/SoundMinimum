using System.IO;
using System.Text.Json;
using SoundMinimum.Audio;
using SoundMinimum.Controls;
using SoundMinimum.Models;
using SoundMinimum.Services;
using SoundMinimum.Themes;

namespace SoundMinimum.Forms;

public class MainForm : Form
{
    private readonly MasterSettingsService _master;
    private readonly ProjectManager _project;
    private readonly PlaylistManager _playlist;
    private readonly FavoritesManager _favorites;
    private readonly AudioEngine _audio;
    private readonly BackgroundAudioEngine _bgAudio;
    private readonly AudioDeviceManager _devices;

    private const int RowHeight = 34;
    private bool _dragTitle;
    private bool _dragVolume;
    private Point _dragStart;
    private int _playingIndex = -1;
    private int _scrollOffset = 0; // For mouse wheel scrolling


    private static readonly string _queuePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "queue.json");
    private static readonly JsonSerializerOptions _jsonOpts = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly System.Windows.Forms.Timer _uiTimer = new() { Interval = 250 };
    private readonly string _appTitle;
    private readonly string _appSubtitle;

    public MainForm(string[]? initialFiles = null)
    {
        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        var masterPath = Path.Combine(basePath, "master.json");

        _master = new MasterSettingsService(masterPath);
        _project = new ProjectManager(_master);
        _playlist = new PlaylistManager();
        _favorites = new FavoritesManager(_master);
        _audio = new AudioEngine();
        _bgAudio = new BackgroundAudioEngine();
        _devices = new AudioDeviceManager();

        _playlist.SetTracks(_project.Current.Tracks);
        _bgAudio.SetTracks(_master.Settings.BackgroundMusic.Tracks);
        _bgAudio.Volume = _master.Settings.BackgroundMusic.Volume;

        if (_master.Settings.OutputDevices.Count > 0)
        {
            _audio.SetDevices(_master.Settings.OutputDevices);
            UpdateBgAudioDevice();
        }

        InitForm();
        SetupEvents();
        AutoLoadQueue();
        _uiTimer.Tick += (_, _) => RefreshUI();
        _uiTimer.Start();

        if (initialFiles != null && initialFiles.Length > 0)
            AddFiles(initialFiles);

        var cfgPath = Path.Combine(basePath, "app.txt");
        if (File.Exists(cfgPath))
        {
            var lines = File.ReadAllLines(cfgPath);
            _appTitle = lines.Length > 0 ? lines[0].Trim() : "Sound Minimum";
            _appSubtitle = lines.Length > 1 ? lines[1].Trim() : "";
        }
        else
        {
            _appTitle = "Sound Minimum";
            _appSubtitle = "";
        }
    }

    private void InitForm()
    {
        Text = "Sound Minimum";
        var icoPath = Path.Combine(Application.StartupPath, "SoundMinimum.ico");
        if (File.Exists(icoPath)) Icon = new Icon(icoPath);
        var ws = _master.Settings.Window;
        StartPosition = FormStartPosition.Manual;
        Bounds = new Rectangle(ws.X, ws.Y, ws.Width, ws.Height);
        MinimumSize = new Size(900, 600);
        FormBorderStyle = FormBorderStyle.None;
        BackColor = DarkTheme.BgMain;
        DoubleBuffered = true;
        KeyPreview = true;

        AllowDrop = true;
        DragEnter += (_, e) => { if (e.Data!.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
        DragDrop += (_, e) =>
        {
            if (e.Data!.GetData(DataFormats.FileDrop) is string[] files)
                AddFiles(files);
        };

        Paint += OnMainPaint;
        MouseWheel += OnMouseWheel;
        KeyDown += OnHotkey;
        Resize += (_, _) => Invalidate();
        FormClosing += OnClosing;
    }

    private void SetupEvents()
    {
        _playlist.OnChanged += () =>
        {
            _project.Current.Tracks = _playlist.Tracks.ToList();
            _project.MarkDirty();
            ClampScroll();
            Invalidate();
        };
        _project.OnProjectChanged += () => Invalidate();
        _audio.OnTrackStarted += () => Invalidate();
        _audio.OnTrackEnded += () =>
        {
            if (_project.Current.AutoPlay)
            {
                var next = _playlist.GetNextIndex();
                if (next >= 0) { PlayAt(next); Invalidate(); return; }
                _audio.Stop();
            }
            CheckResumeBgAfterSound();
            Invalidate();
        };
        _audio.OnAllStopped += () => { _playingIndex = -1; CheckResumeBgAfterSound(); Invalidate(); };
        _audio.OnPositionUpdated += _ =>
        {
            if (!_uiTimer.Enabled) _uiTimer.Start();
        };
        _audio.OnPauseChanged += paused =>
        {
            if (paused)
                CheckResumeBgAfterSound();
            else
                CheckSoundBehaviorOnPlay();
            Invalidate();
        };
        _bgAudio.OnStarted += () => { CheckBgBehaviorOnStart(); Invalidate(); };
        _bgAudio.OnStopped += () => { CheckBgBehaviorOnStop(); Invalidate(); };
    }

    private void CheckBgBehaviorOnStart()
    {
        var behavior = _master.Settings.BackgroundMusic.Behavior;
        if (behavior == "musicPausesSounds" && _audio.State == PlayerState.Playing)
            _audio.Pause();
        else if (behavior == "musicStopsSounds")
            _audio.Stop();
    }

    private void CheckBgBehaviorOnStop()
    {
        var behavior = _master.Settings.BackgroundMusic.Behavior;
        if (behavior == "musicPausesSounds" && _audio.State == PlayerState.Paused)
            _audio.Pause();
    }

    private void CheckSoundBehaviorOnPlay()
    {
        var behavior = _master.Settings.BackgroundMusic.Behavior;
        if (behavior == "soundsPauseMusic" && _bgAudio.IsPlaying)
            _bgAudio.Pause();
        else if (behavior == "soundsStopMusic" && _bgAudio.IsPlaying)
            _bgAudio.Stop();
    }

    private void CheckResumeBgAfterSound()
    {
        var behavior = _master.Settings.BackgroundMusic.Behavior;
        if (behavior == "soundsPauseMusic")
            _bgAudio.Resume();
    }

    private void OnMainPaint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(DarkTheme.BgMain);

        DrawTitleBar(g);
        DrawFavPanel(g);
        DrawToolbar(g);
        DrawPlaylist(g);
        DrawNowPlaying(g);
        DrawTransport(g);
        DrawBgMusic(g);
    }

    private int ContentLeft => FavRect.Right + DarkTheme.Padding;
    private int ContentWidth => Math.Max(200, ClientSize.Width - ContentLeft - DarkTheme.Padding);

    private Rectangle TitleRect => new(0, 0, ClientSize.Width, DarkTheme.TitleBarHeight);
    private Rectangle FavRect => new(0, DarkTheme.TitleBarHeight, 190,
        Math.Max(0, ClientSize.Height - DarkTheme.TitleBarHeight));
    private Rectangle ToolbarRect => new(ContentLeft, DarkTheme.TitleBarHeight + 6,
        ContentWidth, DarkTheme.ToolbarHeight);
    private int PlaylistTop => ToolbarRect.Bottom + 6;
    private int PlaylistBottomSpace => NowPlayingHeight + 4 + TransportHeight + 6 + BgHeight + 6;
    private int NowPlayingHeight => 42;
    private int TransportHeight => 44;
    private int BgHeight => 52;
    private Rectangle PlaylistRect => new(ContentLeft, PlaylistTop, ContentWidth,
        Math.Max(60, ClientSize.Height - PlaylistTop - PlaylistBottomSpace - 6));
    private Rectangle NowPlayingRect => new(ContentLeft, PlaylistRect.Bottom + 4,
        ContentWidth, NowPlayingHeight);
    private Rectangle TransportRect => new(ContentLeft, NowPlayingRect.Bottom + 4,
        ContentWidth, TransportHeight);
    private Rectangle BgRect => new(ContentLeft, TransportRect.Bottom + 6,
        ContentWidth, BgHeight);

    private void DrawTitleBar(Graphics g)
    {
        using var bg = new SolidBrush(DarkTheme.BgTitleBar);
        g.FillRectangle(bg, TitleRect);

        using var accent = new SolidBrush(DarkTheme.FgAccent);
        g.DrawString(_appTitle, DarkTheme.FontTitle, accent, 14, 8);

        var cx = ClientSize.Width - 12;
        if (!string.IsNullOrEmpty(_appSubtitle))
        {
            var subW = cx - 180 - 145;
            if (subW > 40)
            {
                var subR = new Rectangle(145, 10, subW, 20);
                using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                using var dim = new SolidBrush(DarkTheme.FgDim);
                g.DrawString(_appSubtitle, DarkTheme.FontSmall, dim, subR, sf);
            }
        }

        DrawTitleBtn(g, new Rectangle(cx - 34, 7, 30, TitleRect.Height - 14), "✕", DarkTheme.FgRed);
        DrawTitleBtn(g, new Rectangle(cx - 70, 7, 30, TitleRect.Height - 14), "─", DarkTheme.FgDim);
    }

    private void DrawTitleBtn(Graphics g, Rectangle r, string text, Color fg)
    {
        using var brush = new SolidBrush(fg);
        using var font = new Font("Segoe UI", text is "✕" or "─" ? 11f : 9f, FontStyle.Regular);
        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(text, font, brush, r, sf);
    }

    private void DrawFavPanel(Graphics g)
    {
        using var bg = new SolidBrush(DarkTheme.BgFavPanel);
        g.FillRectangle(bg, FavRect);
        using var border = new Pen(DarkTheme.BorderColor);
        g.DrawLine(border, FavRect.Right, FavRect.Top, FavRect.Right, FavRect.Bottom);

        var y = DarkTheme.TitleBarHeight + 10;
        using var dim = new SolidBrush(DarkTheme.FgDim);
        using var fg = new SolidBrush(DarkTheme.FgText);
        using var accent = new SolidBrush(DarkTheme.FgAccent);

        // --- Favorites ---
        g.DrawString(Loc.Get("Favorites_Header"), DarkTheme.FontSmall, dim, 10, y);
        y += 20;

        foreach (var fav in _favorites.Favorites)
        {
            var rect = new Rectangle(8, y, FavRect.Width - 16, 26);
            using var favBg = new SolidBrush(DarkTheme.BgInput);
            g.FillRoundedRect(favBg, rect, 4);
            var display = fav.DisplayName.Length > 20 ? fav.DisplayName[..18] + ".." : fav.DisplayName;
            g.DrawString(display, DarkTheme.FontSmall, fg, 14, y + 5);
            y += 30;
        }

        if (_favorites.Favorites.Count == 0)
        {
            g.DrawString(Loc.Get("Favorites_Empty"), DarkTheme.FontSmall, dim, 10, y + 2);
            y += 50;
        }

        // --- Devices ---
        y += 6;
        using var sep = new Pen(DarkTheme.BorderLight);
        g.DrawLine(sep, 8, y, FavRect.Right - 8, y);
        y += 10;

        g.DrawString(Loc.Get("Devices_Header"), DarkTheme.FontSmall, dim, 10, y);
        y += 20;

        var allDevs = _devices.GetDevices();
        var selected = _master.Settings.OutputDevices;

        // Default option
        var defR = new Rectangle(8, y, FavRect.Width - 16, 26);
        using var defBg = new SolidBrush(DarkTheme.BgInput);
        g.FillRoundedRect(defBg, defR, 4);
        g.DrawString(selected.Count == 0 ? "✓ " + Loc.Get("Devices_Default") : "☐ " + Loc.Get("Devices_Default"), DarkTheme.FontSmall, selected.Count == 0 ? accent : dim, 14, y + 5);
        y += 30;

        foreach (var d in allDevs)
        {
            var isOn = selected.Contains(d);
            var devR = new Rectangle(8, y, FavRect.Width - 16, 26);
            using var devBg = new SolidBrush(isOn ? DarkTheme.BgSelected : DarkTheme.BgInput);
            g.FillRoundedRect(devBg, devR, 4);
            var label = d.Length > 18 ? d[..16] + ".." : d;
            g.DrawString($"{(isOn ? "✓" : "☐")} {label}", DarkTheme.FontSmall, isOn ? accent : fg, 14, y + 5);
            y += 30;
        }

        // --- Device Status ---
        if (selected.Count == 0)
        {
            g.DrawString("♪ " + Loc.Get("Devices_Default"), DarkTheme.FontSmall, dim, 10, FavRect.Bottom - 18);
        }
        else if (selected.Count == 1)
        {
            var label = selected[0].Length > 18 ? selected[0][..16] + ".." : selected[0];
            g.DrawString($"♪ {label}", DarkTheme.FontSmall, dim, 10, FavRect.Bottom - 18);
        }
        else
        {
            int lineH = 16;
            var list = selected.Take(8).ToList();
            int startY = FavRect.Bottom - 4 - list.Count * lineH;
            for (int i = 0; i < list.Count; i++)
            {
                var label = list[i].Length > 18 ? list[i][..16] + ".." : list[i];
                g.DrawString($"♪ {label}", DarkTheme.FontSmall, dim, 10, startY + i * lineH);
            }
        }
    }

    private void DrawToolbar(Graphics g)
    {
        using var bg = new SolidBrush(DarkTheme.BgPanel);
        g.FillRoundedRect(bg, ToolbarRect, DarkTheme.ButtonRadius);

        var x = ToolbarRect.X + 8;
        var btnRect = new Rectangle(x, ToolbarRect.Y + 4, 60, ToolbarRect.Height - 8);
        DrawTbBtn(g, btnRect, Loc.Get("Toolbar_Add"));
        btnRect.X += 65; DrawTbBtn(g, btnRect, Loc.Get("Toolbar_Del"));
        btnRect.X += 65; DrawTbBtn(g, btnRect, Loc.Get("Toolbar_Up"));
        btnRect.X += 50; DrawTbBtn(g, btnRect, Loc.Get("Toolbar_Dn"));
        btnRect.X += 60; btnRect.Width = 80; DrawTbBtn(g, btnRect, Loc.Get("Toolbar_Clear"));
        using var dim = new SolidBrush(DarkTheme.FgDim);
        using var accent = new SolidBrush(DarkTheme.FgAccent);
        // Removed queue limit display
        g.DrawString($"({_playlist.Count})", DarkTheme.FontSmall, accent, ToolbarRect.Right - 90, ToolbarRect.Y + 9);
    }

    private void DrawTbBtn(Graphics g, Rectangle r, string text)
    {
        using var bg = new SolidBrush(DarkTheme.BgInput);
        g.FillRoundedRect(bg, r, 4);
        using var pen = new Pen(DarkTheme.BorderColor);
        g.DrawRoundedRect(pen, r, 4);
        using var brush = new SolidBrush(DarkTheme.FgText);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(text, DarkTheme.FontSmall, brush, r, sf);
    }

    private void DrawPlaylist(Graphics g)
    {
        var rect = PlaylistRect;
        using var bg = new SolidBrush(DarkTheme.BgPanel);
        g.FillRoundedRect(bg, rect, DarkTheme.ButtonRadius);

        var cols = new[] { Loc.Get("Playlist_ColNum"), Loc.Get("Playlist_ColTrack"), Loc.Get("Playlist_ColVol"), Loc.Get("Playlist_ColLoop"), Loc.Get("Playlist_ColXfd"), Loc.Get("Playlist_ColFade"), Loc.Get("Playlist_ColFav") };
        var cw = new[] { 28, rect.Width - 306, 56, 46, 42, 50, 34 };
        var x = rect.X + DarkTheme.Padding;
        var hdrY = rect.Y + 4;

        // Fill header background to prevent overlap when scrolling
        using var headerBg = new SolidBrush(DarkTheme.BgPanel);
        g.FillRectangle(headerBg, rect.X, hdrY - 4, rect.Width, 30);

        using var hdrBg = new SolidBrush(DarkTheme.BgHeader);
        using var hdrBrush = new SolidBrush(DarkTheme.FgDim);
        for (int i = 0; i < cols.Length; i++)
        {
            var r = new Rectangle(x, hdrY, cw[i], 22);
            g.FillRoundedRect(hdrBg, r, 3);
            g.DrawString(cols[i], DarkTheme.FontSmall, hdrBrush, r.X + 6, r.Y + 4);
            x += cw[i];
        }

        // Clip rows strictly below the header so they never overlap
        var rowClip = new Rectangle(rect.X, hdrY + 26, rect.Width, rect.Height - 26);
        g.SetClip(rowClip);
        var y = hdrY + 26 - _scrollOffset;
        var totW = rect.Width - DarkTheme.Padding * 2;
        using var dim = new SolidBrush(DarkTheme.FgDim);
        using var bright = new SolidBrush(DarkTheme.FgBright);
        using var accent = new SolidBrush(DarkTheme.FgAccent);
        using var selBg = new SolidBrush(DarkTheme.BgSelected);
        using var playBg = new SolidBrush(DarkTheme.BgPlaying);
        using var pen = new Pen(DarkTheme.BorderColor);
        
        for (int i = 0; i < _playlist.Tracks.Count; i++)
        {
            if (y + RowHeight < rect.Y || y > rect.Bottom) { y += RowHeight; continue; }
            var row = new Rectangle(rect.X + DarkTheme.Padding, y, totW, RowHeight);
            if (y >= rect.Bottom) break;

            var isPlay = i == _playingIndex;
            var isSel = i == _playlist.CurrentIndex;
            if (isPlay) g.FillRectangle(playBg, row);
            else if (isSel) g.FillRectangle(selBg, row);

            var cx = rect.X + DarkTheme.Padding;
            var t = _playlist.Tracks[i];

            g.DrawString((i + 1).ToString(), DarkTheme.FontSmall, dim, cx + 8, y + 12);
            cx += cw[0];

            var nm = t.DisplayName;
            if (string.IsNullOrEmpty(nm)) nm = Path.GetFileName(t.FilePath);
            var exists = File.Exists(t.FilePath);
            if (!exists)
            {
                using var red = new SolidBrush(Color.FromArgb(200, 60, 60));
                g.DrawString("✕", DarkTheme.FontSmall, red, cx + 4, y + 12);
            }
            using var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter };
            var nameX = cx + (exists ? 4 : 18);
            g.DrawString(nm, DarkTheme.FontNormal, isPlay ? accent : bright, new RectangleF(nameX, y + 6, cw[1] - (exists ? 12 : 26), 24), sf);
            cx += cw[1];

            g.DrawString($"{(int)(t.Volume * 100)}%", DarkTheme.FontSmall, dim, cx + 4, y + 12);
            cx += cw[2];

            g.DrawString(t.Loop ? "✓" : "☐", DarkTheme.FontSmall, t.Loop ? accent : dim, cx + 10, y + 12);
            cx += cw[3];

            g.DrawString(t.Crossfade ? "✓" : "☐", DarkTheme.FontSmall, t.Crossfade ? accent : dim, cx + 8, y + 12);
            cx += cw[4];

            g.DrawString(t.Crossfade ? t.FadeDuration.ToString("0.0") : "—", DarkTheme.FontSmall, dim, cx + 4, y + 12);
            cx += cw[5];

            var fav = _favorites.IsFavorite(t.FilePath);
            g.DrawString(fav ? "★" : "☆", DarkTheme.FontSmall, fav ? new SolidBrush(DarkTheme.FgOrange) : dim, cx + 8, y + 12);

            y += RowHeight;
        }
        g.ResetClip();

        if (_playlist.Count == 0)
            g.DrawString(Loc.Get("Playlist_Empty"), DarkTheme.FontNormal, dim, rect.X + 20, rect.Y + rect.Height / 2 - 8);
    }

    private void DrawNowPlaying(Graphics g)
    {
        var rect = NowPlayingRect;
        using var bg = new SolidBrush(DarkTheme.BgPanel);
        g.FillRoundedRect(bg, rect, DarkTheme.ButtonRadius);

        if (_playingIndex >= 0 && _playingIndex < _playlist.Tracks.Count)
        {
            var t = _playlist.Tracks[_playingIndex];
            var nm = t.DisplayName;
            if (string.IsNullOrEmpty(nm)) nm = Path.GetFileName(t.FilePath);

            var icon = _audio.State == PlayerState.Playing ? "▶" :
                       _audio.State == PlayerState.Paused ? "⏸" : "■";
            using var accent = new SolidBrush(DarkTheme.FgAccent);
            g.DrawString($"{icon} {nm}", DarkTheme.FontBold, accent, rect.X + 12, rect.Y - 3);

            var pw = Math.Max(30, rect.Width - 170);
            var progR = new Rectangle(rect.X + 12, rect.Y + 24, pw, 8);
            using var progBg = new SolidBrush(DarkTheme.BgInput);
            g.FillRoundedRect(progBg, progR, 4);

            // Determine progress bar color and value based on state
            if (_audio.State == PlayerState.Fading && _audio.CurrentFadeDuration > 0)
            {
                // During crossfade - show yellow progress bar with fade progress
                using var progFill = new SolidBrush(Color.Gold); // Yellow color for crossfade
                var fill = (int)(progR.Width * _audio.CurrentFadeProgress);
                if (fill > 0)
                {
                    g.FillRoundedRect(progFill, new Rectangle(progR.X, progR.Y, fill, progR.Height), 4);
                }
            }
            else if (_audio.TotalDuration > 0)
            {
                // Normal playback - show accent color progress
                var fill = (int)(progR.Width * (_audio.CurrentPosition / _audio.TotalDuration));
                if (fill > 0)
                {
                    using var progFill = new SolidBrush(DarkTheme.FgAccentDim);
                    g.FillRoundedRect(progFill, new Rectangle(progR.X, progR.Y, fill, progR.Height), 4);
                }
            }

            using var dim = new SolidBrush(DarkTheme.FgDim);
            string time;
            if (_audio.State == PlayerState.Fading && _audio.CurrentFadeDuration > 0)
            {
                // During crossfade - show fade progress as time
                var currentTime = _audio.CurrentFadeDuration * _audio.CurrentFadeProgress;
                time = $"{FormatTimePrecise(currentTime)} / {FormatTimePrecise(_audio.CurrentFadeDuration)}";
            }
            else
            {
                // Normal playback - show track duration
                time = $"{FormatTime(_audio.CurrentPosition)} / {FormatTime(_audio.TotalDuration)}";
            }
            g.DrawString(time, DarkTheme.FontMono, dim, progR.Right + 6, rect.Y + 22);
        }
        else
        {
            using var dim = new SolidBrush(DarkTheme.FgDim);
            g.DrawString(Loc.Get("NowPlaying_Ready"), DarkTheme.FontNormal, dim, rect.X + 12, rect.Y + 12);
        }
    }

    private void DrawTransport(Graphics g)
    {
        var rect = TransportRect;
        using var bg = new SolidBrush(DarkTheme.BgPanel);
        g.FillRoundedRect(bg, rect, DarkTheme.ButtonRadius);

        var cx = rect.X + 12;
        var bh = rect.Height - 12;
        DrawTpBtn(g, new Rectangle(cx, rect.Y + 6, 34, bh), "⏮"); cx += 40;
        DrawTpBtn(g, new Rectangle(cx, rect.Y + 6, 34, bh),
            _audio.State == PlayerState.Playing ? "⏸" : "▶"); cx += 40;
        DrawTpBtn(g, new Rectangle(cx, rect.Y + 6, 34, bh), "⏹"); cx += 40;
        DrawTpBtn(g, new Rectangle(cx, rect.Y + 6, 34, bh), "⏭"); cx += 46;

        using var dim = new SolidBrush(DarkTheme.FgDim);
        using var accent = new SolidBrush(DarkTheme.FgAccent);

        // Master volume slider
        cx = TransportRect.Right - 260;
        cx += 34;
        var mvolR = new Rectangle(cx, rect.Y + 10, 120, 18);
        using var vBg = new SolidBrush(DarkTheme.BgInput);
        g.FillRoundedRect(vBg, mvolR, 4);
        using var vFill = new SolidBrush(DarkTheme.FgAccent);
        g.FillRectangle(vFill, mvolR.X, mvolR.Y, (int)(mvolR.Width * _audio.MasterVolume), mvolR.Height);

        var status = _audio.State switch
        {
            PlayerState.Playing => $"{Loc.Get("Transport_Playing")} ({_playingIndex + 1}/{_playlist.Count})",
            PlayerState.Paused => Loc.Get("Transport_Paused"),
            PlayerState.Fading => Loc.Get("NowPlaying_Crossfade") + "...",
            _ => "Stopped"
        };
        var statusMaxW = TransportRect.Right - 270 - cx;
        if (statusMaxW > 20)
        {
            using var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter };
            g.DrawString(status, DarkTheme.FontSmall, dim, new RectangleF(cx, rect.Y + 14, statusMaxW, 20), sf);
        }
        var autoT = _project.Current.AutoPlay ? "✓ Auto" : "☐ Auto";
        g.DrawString(autoT, DarkTheme.FontSmall, _project.Current.AutoPlay ? accent : dim, rect.Right - 68, rect.Y + 14);
    }

    private void DrawTpBtn(Graphics g, Rectangle r, string text)
    {
        using var bg = new SolidBrush(DarkTheme.BgInput);
        g.FillRoundedRect(bg, r, 4);
        using var pen = new Pen(DarkTheme.BorderColor);
        g.DrawRoundedRect(pen, r, 4);
        using var brush = new SolidBrush(DarkTheme.FgText);
        using var font = new Font("Segoe UI", 10f);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(text, font, brush, r, sf);
    }

    private void DrawBgMusic(Graphics g)
    {
        var rect = BgRect;
        using var bg = new SolidBrush(DarkTheme.BgPanel);
        g.FillRoundedRect(bg, rect, DarkTheme.ButtonRadius);

        var br = rect.Right - 6;
        DrawTpBtn(g, new Rectangle(br - 160, rect.Y + 5, 22, 22), "⏮");
        DrawTpBtn(g, new Rectangle(br - 136, rect.Y + 5, 22, 22), "⏭");
        DrawTpBtn(g, new Rectangle(br - 110, rect.Y + 5, 34, 22), _bgAudio.IsPlaying ? "⏸" : "▶");
        DrawTpBtn(g, new Rectangle(br - 48, rect.Y + 5, 44, 22), "Edit");

        using var accent = new SolidBrush(DarkTheme.FgAccent);
        g.DrawString(Loc.Get("BgMusic_Header"), DarkTheme.FontBold, accent, rect.X + 10, rect.Y + 5);

        var behMap = new Dictionary<string, string>
        {
            ["mix"] = Loc.Get("BgBehavior_Mix"), ["musicPausesSounds"] = Loc.Get("BgBehavior_MusicPausesSounds"),
            ["musicStopsSounds"] = Loc.Get("BgBehavior_MusicStopsSounds"), ["soundsPauseMusic"] = Loc.Get("BgBehavior_SoundsPauseMusic"),
            ["soundsStopMusic"] = Loc.Get("BgBehavior_SoundsStopMusic")
        };
        g.DrawString(behMap.GetValueOrDefault(_master.Settings.BackgroundMusic.Behavior, Loc.Get("BgBehavior_Mix")),
            DarkTheme.FontSmall, new SolidBrush(DarkTheme.FgDim), rect.X + 135, rect.Y + 7);

        var volY = rect.Y + 28;
        DrawTpBtn(g, new Rectangle(rect.X + 8, volY, 20, 20), "−");
        g.DrawString($"{(int)(_bgAudio.Volume * 100)}%", DarkTheme.FontSmall,
            new SolidBrush(DarkTheme.FgText), rect.X + 32, volY + 3);
        DrawTpBtn(g, new Rectangle(rect.X + 64, volY, 20, 20), "+");

        if (_bgAudio.IsPlaying)
        {
            using var bright = new SolidBrush(DarkTheme.FgBright);
            g.DrawString(_bgAudio.CurrentTrack, DarkTheme.FontSmall, bright, rect.X + 92, volY + 3);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        // Title bar buttons
        if (TitleRect.Contains(e.Location))
        {
            var cx = ClientSize.Width - 12;
            var bh = TitleRect.Height - 14;
            if (HandleTitleBarButton(e.Location)) return;
            _dragTitle = true;
            _dragStart = e.Location;
            return;
        }

        // Master volume drag
        if (TransportRect.Contains(e.Location))
        {
            var mvolX = TransportRect.Right - 226;
            var mvolR = new Rectangle(mvolX, TransportRect.Y + 10, 120, 18);
            if (mvolR.Contains(e.Location))
            {
                _dragVolume = true;
                _audio.MasterVolume = Math.Clamp((double)(e.X - mvolR.X) / mvolR.Width, 0, 1);
                Invalidate();
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragTitle) Location = new Point(Location.X + e.X - _dragStart.X, Location.Y + e.Y - _dragStart.Y);
        if (_dragVolume && TransportRect.Contains(e.Location))
        {
            var mvolX = TransportRect.Right - 226;
            var mvolR = new Rectangle(mvolX, TransportRect.Y + 10, 120, 18);
            _audio.MasterVolume = Math.Clamp((double)(e.X - mvolR.X) / mvolR.Width, 0, 1);
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _dragTitle = false;
        _dragVolume = false;
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (PlaylistRect.Contains(e.Location))
        {
            var idx = GetRowAt(e.Y);
            if (idx >= 0 && idx < _playlist.Count)
                PlayAt(idx);
        }
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);

        if (TitleRect.Contains(e.Location))
        {
            var cx = ClientSize.Width - 12;
            var bh = TitleRect.Height - 14;
            if (new Rectangle(cx - 34, 7, 30, bh).Contains(e.Location)) { Close(); return; }
            if (new Rectangle(cx - 70, 7, 30, bh).Contains(e.Location)) { WindowState = FormWindowState.Minimized; return; }
            return;
        }

        // Toolbar
        if (ToolbarRect.Contains(e.Location))
        {
            var x = ToolbarRect.X + 8;
            var bh = ToolbarRect.Height - 8;
            if (new Rectangle(x, ToolbarRect.Y + 4, 60, bh).Contains(e.Location)) AddFiles();
            x += 65; if (new Rectangle(x, ToolbarRect.Y + 4, 60, bh).Contains(e.Location)) RemoveSelected();
            x += 65; if (new Rectangle(x, ToolbarRect.Y + 4, 44, bh).Contains(e.Location)) MoveSelectedUp();
            x += 50; if (new Rectangle(x, ToolbarRect.Y + 4, 44, bh).Contains(e.Location)) MoveSelectedDown();
            x += 60; if (new Rectangle(x, ToolbarRect.Y + 4, 80, bh).Contains(e.Location)) ClearPlaylist();
            return;
        }

        // Playlist click
        if (PlaylistRect.Contains(e.Location))
        {
            var clicked = GetRowAt(e.Y);
            if (clicked >= 0 && clicked < _playlist.Count)
            {
                var col = GetColumnAt(e.X);
                if (col == 6) ToggleFavorite(clicked);
                else if (col >= 2 && col <= 5) { ToggleProperty(clicked, col); _playlist.SetCurrent(clicked); }
                else _playlist.SetCurrent(clicked);
            }
            return;
        }

        // Now Playing — progress bar seeking
        if (NowPlayingRect.Contains(e.Location) && _playingIndex >= 0 && _audio.TotalDuration > 0)
        {
            var rect = NowPlayingRect;
            var pw = Math.Max(30, rect.Width - 170);
            var hitR = new Rectangle(rect.X + 12, rect.Y + 20, pw, 18);
            if (hitR.Contains(e.Location))
            {
                var ratio = (double)(e.X - hitR.X) / hitR.Width;
                _audio.Seek(ratio * _audio.TotalDuration);
            }
            return;
        }

        // Transport
        if (TransportRect.Contains(e.Location))
        {
            var cx = TransportRect.X + 12;
            var bh = TransportRect.Height - 12;
            if (new Rectangle(cx, TransportRect.Y + 6, 34, bh).Contains(e.Location)) PlayPrev(); cx += 40;
            if (new Rectangle(cx, TransportRect.Y + 6, 34, bh).Contains(e.Location)) TogglePlayPause(); cx += 40;
            if (new Rectangle(cx, TransportRect.Y + 6, 34, bh).Contains(e.Location)) _audio.Stop(); cx += 40;
            if (new Rectangle(cx, TransportRect.Y + 6, 34, bh).Contains(e.Location)) PlayNext(); cx += 46;
            // Master volume
            var mvolX = TransportRect.Right - 226;
            var mvolR = new Rectangle(mvolX, TransportRect.Y + 10, 120, 18);
            if (mvolR.Contains(e.Location))
            {
                _audio.MasterVolume = Math.Clamp((double)(e.X - mvolR.X) / mvolR.Width, 0, 1);
                Invalidate();
            }
            // Auto toggle
            if (new Rectangle(TransportRect.Right - 68, TransportRect.Y + 7, 60, 28).Contains(e.Location))
            {
                _project.Current.AutoPlay = !_project.Current.AutoPlay;
                _project.MarkDirty();
            }
            return;
        }

        // Background music
        if (BgRect.Contains(e.Location))
        {
            var r = BgRect;
            var br = r.Right - 6;
            var volY = r.Y + 28;
            var btnY = r.Y + 5;

            // Background navigation buttons
            if (new Rectangle(br - 160, btnY, 22, 22).Contains(e.Location))
            {
                _bgAudio.Previous();
            }
            else if (new Rectangle(br - 136, btnY, 22, 22).Contains(e.Location))
            {
                _bgAudio.Next();
            }
            // Play/Pause button
            else if (new Rectangle(br - 110, btnY, 34, 22).Contains(e.Location))
            {
                if (_bgAudio.IsPlaying) 
                {
                    _bgAudio.Pause();
                }
                else 
                {
                    // Always call Play when not playing - it handles initialization correctly
                    _bgAudio.Play();
                }
            }
            else if (new Rectangle(br - 48, r.Y + 5, 44, 22).Contains(e.Location))
            {
                using var editor = new PlaylistEditorForm(_master);
                editor.ShowDialog(this);
                _bgAudio.SetTracks(_master.Settings.BackgroundMusic.Tracks);
                _bgAudio.Volume = _master.Settings.BackgroundMusic.Volume;
                Invalidate();
            }
            else if (new Rectangle(r.X + 8, volY, 20, 20).Contains(e.Location))
            {
                _bgAudio.Volume = Math.Max(0, _bgAudio.Volume - 0.05);
                _master.Settings.BackgroundMusic.Volume = _bgAudio.Volume;
                _master.Save();
                Invalidate();
            }
            else if (new Rectangle(r.X + 64, volY, 20, 20).Contains(e.Location))
            {
                _bgAudio.Volume = Math.Min(1, _bgAudio.Volume + 0.05);
                _master.Settings.BackgroundMusic.Volume = _bgAudio.Volume;
                _master.Save();
                Invalidate();
            }
            return;
        }

        // Favorites panel
        if (FavRect.Contains(e.Location))
        {
            var y = DarkTheme.TitleBarHeight + 30;
            foreach (var fav in _favorites.Favorites)
            {
                var rect = new Rectangle(8, y, FavRect.Width - 16, 26);
                if (rect.Contains(e.Location))
                {
                    AddFavoriteToPlaylist(fav);
                    return;
                }
                y += 30;
            }

            if (_favorites.Favorites.Count == 0) y += 50;
            // Skip separator + gap (6+10) + "DEVICES" header (20) = 36
            y += 36;
            // Default button
            var devR = new Rectangle(8, y, FavRect.Width - 16, 26);
            if (devR.Contains(e.Location))
            {
                _master.Settings.OutputDevices.Clear();
                _master.Save();
                _audio.SetDevices(new List<string>());
                _bgAudio.SetDeviceId(-1);
                Invalidate();
                return;
            }
            y += 30;

            var allDevs = _devices.GetDevices();
            foreach (var d in allDevs)
            {
                devR = new Rectangle(8, y, FavRect.Width - 16, 26);
                if (devR.Contains(e.Location))
                {
                    var sel = _master.Settings.OutputDevices;
                    if (sel.Contains(d)) sel.Remove(d); else sel.Add(d);
                    _master.Save();
                    _audio.SetDevices(sel.ToList());
                    UpdateBgAudioDevice();
                    Invalidate();
                    return;
                }
                y += 30;
            }
        }
    }

    private bool HandleTitleBarButton(Point location)
    {
        var cx = ClientSize.Width - 12;
        var bh = TitleRect.Height - 14;
        if (new Rectangle(cx - 34, 7, 30, bh).Contains(location)) { Close(); return true; }
        if (new Rectangle(cx - 70, 7, 30, bh).Contains(location)) { WindowState = FormWindowState.Minimized; return true; }
        return false;
    }

    private int GetRowAt(int y)
    {
        var hdrHeight = 26;
        var rowY = PlaylistRect.Y + 4 + hdrHeight;
        return (y - rowY + _scrollOffset) / RowHeight;
    }

    private int GetColumnAt(int x)
    {
        var cw = new[] { 28, PlaylistRect.Width - 306, 56, 46, 42, 50, 34 };
        var cx = PlaylistRect.X + DarkTheme.Padding;
        for (int i = 0; i < cw.Length; i++)
        {
            if (x >= cx && x < cx + cw[i]) return i;
            cx += cw[i];
        }
        return -1;
    }

    private void ToggleFavorite(int index)
    {
        if (index < 0 || index >= _playlist.Count) return;
        var track = _playlist.Tracks[index];
        _favorites.Toggle(track.FilePath);
        Invalidate();
    }

    private void ToggleProperty(int index, int col)
    {
        if (index < 0 || index >= _playlist.Count) return;
        var track = _playlist.Tracks[index];
        switch (col)
        {
            case 2: // volume
                track.Volume = Math.Clamp(track.Volume + 0.1 > 1.0 ? 0.1 : track.Volume + 0.1, 0, 1);
                // Update audio engine volume if this is the currently playing track
                if (index == _playingIndex)
                {
                    _audio.SetVolume(track.Volume);
                }
                break;
            case 3: 
                track.Loop = !track.Loop; 
                // Update audio engine loop flag if this is the currently playing track
                if (index == _playingIndex)
                {
                    _audio.SetLoop(track.Loop);
                }
                break;
            case 4: track.Crossfade = !track.Crossfade; break;
            case 5: // fade duration
                track.FadeDuration = track.FadeDuration >= 5.0 ? 1.0 : track.FadeDuration + 1.0;
                break;
        }
        _playlist.NotifyChanged();
    }

    private void AddFiles(string[]? files = null)
    {
        if (files == null)
        {
            using var ofd = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Audio Files|*.mp3;*.wav|All Files|*.*"
            };
            if (ofd.ShowDialog(this) != DialogResult.OK) return;
            files = ofd.FileNames;
        }
        foreach (var file in files)
        {
            if (!_playlist.CanAdd) break;
            _playlist.Add(new PlaylistItem
            {
                FilePath = file,
                DisplayName = Path.GetFileNameWithoutExtension(file),
                Volume = _master.Settings.Defaults.Volume,
                FadeDuration = _master.Settings.Defaults.FadeDuration
            });
        }
        Invalidate();
        AutoSaveQueue();
    }

    private void RemoveSelected()
    {
        if (_playlist.CurrentIndex >= 0)
            _playlist.RemoveAt(_playlist.CurrentIndex);
        AutoSaveQueue();
    }

    private void MoveSelectedUp() { _playlist.MoveUp(_playlist.CurrentIndex); AutoSaveQueue(); Invalidate(); }
    private void MoveSelectedDown() { _playlist.MoveDown(_playlist.CurrentIndex); AutoSaveQueue(); Invalidate(); }

    private void AddFavoriteToPlaylist(FavoriteItem fav)
    {
        if (!_playlist.CanAdd) return;
        _playlist.Add(new PlaylistItem
        {
            FilePath = fav.FilePath,
            DisplayName = fav.DisplayName,
            Volume = _master.Settings.Defaults.Volume,
            FadeDuration = _master.Settings.Defaults.FadeDuration
        });
    }

    private void PlayAt(int index, bool halfFade = false)
    {
        if (index < 0 || index >= _playlist.Count) return;
        _playingIndex = index;
        _playlist.SetCurrent(index);
        var track = _playlist.Tracks[index];
        _audio.SetVolume(track.Volume);
        _audio.Play(track, halfFade);
        CheckSoundBehaviorOnPlay();
        Invalidate();
    }

    private void TogglePlayPause()
    {
        if (_audio.State == PlayerState.Stopped)
        {
            if (_playlist.CurrentIndex >= 0)
                PlayAt(_playlist.CurrentIndex);
            else if (_playlist.Count > 0)
                PlayAt(0);
        }
        else
        {
            _audio.Pause();
        }
    }

    private void PlayNext()
    {
        if (_playlist.CurrentIndex >= 0 && _playlist.CurrentIndex != _playingIndex)
        {
            PlayAt(_playlist.CurrentIndex);
            return;
        }
        var next = _playingIndex + 1;
        if (next < _playlist.Count) PlayAt(next);
    }

    private void PlayPrev()
    {
        if (_playingIndex <= 0) return;
        PlayAt(_playingIndex - 1, true); // half fade for back
    }

    private void UpdateBgAudioDevice()
    {
        var devName = _master.Settings.OutputDevices.Count > 0 ? _master.Settings.OutputDevices[0] : null;
        _bgAudio.SetDeviceId(_devices.GetDeviceId(devName));
    }

    private void ClampScroll()
    {
        var h = PlaylistRect.Height;
        var maxScroll = 30 + _playlist.Tracks.Count * RowHeight - h;
        _scrollOffset = Math.Max(0, Math.Min(_scrollOffset, Math.Max(0, maxScroll)));
    }

    private void OnMouseWheel(object? sender, MouseEventArgs e)
    {
        var rect = PlaylistRect;
        if (rect.Contains(e.Location))
        {
            _scrollOffset -= e.Delta / 120 * RowHeight;
            ClampScroll();
            Invalidate();
        }
    }

    private void OnHotkey(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Space: TogglePlayPause(); break;
            case Keys.Right: PlayNext(); break;
            case Keys.Left: PlayPrev(); break;
            case Keys.Up: MoveSelectedUp(); break;
            case Keys.Down: MoveSelectedDown(); break;
            case Keys.Oemplus:
            case Keys.Add:
                _project.Current.GlobalVolume = Math.Min(1.0, _project.Current.GlobalVolume + 0.05);
                _audio.SetVolume(_project.Current.GlobalVolume);
                _project.MarkDirty();
                break;
            case Keys.OemMinus:
            case Keys.Subtract:
                _project.Current.GlobalVolume = Math.Max(0, _project.Current.GlobalVolume - 0.05);
                _audio.SetVolume(_project.Current.GlobalVolume);
                _project.MarkDirty();
                break;
            case Keys.Delete: RemoveSelected(); break;
        }
    }

    private void OnClosing(object? sender, FormClosingEventArgs e)
    {
        _uiTimer.Stop();
        _master.Settings.Window = new WindowSettings
        {
            X = Location.X, Y = Location.Y, Width = Width, Height = Height
        };
        _master.Save();
        if (!string.IsNullOrEmpty(_project.CurrentPath))
            _project.Save();
        AutoSaveQueue();
        _audio.Dispose();
        _bgAudio.Dispose();
    }

    private void RefreshUI()
    {
        if (!IsDisposed) Invalidate();
    }

    private static string FormatTime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.TotalHours >= 1
            ? $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}"
            : $"{ts.Minutes:D2}:{ts.Seconds:D2}";
    }

    private static string FormatTimePrecise(double seconds)
    {
        if (double.IsNaN(seconds) || seconds < 0) return "0.0";
        var ts = TimeSpan.FromSeconds(seconds);
        
        // For durations under a minute, show seconds only without leading zeros
        if (ts.TotalMinutes < 1)
        {
            return $"{ts.Seconds}.{ts.Milliseconds / 100:D1}";
        }
        
        // For durations under an hour, show minutes and seconds without leading zeros for minutes
        if (ts.TotalHours < 1)
        {
            return $"{ts.Minutes}:{ts.Seconds:D2}.{ts.Milliseconds / 100:D1}";
        }
        
        // For durations over an hour, show hours, minutes, and seconds
        return $"{ts.Hours}:{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds / 100:D1}";
    }

    private void ClearPlaylist()
    {
        if (_playlist.Count == 0) return;
        if (MessageBox.Show("Clear all tracks?", "Clear", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        _audio.Stop();
        _playingIndex = -1;
        _playlist.Clear();
        AutoSaveQueue();
    }

    private void AutoSaveQueue()
    {
        try { File.WriteAllText(_queuePath, JsonSerializer.Serialize(_playlist.Tracks, _jsonOpts)); } catch { }
    }

    private void AutoLoadQueue()
    {
        try
        {
            if (!File.Exists(_queuePath)) return;
            var data = JsonSerializer.Deserialize<List<PlaylistItem>>(File.ReadAllText(_queuePath), _jsonOpts);
            if (data == null || data.Count == 0) return;
            _playlist.Clear();
            foreach (var item in data)
            {
                if (File.Exists(item.FilePath))
                {
                    item.DisplayName = Path.GetFileName(item.FilePath);
                    _playlist.Add(item);
                }
            }
        }
        catch { }
    }
}
