using SoundMinimum.Models;

namespace SoundMinimum.Services;

public class PlaylistManager
{
    private List<PlaylistItem> _tracks = new();
    private int _currentIndex = -1;

    public int Count => _tracks.Count;
    public int CurrentIndex => _currentIndex;
    public IReadOnlyList<PlaylistItem> Tracks => _tracks;
    public PlaylistItem? Current => _currentIndex >= 0 && _currentIndex < _tracks.Count ? _tracks[_currentIndex] : null;

    public event Action? OnChanged;

    public bool CanAdd => true; // No limit

    public bool Add(PlaylistItem item)
    {
        _tracks.Add(item);
        OnChanged?.Invoke();
        return true;
    }

    public void RemoveAt(int index)
    {
        if (index < 0 || index >= _tracks.Count) return;
        _tracks.RemoveAt(index);
        if (_currentIndex >= _tracks.Count)
            _currentIndex = _tracks.Count - 1;
        OnChanged?.Invoke();
    }

    public void MoveUp(int index)
    {
        if (index <= 0 || index >= _tracks.Count) return;
        (_tracks[index], _tracks[index - 1]) = (_tracks[index - 1], _tracks[index]);
        if (_currentIndex == index) _currentIndex = index - 1;
        else if (_currentIndex == index - 1) _currentIndex = index;
        OnChanged?.Invoke();
    }

    public void MoveDown(int index)
    {
        if (index < 0 || index >= _tracks.Count - 1) return;
        (_tracks[index], _tracks[index + 1]) = (_tracks[index + 1], _tracks[index]);
        if (_currentIndex == index) _currentIndex = index + 1;
        else if (_currentIndex == index + 1) _currentIndex = index;
        OnChanged?.Invoke();
    }

    public void SetTracks(List<PlaylistItem> tracks)
    {
        _tracks = tracks;
        _currentIndex = -1;
        OnChanged?.Invoke();
    }

    public void SetCurrent(int index)
    {
        _currentIndex = index;
        OnChanged?.Invoke();
    }

    public int GetNextIndex()
    {
        if (_tracks.Count == 0) return -1;
        var next = _currentIndex + 1;
        return next >= _tracks.Count ? -1 : next;
    }

    public int GetPrevIndex()
    {
        var prev = _currentIndex - 1;
        return prev < 0 ? -1 : prev;
    }

    public void Clear()
    {
        _tracks.Clear();
        _currentIndex = -1;
        OnChanged?.Invoke();
    }

    public void NotifyChanged() => OnChanged?.Invoke();
}
