using SoundMinimum.Forms;

namespace SoundMinimum;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        Loc.Load(Path.Combine(basePath, "lang.json"));
        var files = args.Where(a => File.Exists(a) && a.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)).ToArray();
        Application.Run(new MainForm(files));
    }
}