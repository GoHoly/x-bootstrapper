using System.Text.Json;
using System.Text.Json.Serialization;

namespace Caelus.Core;

public sealed class JsonStore<T> where T : class, new()
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _path;

    public T Prop { get; private set; } = new();

    public JsonStore(string path)
    {
        _path = path;
    }

    public void Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                Save();
                return;
            }

            var json = File.ReadAllText(_path);
            Prop = JsonSerializer.Deserialize<T>(json, Options) ?? new T();
        }
        catch (Exception ex)
        {
            Logger.Error("JsonStore", ex);
            Prop = new T();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(Prop, Options));
    }
}
