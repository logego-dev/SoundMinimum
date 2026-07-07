using System.Text.Json;
using System.Text.Json.Serialization;

namespace SoundMinimum.Services;

public static class SerializationService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static void Save<T>(string filePath, T data)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(data, Options);
        File.WriteAllText(filePath, json);
    }

    public static T Load<T>(string filePath) where T : new()
    {
        if (!File.Exists(filePath)) return new T();
        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<T>(json, Options) ?? new T();
        }
        catch
        {
            return new T();
        }
    }
}
