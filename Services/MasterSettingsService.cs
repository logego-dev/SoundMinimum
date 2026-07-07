using SoundMinimum.Models;

namespace SoundMinimum.Services;

public class MasterSettingsService
{
    private readonly string _filePath;
    public MasterSettings Settings { get; private set; }

    public MasterSettingsService(string filePath)
    {
        _filePath = filePath;
        Settings = SerializationService.Load<MasterSettings>(filePath);
    }

    public void Save()
    {
        SerializationService.Save(_filePath, Settings);
    }

    public void AddRecentProject(string path, string name)
    {
        Settings.RecentProjects.RemoveAll(r => r.Path == path);
        Settings.RecentProjects.Insert(0, new RecentProjectEntry
        {
            Path = path,
            Name = name,
            LastOpened = DateTime.Now
        });
        if (Settings.RecentProjects.Count > 10)
            Settings.RecentProjects = Settings.RecentProjects.Take(10).ToList();
        Save();
    }

    public void AddFavorite(FavoriteItem item)
    {
        if (!Settings.Favorites.Any(f => f.FilePath == item.FilePath))
        {
            Settings.Favorites.Add(item);
            Save();
        }
    }

    public void RemoveFavorite(string filePath)
    {
        Settings.Favorites.RemoveAll(f => f.FilePath == filePath);
        Save();
    }

    public bool IsFavorite(string filePath)
    {
        return Settings.Favorites.Any(f => f.FilePath == filePath);
    }
}
