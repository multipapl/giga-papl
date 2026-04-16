using System.Text.Json;
using System.Text.Json.Serialization;
using BlenderToolbox.Core.Abstractions;

namespace BlenderToolbox.Core.Services;

public sealed class JsonSettingsStore : IJsonSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _settingsDirectory;

    public JsonSettingsStore(string applicationName)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _settingsDirectory = Path.Combine(localAppData, applicationName);
    }

    public T Load<T>(string fileName) where T : new()
    {
        var path = GetSettingsPath(fileName);
        if (!File.Exists(path))
        {
            return new T();
        }

        try
        {
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<T>(stream, SerializerOptions) ?? new T();
        }
        catch
        {
            return new T();
        }
    }

    public void Save<T>(string fileName, T settings)
    {
        var path = GetSettingsPath(fileName);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = File.Create(path);
        JsonSerializer.Serialize(stream, settings, SerializerOptions);
    }

    private string GetSettingsPath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("A settings file name is required.", nameof(fileName));
        }

        return Path.Combine(_settingsDirectory, fileName);
    }
}
