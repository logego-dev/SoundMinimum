namespace SoundMinimum.Themes;

public static class DarkTheme
{
    public static Color BgMain = Color.FromArgb(18, 18, 22);
    public static Color BgPanel = Color.FromArgb(28, 28, 34);
    public static Color BgInput = Color.FromArgb(38, 38, 44);
    public static Color BgHover = Color.FromArgb(48, 48, 54);
    public static Color BgSelected = Color.FromArgb(58, 58, 64);
    public static Color BgHeader = Color.FromArgb(32, 32, 38);
    public static Color BgTitleBar = Color.FromArgb(12, 12, 16);
    public static Color BgPlaying = Color.FromArgb(22, 38, 34);
    public static Color BgFavPanel = Color.FromArgb(22, 22, 26);

    public static Color FgText = Color.FromArgb(230, 230, 235);
    public static Color FgDim = Color.FromArgb(140, 140, 150);
    public static Color FgBright = Color.FromArgb(250, 250, 255);
    public static Color FgAccent = Color.FromArgb(0, 200, 150);
    public static Color FgAccentDim = Color.FromArgb(0, 160, 120);
    public static Color FgOrange = Color.FromArgb(255, 185, 50);
    public static Color FgRed = Color.FromArgb(230, 80, 70);

    public static Color BorderColor = Color.FromArgb(48, 48, 54);
    public static Color BorderLight = Color.FromArgb(68, 68, 74);

    public static Font FontNormal = new Font("Segoe UI", 9.5f, FontStyle.Regular);
    public static Font FontBold = new Font("Segoe UI", 9.5f, FontStyle.Bold);
    public static Font FontSmall = new Font("Segoe UI", 8f, FontStyle.Regular);
    public static Font FontHeader = new Font("Segoe UI", 9.5f, FontStyle.Bold);
    public static Font FontTitle = new Font("Segoe UI", 11f, FontStyle.Bold);
    public static Font FontMono = new Font("Consolas", 8.5f, FontStyle.Regular);

    public const int RowHeight = 30;
    public const int HeaderHeight = 28;
    public const int TitleBarHeight = 38;
    public const int ToolbarHeight = 34;
    public const int TransportHeight = 46;
    public const int FadeInactive = 16;
    public const int ButtonRadius = 5;
    public const int Padding = 10;
}
