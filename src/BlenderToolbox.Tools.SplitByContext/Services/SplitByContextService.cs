using System.Diagnostics;
using System.IO;
using System.Text;
using BlenderToolbox.Tools.SplitByContext.Models;

namespace BlenderToolbox.Tools.SplitByContext.Services;

public sealed class SplitByContextService
{
    private readonly SplitByContextOutputParser _outputParser;
    private readonly SplitByContextScriptBuilder _scriptBuilder;
    private readonly string _workingDirectory;

    public SplitByContextService(
        SplitByContextScriptBuilder? scriptBuilder = null,
        SplitByContextOutputParser? outputParser = null,
        string applicationName = "BlenderToolbox")
    {
        _scriptBuilder = scriptBuilder ?? new SplitByContextScriptBuilder();
        _outputParser = outputParser ?? new SplitByContextOutputParser();
        _workingDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            applicationName,
            "SplitByContext");
    }

    public async Task<SplitByContextResult> SplitAsync(
        SplitByContextRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Validate(request);
        Directory.CreateDirectory(_workingDirectory);

        var scriptPath = Path.Combine(_workingDirectory, "split_by_context.py");
        var logPath = Path.Combine(_workingDirectory, "split_by_context.log");
        await File.WriteAllTextAsync(scriptPath, _scriptBuilder.BuildScript(), Encoding.UTF8, cancellationToken);

        var startInfo = new ProcessStartInfo(request.ExecutablePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(request.SceneFilePath) ?? _workingDirectory,
        };

        startInfo.ArgumentList.Add("--background");
        startInfo.ArgumentList.Add(request.SceneFilePath);
        startInfo.ArgumentList.Add("--python");
        startInfo.ArgumentList.Add(scriptPath);

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync(cancellationToken);

        var standardOutput = await standardOutputTask;
        var standardError = await standardErrorTask;
        await File.WriteAllTextAsync(logPath, BuildLog(standardOutput, standardError), Encoding.UTF8, cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Split failed. See log: {logPath}");
        }

        var createdFiles = _outputParser.ParseCreatedFiles(standardOutput);
        return new SplitByContextResult(createdFiles, logPath);
    }

    private static string BuildLog(string standardOutput, string standardError)
    {
        var builder = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(standardOutput))
        {
            builder.AppendLine(standardOutput.TrimEnd());
        }

        if (!string.IsNullOrWhiteSpace(standardError))
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.AppendLine(standardError.TrimEnd());
        }

        return builder.ToString();
    }

    private static void Validate(SplitByContextRequest request)
    {
        if (!File.Exists(request.ExecutablePath))
        {
            throw new InvalidOperationException("Executable path is invalid.");
        }

        if (!File.Exists(request.SceneFilePath))
        {
            throw new InvalidOperationException("Scene file was not found.");
        }

        if (!string.Equals(Path.GetExtension(request.SceneFilePath), ".blend", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Scene file must use the .blend extension.");
        }
    }
}
