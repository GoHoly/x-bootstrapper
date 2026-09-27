using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Caelus.Core;

/// <summary>
/// JSON settings file shared by several X Bootstrapper processes (menu, website launches).
/// Saves are atomic (temp file + replace, previous copy kept as .bak), serialized across processes
/// with a named mutex, and merged: only the properties this process actually changed are written,
/// so a game launch finishing later can't undo changes made in the menu meanwhile.
/// </summary>
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
    private readonly string _mutexName;
    private JsonObject _baseline = new();

    public T Prop { get; private set; } = new();

    public JsonStore(string path)
    {
        _path = path;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(path).ToLowerInvariant())))[..16];
        _mutexName = $"XBootstrapper.Store.{hash}";
    }

    public void Load()
    {
        WithFileLock(() =>
        {
            if (TryRead(_path, out var prop, out var node) || TryRead(_path + ".bak", out prop, out node))
            {
                Prop = prop!;
                // Baseline = what this process currently believes; later saves write only the differences.
                _baseline = Serialize(Prop);
                return;
            }

            if (File.Exists(_path))
            {
                // Unreadable and no usable backup: keep the broken file for inspection instead of overwriting it silently.
                var corrupt = $"{_path}.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}";
                try
                {
                    File.Copy(_path, corrupt, overwrite: true);
                    Logger.Write("JsonStore", $"{Path.GetFileName(_path)} could not be read; kept a copy at {corrupt}");
                }
                catch (Exception ex)
                {
                    Logger.Error("JsonStore", ex);
                }
            }

            Prop = new T();
            _baseline = new JsonObject();
        });

        if (!File.Exists(_path))
            Save();
    }

    public void Save()
    {
        WithFileLock(() =>
        {
            var mine = Serialize(Prop);
            JsonObject result;
            if (TryReadNode(_path, out var disk))
            {
                result = disk!;
                foreach (var (key, value) in mine)
                {
                    if (!_baseline.TryGetPropertyValue(key, out var before) || !JsonNode.DeepEquals(before, value))
                        result[key] = value?.DeepClone();
                }

                foreach (var (key, _) in _baseline)
                {
                    if (!mine.ContainsKey(key))
                        result.Remove(key);
                }
            }
            else
            {
                result = (JsonObject)mine.DeepClone();
            }

            var json = result.ToJsonString(Options);
            if (File.Exists(_path) && File.ReadAllText(_path) == json)
            {
                _baseline = mine;
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temp = _path + ".tmp";
            File.WriteAllText(temp, json);
            if (File.Exists(_path))
                File.Replace(temp, _path, _path + ".bak", ignoreMetadataErrors: true);
            else
                File.Move(temp, _path);

            _baseline = mine;
        });
    }

    private static JsonObject Serialize(T value) =>
        JsonSerializer.SerializeToNode(value, Options) as JsonObject ?? new JsonObject();

    private static bool TryRead(string path, out T? prop, out JsonObject? node)
    {
        prop = null;
        node = null;
        try
        {
            if (!File.Exists(path))
                return false;

            var json = File.ReadAllText(path);
            prop = JsonSerializer.Deserialize<T>(json, Options);
            node = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip }) as JsonObject;
            return prop is not null && node is not null;
        }
        catch (Exception ex)
        {
            Logger.Write("JsonStore", $"Could not read {path}: {ex.Message}");
            return false;
        }
    }

    private static bool TryReadNode(string path, out JsonObject? node)
    {
        node = null;
        try
        {
            if (!File.Exists(path))
                return false;
            node = JsonNode.Parse(File.ReadAllText(path), documentOptions: new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip }) as JsonObject;
            return node is not null;
        }
        catch
        {
            return false;
        }
    }

    private void WithFileLock(Action action)
    {
        Mutex? mutex = null;
        var owned = false;
        try
        {
            mutex = new Mutex(false, _mutexName);
            try
            {
                owned = mutex.WaitOne(TimeSpan.FromSeconds(5));
            }
            catch (AbandonedMutexException)
            {
                owned = true;
            }

            action();
        }
        finally
        {
            if (owned)
                mutex?.ReleaseMutex();
            mutex?.Dispose();
        }
    }
}
