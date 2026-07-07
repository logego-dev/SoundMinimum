namespace SoundMinimum.Models;

public class PlaylistItem
{
    public string FilePath { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public double Volume { get; set; } = 0.8;
    public bool Loop { get; set; }
    public bool Crossfade { get; set; } = true;
    public double FadeDuration { get; set; } = 2.0;
}
