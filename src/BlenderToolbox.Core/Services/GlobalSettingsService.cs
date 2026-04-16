using System.Text.Json;
using System.Text.Json.Serialization;
using BlenderToolbox.Core.Abstractions;
using BlenderToolbox.Core.Models;

namespace BlenderToolbox.Core.Services;

public sealed class GlobalSettingsService
{
    public const string GlobalSettingsFileName = "global.json";
    public const string RenderManagerSettingsFileName = "RenderManager\\settings.json";
    public const string SplitByContextSettingsFileName = "split-by-context.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _applicationName;
    private readonly IJsonSettingsStore _settingsStore;

    public GlobalSettingsService(IJsonSettingsStore settingsStore, string applicationName = "BlenderToolbox")
    {
        _settingsStore = settingsStore;
        _applicationName = applicationName;
        Current = _settingsStore.Load<GlobalSettings>(GlobalSettingsFileName);
    }

    public event EventHandler? Changed;

    public string AppDataFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        _applicationName);

    public GlobalSettings Current { get; private set; }

    public string LogFolder => Path.Combine(AppDataFolder, "RenderManager", "logs");

    public void Save(GlobalSettings next)
    {
        _settingsStore.Save(GlobalSettingsFileName, next);
        Current = Clone(next);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Reload()
    {
        Current = _settingsStore.Load<GlobalSettings>(GlobalSettingsFileName);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void MigrateLegacySettings()
    {
        var globalSettingsPath = GetSettingsPath(GlobalSettingsFileName);
        if (File.Exists(globalSettingsPath))
        {
            Current = _settingsStore.Load<GlobalSettings>(GlobalSettingsFileName);
            return;
        }

        var renderManagerSettings = _settingsStore.Load<LegacyRenderManagerSettings>(RenderManagerSettingsFileName);
        var splitByContextSettings = _settingsStore.Load<LegacySplitByContextSettings>(SplitByContextSettingsFileName);
        var blenderPath = FirstNonEmpty(
            renderManagerSettings.DefaultBlenderPath,
            splitByContextSettings.ExecutablePath);

        var migrated = new GlobalSettings
        {
            BlenderExecutablePath = blenderPath,
            ThemeOverride = Core.Presentation.ThemeOverride.Auto,
            LogsExpanded = true,
        };

        try
        {
            WriteGlobalSettingsAtomically(migrated);
            if (!File.Exists(globalSettingsPath))
            {
                return;
            }

            var roundTripped = _settingsStore.Load<GlobalSettings>(GlobalSettingsFileName);
            Current = roundTripped;

            renderManagerSettings.DefaultBlenderPath = string.Empty;
            splitByContextSettings.ExecutablePath = string.Empty;
            _settingsStore.Save(RenderManagerSettingsFileName, renderManagerSettings);
            _settingsStore.Save(SplitByContextSettingsFileName, splitByContextSettings);
        }
        catch
        {
            Current = migrated;
        }
    }

    private static GlobalSettings Clone(GlobalSettings settings)
    {
        return new GlobalSettings
        {
            BlenderExecutablePath = settings.BlenderExecutablePath,
            ThemeOverride = settings.ThemeOverride,
            LogsExpanded = settings.LogsExpanded,
        };
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private string GetSettingsPath(string fileName)
    {
        return Path.Combine(AppDataFolder, fileName);
    }

    private void WriteGlobalSettingsAtomically(GlobalSettings settings)
    {
        var path = GetSettingsPath(GlobalSettingsFileName);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        using (var stream = File.Create(tempPath))
        {
            JsonSerializer.Serialize(stream, settings, SerializerOptions);
        }

        File.Move(tempPath, path, overwrite: true);
    }

    private sealed class LegacyRenderManagerSettings
    {
        public string DefaultBlenderPath { get; set; } = string.Empty;

        public string LastBlendDirectory { get; set; } = string.Empty;

        public string LastBlenderDirectory { get; set; } = string.Empty;
    }

    private sealed class LegacySplitByContextSettings
    {
        public string ExecutablePath { get; set; } = string.Empty;

        public string SceneFilePath { get; set; } = string.Empty;
    }
}
