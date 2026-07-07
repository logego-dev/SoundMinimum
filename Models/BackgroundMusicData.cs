namespace SoundMinimum.Models;

public class BackgroundMusicData
{
    public string Behavior { get; set; } = "soundsPauseMusic";
    public double Volume { get; set; } = 0.5; // 50% default volume
    public List<BgTrackItem> Tracks { get; set; } = new();
}

public class BgTrackItem
{
    public string FilePath { get; set; } = "";
    public string DisplayName { get; set; } = "";
}
