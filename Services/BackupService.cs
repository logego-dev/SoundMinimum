namespace SoundMinimum.Services;

public class BackupService
{
    private readonly string _backupDir;
    private readonly int _maxBackups;

    public BackupService(string projectDir, int maxBackups = 20)
    {
        _backupDir = Path.Combine(projectDir, "backups");
        _maxBackups = maxBackups;
        if (!Directory.Exists(_backupDir))
            Directory.CreateDirectory(_backupDir);
    }

    public void CreateBackup(string projectFilePath)
    {
        if (!File.Exists(projectFilePath)) return;
        var name = Path.GetFileNameWithoutExtension(projectFilePath);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupPath = Path.Combine(_backupDir, $"{name}_{timestamp}.smproj.bak");
        File.Copy(projectFilePath, backupPath, true);
        RotateBackups(name);
    }

    private void RotateBackups(string projectName)
    {
        var files = Directory.GetFiles(_backupDir, $"{projectName}_*.smproj.bak")
            .OrderByDescending(f => f)
            .ToList();
        foreach (var f in files.Skip(_maxBackups))
            File.Delete(f);
    }

    public List<string> GetBackups(string projectName)
    {
        return Directory.GetFiles(_backupDir, $"{projectName}_*.smproj.bak")
            .OrderByDescending(f => f)
            .ToList();
    }
}
