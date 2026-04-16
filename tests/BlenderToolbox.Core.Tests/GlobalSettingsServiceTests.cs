using System.Text.Json;
using BlenderToolbox.Core.Models;
using BlenderToolbox.Core.Presentation;
using BlenderToolbox.Core.Services;

namespace BlenderToolbox.Core.Tests;

public sealed class GlobalSettingsServiceTests
{
    [Fact]
    public void GlobalSettingsService_LoadSaveRoundTrip()
    {
        using var scope = new AppSettingsScope();
        var service = scope.CreateService();

        service.Save(new GlobalSettings
        {
            BlenderExecutablePath = @"C:\Blender\blender.exe",
            ThemeOverride = ThemeOverride.Dark,
            LogsExpanded = false,
        });

        var reloaded = scope.CreateService();

        Assert.Equal(@"C:\Blender\blender.exe", reloaded.Current.BlenderExecutablePath);
        Assert.Equal(ThemeOverride.Dark, reloaded.Current.ThemeOverride);
        Assert.False(reloaded.Current.LogsExpanded);
    }

    [Fact]
    public void GlobalSettingsService_MigratesRenderManagerDefaultBlenderPath()
    {
        using var scope = new AppSettingsScope();
        scope.WriteJson(GlobalSettingsService.RenderManagerSettingsFileName, new
        {
            DefaultBlenderPath = @"C:\Blender\rm.exe",
            LastBlendDirectory = @"Q:\shots",
            LastBlenderDirectory = @"C:\Blender",
        });

        var service = scope.CreateService();
        service.MigrateLegacySettings();

        Assert.Equal(@"C:\Blender\rm.exe", service.Current.BlenderExecutablePath);
        Assert.Equal(string.Empty, scope.ReadString(GlobalSettingsService.RenderManagerSettingsFileName, "DefaultBlenderPath"));
    }

    [Fact]
    public void GlobalSettingsService_MigratesSplitByContextPathWhenRenderManagerPathIsEmpty()
    {
        using var scope = new AppSettingsScope();
        scope.WriteJson(GlobalSettingsService.RenderManagerSettingsFileName, new
        {
            DefaultBlenderPath = string.Empty,
        });
        scope.WriteJson(GlobalSettingsService.SplitByContextSettingsFileName, new
        {
            ExecutablePath = @"C:\Blender\split.exe",
            SceneFilePath = @"Q:\scene.blend",
        });

        var service = scope.CreateService();
        service.MigrateLegacySettings();

        Assert.Equal(@"C:\Blender\split.exe", service.Current.BlenderExecutablePath);
        Assert.Equal(string.Empty, scope.ReadString(GlobalSettingsService.SplitByContextSettingsFileName, "ExecutablePath"));
    }

    [Fact]
    public void GlobalSettingsService_SkipsMigrationWhenGlobalSettingsExist()
    {
        using var scope = new AppSettingsScope();
        scope.WriteJson(GlobalSettingsService.GlobalSettingsFileName, new
        {
            BlenderExecutablePath = @"C:\Blender\global.exe",
            ThemeOverride = "Light",
            LogsExpanded = true,
        });
        scope.WriteJson(GlobalSettingsService.RenderManagerSettingsFileName, new
        {
            DefaultBlenderPath = @"C:\Blender\legacy.exe",
        });

        var service = scope.CreateService();
        service.MigrateLegacySettings();

        Assert.Equal(@"C:\Blender\global.exe", service.Current.BlenderExecutablePath);
        Assert.Equal(@"C:\Blender\legacy.exe", scope.ReadString(GlobalSettingsService.RenderManagerSettingsFileName, "DefaultBlenderPath"));
    }

    private sealed class AppSettingsScope : IDisposable
    {
        public AppSettingsScope()
        {
            ApplicationName = $"BlenderToolbox.Tests.{Guid.NewGuid():N}";
            RootPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ApplicationName);
        }

        public string ApplicationName { get; }

        public string RootPath { get; }

        public GlobalSettingsService CreateService()
        {
            return new GlobalSettingsService(new JsonSettingsStore(ApplicationName), ApplicationName);
        }

        public string ReadString(string fileName, string propertyName)
        {
            var path = Path.Combine(RootPath, fileName);
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.TryGetProperty(propertyName, out var property)
                ? property.GetString() ?? string.Empty
                : string.Empty;
        }

        public void WriteJson(string fileName, object value)
        {
            var path = Path.Combine(RootPath, fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(value));
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(RootPath))
                {
                    Directory.Delete(RootPath, recursive: true);
                }
            }
            catch
            {
                // best effort cleanup for test temp files
            }
        }
    }
}
