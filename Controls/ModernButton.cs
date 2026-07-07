using System.Drawing.Drawing2D;
using SoundMinimum.Themes;

namespace SoundMinimum.Controls;

public class ModernButton : Button
{
    private bool _isHovered;
    private bool _isPressed;

    public ModernButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Font = DarkTheme.FontNormal;
        ForeColor = DarkTheme.FgText;
        BackColor = DarkTheme.BgInput;
        FlatStyle = FlatStyle.Flat;
        Size = new Size(80, 30);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using var bg = new SolidBrush(_isPressed ? DarkTheme.BgSelected :
                                      _isHovered ? DarkTheme.BgHover : DarkTheme.BgInput);
        using var border = new Pen(DarkTheme.BorderColor);
        using var textBrush = new SolidBrush(Enabled ? ForeColor : DarkTheme.FgDim);

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        g.FillRoundedRect(bg, rect, DarkTheme.ButtonRadius);
        g.DrawRoundedRect(border, rect, DarkTheme.ButtonRadius);

        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(Text, Font, textBrush, ClientRectangle, sf);
    }

    protected override void OnMouseEnter(EventArgs e) { _isHovered = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { _isHovered = false; _isPressed = false; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { _isPressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _isPressed = false; Invalidate(); base.OnMouseUp(e); }
}
