using System.IO;
using System.Text;

namespace BlenderToolbox.Tools.RenderManager.Services;

public sealed class RenderJobLogWriter
{
    public void AppendLine(string? logFilePath, string line)
    {
        if (string.IsNullOrWhiteSpace(logFilePath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(logFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.AppendAllText(logFilePath, line + Environment.NewLine, Encoding.UTF8);
    }
}
