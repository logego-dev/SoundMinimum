namespace SoundMinimum.Models;

public class MasterSettings
{
    public int Version { get; set; } = 1;
    public List<string> OutputDevices { get; set; } = new();
    public WindowSettings Window { get; set; } = new();
    public string LastProject { get; set; } = "";
    public List<RecentProjectEntry> RecentProjects { get; set; } = new();
    public List<FavoriteItem> Favorites { get; set; } = new();
    public BackgroundMusicData BackgroundMusic { get; set; } = new();
    public DefaultSettings Defaults { get; set; } = new();
    public AutoSaveSettings AutoSave { get; set; } = new();
}

public class WindowSettings
{
    public int X { get; set; } = 100;
    public int Y { get; set; } = 100;
    public int Width { get; set; } = 1100;
    public int Height { get; set; } = 720;
}

public class RecentProjectEntry
{
    public string Path { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime LastOpened { get; set; }
}

public class FavoriteItem
{
    public string FilePath { get; set; } = "";
    public string DisplayName { get; set; } = "";
}

public class DefaultSettings
{
    public double Volume { get; set; } = 0.8;
    public double FadeDuration { get; set; } = 3.0;
}

public class AutoSaveSettings
{
    public bool Enabled { get; set; } = true;
    public int BackupCount { get; set; } = 20;
    public bool SaveOnAction { get; set; } = true;
}
