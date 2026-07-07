namespace SoundMinimum.Models;

public class ProjectData
{
    public int Version { get; set; } = 1;
    public string Name { get; set; } = "Новый проект";
    public double GlobalVolume { get; set; } = 0.85;
    public bool AutoPlay { get; set; } = true;
    public List<PlaylistItem> Tracks { get; set; } = new();
}
