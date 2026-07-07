using System.Drawing.Drawing2D;
using SoundMinimum.Themes;

namespace SoundMinimum.Controls;

public class ModernTrackBar : Control
{
    private int _value = 50;
    private bool _isDragging;

    public int Min { get; set; } = 0;
    public int Max { get; set; } = 100;

    public int Value
    {
        get => _value;
        set { _value = Math.Clamp(value, Min, Max); Invalidate(); ValueChanged?.Invoke(this, EventArgs.Empty); }
    }

    public double Normalized => (double)(_value - Min) / (Max - Min);

    public event EventHandler? ValueChanged;

    public ModernTrackBar()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer, true);
        Height = 24;
        Width = 200;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var trackRect = new Rectangle(4, Height / 2 - 2, Width - 8, 4);
        var fillWidth = (int)(trackRect.Width * Normalized);

        using var trackBg = new SolidBrush(DarkTheme.BgInput);
        using var trackFill = new SolidBrush(DarkTheme.FgAccent);
        using var thumbBrush = new SolidBrush(DarkTheme.FgBright);
        using var border = new Pen(DarkTheme.BorderColor);

        g.FillRoundedRect(trackBg, trackRect, 2);

        if (fillWidth > 0)
        {
            var fillRect = new Rectangle(trackRect.X, trackRect.Y, fillWidth, trackRect.Height);
            g.FillRoundedRect(trackFill, fillRect, 2);
        }

        var thumbX = trackRect.X + fillWidth;
        var thumbY = Height / 2;
        g.FillEllipse(thumbBrush, thumbX - 5, thumbY - 5, 10, 10);
        g.DrawEllipse(border, thumbX - 5, thumbY - 5, 10, 10);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) { _isDragging = true; UpdateValue(e.X); }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_isDragging) UpdateValue(e.X);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _isDragging = false;
    }

    private void UpdateValue(int mouseX)
    {
        var trackWidth = Width - 8;
        if (trackWidth <= 0) return;
        var ratio = (double)(mouseX - 4) / trackWidth;
        Value = (int)(Min + ratio * (Max - Min));
    }
}
