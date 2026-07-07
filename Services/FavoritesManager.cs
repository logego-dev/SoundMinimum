using SoundMinimum.Models;

namespace SoundMinimum.Services;

public class FavoritesManager
{
    private readonly MasterSettingsService _master;

    public FavoritesManager(MasterSettingsService master)
    {
        _master = master;
    }

    public IReadOnlyList<FavoriteItem> Favorites => _master.Settings.Favorites;

    public void Add(string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);
        _master.AddFavorite(new FavoriteItem { FilePath = filePath, DisplayName = name });
    }

    public void Remove(string filePath)
    {
        _master.RemoveFavorite(filePath);
    }

    public bool IsFavorite(string filePath)
    {
        return _master.IsFavorite(filePath);
    }

    public void Toggle(string filePath)
    {
        if (IsFavorite(filePath))
            Remove(filePath);
        else
            Add(filePath);
    }
}
