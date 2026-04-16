using System.IO;

namespace BlenderToolbox.Tools.RenderManager.Services;

public sealed class RenderManagerPaths
{
    public const string ApplicationName = "BlenderToolbox";
    public const string ToolDirectoryName = "RenderManager";
    public const string QueueFileName = "RenderManager\\queue.json";
    public const string SettingsFileName = "RenderManager\\settings.json";

    private readonly string _toolRootDirectory;

    public RenderManagerPaths(string applicationName = ApplicationName)
    {
        _toolRootDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            applicationName,
            ToolDirectoryName);
    }

    public string InspectionDirectory => EnsureDirectory("inspection");

    public string LogsDirectory => EnsureDirectory("logs");

    public string RuntimeDirectory => EnsureDirectory("runtime");

    public string CreateJobLogPath(string jobId)
    {
        var safeJobId = string.IsNullOrWhiteSpace(jobId) ? "job" : jobId.Trim();
        return Path.Combine(LogsDirectory, $"{safeJobId}_{DateTimeOffset.Now:yyyyMMdd_HHmmss}.log");
    }

    private string EnsureDirectory(string name)
    {
        var path = Path.Combine(_toolRootDirectory, name);
        Directory.CreateDirectory(path);
        return path;
    }
}
