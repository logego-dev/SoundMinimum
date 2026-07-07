using Newtonsoft.Json;

namespace SoundMinimum;

public static class Loc
{
    private static Dictionary<string, string> _strings = new();

    public static void Load(string filePath)
    {
        if (File.Exists(filePath))
        {
            try
            {
                var json = File.ReadAllText(filePath);
                _strings = JsonConvert.DeserializeObject<Dictionary<string, string>>(json) ?? new();
                return;
            }
            catch { }
        }
        _strings = new();
    }

    public static string Get(string key, params object[] args)
    {
        if (_strings.TryGetValue(key, out var val))
            return args.Length > 0 ? string.Format(val, args) : val;
        return key;
    }
}
