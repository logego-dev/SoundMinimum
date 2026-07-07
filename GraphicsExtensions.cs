using System.Drawing.Drawing2D;

public static class GraphicsExtensions
{
    public static void FillRoundedRect(this Graphics g, Brush brush, Rectangle rect, int radius)
    {
        if (rect.Width <= 0 || rect.Height <= 0) return;
        using var path = GetRoundedPath(rect, radius);
        g.FillPath(brush, path);
    }

    public static void DrawRoundedRect(this Graphics g, Pen pen, Rectangle rect, int radius)
    {
        if (rect.Width <= 0 || rect.Height <= 0) return;
        using var path = GetRoundedPath(rect, radius);
        g.DrawPath(pen, path);
    }

    private static GraphicsPath GetRoundedPath(Rectangle rect, int r)
    {
        var path = new GraphicsPath();
        r = Math.Min(r, Math.Min(rect.Width / 2, rect.Height / 2));
        if (r <= 0)
        {
            path.AddRectangle(rect);
            return path;
        }
        var d = r * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
