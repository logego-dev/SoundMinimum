using SoundMinimum.Models;

namespace SoundMinimum.Services;

public class ProjectManager
{
    private readonly string _projectsDir;
    private readonly MasterSettingsService _master;
    private readonly BackupService _backup;
    private string? _currentPath;
    private bool _dirty;

    public ProjectData Current { get; private set; } = new();
    public string? CurrentPath => _currentPath;
    public bool IsDirty => _dirty;
    public BackupService Backup => _backup;

    public event Action? OnProjectChanged;

    public ProjectManager(MasterSettingsService master)
    {
        _master = master;
        var baseDir = Path.GetDirectoryName(master.Settings.LastProject);
        if (string.IsNullOrEmpty(baseDir))
            baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "projects");
        _projectsDir = baseDir;
        if (!Directory.Exists(_projectsDir))
            Directory.CreateDirectory(_projectsDir);
        _backup = new BackupService(_projectsDir, master.Settings.AutoSave.BackupCount);

        if (!string.IsNullOrEmpty(master.Settings.LastProject) && File.Exists(master.Settings.LastProject))
            Open(master.Settings.LastProject);
    }

    public void New()
    {
        Current = new ProjectData();
        _currentPath = null;
        _dirty = true;
        OnProjectChanged?.Invoke();
    }

    public bool Open(string path)
    {
        if (!File.Exists(path)) return false;
        Current = SerializationService.Load<ProjectData>(path);
        _currentPath = path;
        _dirty = false;
        _master.Settings.LastProject = path;
        _master.AddRecentProject(path, Current.Name);
        OnProjectChanged?.Invoke();
        return true;
    }

    public void Save()
    {
        if (string.IsNullOrEmpty(_currentPath))
        {
            _currentPath = Path.Combine(_projectsDir, $"{Current.Name}.smproj");
        }
        SaveAs(_currentPath);
    }

    public void SaveAs(string path)
    {
        _backup.CreateBackup(path);
        SerializationService.Save(path, Current);
        _currentPath = path;
        _dirty = false;
        _master.Settings.LastProject = path;
        _master.AddRecentProject(path, Current.Name);
        OnProjectChanged?.Invoke();
    }

    public void MarkDirty()
    {
        _dirty = true;
        if (_master.Settings.AutoSave.SaveOnAction && !string.IsNullOrEmpty(_currentPath))
        {
            SaveAs(_currentPath);
            _dirty = false;
        }
        OnProjectChanged?.Invoke();
    }

    public string[] GetProjectFiles()
    {
        return Directory.GetFiles(_projectsDir, "*.smproj");
    }

    public string ProjectsDir => _projectsDir;
}
