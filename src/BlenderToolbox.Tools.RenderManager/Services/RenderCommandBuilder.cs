using System.Diagnostics;
using System.IO;
using System.Text;
using BlenderToolbox.Tools.RenderManager.ViewModels;

namespace BlenderToolbox.Tools.RenderManager.Services;

public sealed class RenderCommandPlan
{
    public required string ExecutablePath { get; init; }

    public required IReadOnlyList<string> Arguments { get; init; }

    public required string ArgumentsDisplayText { get; init; }

    public required string OutputDirectory { get; init; }

    public required string OverrideScriptPath { get; init; }

    public required bool UsesRenderset { get; init; }

    public required bool UsesBlendOutputFallback { get; init; }

    public required string WorkingDirectory { get; init; }

    public ProcessStartInfo CreateStartInfo()
    {
        var startInfo = new ProcessStartInfo(ExecutablePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = WorkingDirectory,
        };

        foreach (var argument in Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }
}

public sealed class RenderCommandBuilder
{
    private readonly RenderManagerPaths _paths;
    private readonly RendersetRenderScriptBuilder _rendersetScriptBuilder;
    private readonly RenderOverrideScriptBuilder _scriptBuilder;

    public RenderCommandBuilder(
        RenderManagerPaths paths,
        RenderOverrideScriptBuilder scriptBuilder,
        RendersetRenderScriptBuilder? rendersetScriptBuilder = null)
    {
        _paths = paths;
        _scriptBuilder = scriptBuilder;
        _rendersetScriptBuilder = rendersetScriptBuilder ?? new RendersetRenderScriptBuilder();
    }

    public RenderCommandPlan Build(RenderQueueItemViewModel job, string globalBlenderPath, RenderResumePlan resumePlan)
    {
        var blenderPath = string.IsNullOrWhiteSpace(job.BlenderExecutablePath)
            ? globalBlenderPath.Trim()
            : job.BlenderExecutablePath.Trim();

        var arguments = new List<string>
        {
            "--background",
            job.BlendFilePath.Trim(),
        };

        if (job.UseRenderset)
        {
            return BuildRenderset(job, blenderPath, arguments);
        }

        if (job.HasSceneOverride)
        {
            arguments.Add("--scene");
            arguments.Add(job.SceneName.Trim());
        }

        var overrideScript = _scriptBuilder.Build(job);
        var overrideScriptPath = string.Empty;
        if (!string.IsNullOrWhiteSpace(overrideScript))
        {
            overrideScriptPath = Path.Combine(_paths.RuntimeDirectory, $"render_overrides_{job.Id}.py");
            File.WriteAllText(overrideScriptPath, overrideScript, Encoding.UTF8);
            arguments.Add("--python");
            arguments.Add(overrideScriptPath);
        }

        switch (job.Mode)
        {
            case Models.RenderMode.Animation:
                if (resumePlan.HasResumeStartFrame)
                {
                    arguments.Add("-s");
                    arguments.Add(resumePlan.ResumeStartFrame.ToString());
                }

                arguments.Add("--render-anim");
                break;

            case Models.RenderMode.FrameRange:
                arguments.Add("-s");
                arguments.Add(resumePlan.HasResumeStartFrame
                    ? resumePlan.ResumeStartFrame.ToString()
                    : job.ResolvedStartFrameText);
                arguments.Add("-e");
                arguments.Add(job.ResolvedEndFrameText);
                arguments.Add("-j");
                arguments.Add(job.ResolvedStepText);
                arguments.Add("-a");
                break;

            case Models.RenderMode.SingleFrame:
                arguments.Add("--render-frame");
                arguments.Add(job.ResolvedSingleFrameText);
                break;
        }

        return new RenderCommandPlan
        {
            ExecutablePath = blenderPath,
            Arguments = arguments,
            ArgumentsDisplayText = BuildDisplayText(blenderPath, arguments),
            OverrideScriptPath = overrideScriptPath,
            OutputDirectory = job.ResolvedOutputDirectory,
            UsesRenderset = false,
            UsesBlendOutputFallback = job.UsesOutputFallback,
            WorkingDirectory = Path.GetDirectoryName(job.BlendFilePath.Trim()) ?? _paths.RuntimeDirectory,
        };
    }

    private RenderCommandPlan BuildRenderset(
        RenderQueueItemViewModel job,
        string blenderPath,
        List<string> arguments)
    {
        var scriptPath = Path.Combine(_paths.RuntimeDirectory, $"renderset_render_{job.Id}.py");
        File.WriteAllText(scriptPath, _rendersetScriptBuilder.BuildScript(), Encoding.UTF8);

        arguments.Add("--python");
        arguments.Add(scriptPath);
        arguments.Add("--");
        arguments.Add(_rendersetScriptBuilder.BuildArgumentsJson(job.SelectedRendersetContextNames));

        return new RenderCommandPlan
        {
            ExecutablePath = blenderPath,
            Arguments = arguments,
            ArgumentsDisplayText = BuildDisplayText(blenderPath, arguments),
            OverrideScriptPath = scriptPath,
            OutputDirectory = string.Empty,
            UsesRenderset = true,
            UsesBlendOutputFallback = false,
            WorkingDirectory = Path.GetDirectoryName(job.BlendFilePath.Trim()) ?? _paths.RuntimeDirectory,
        };
    }

    private static string BuildDisplayText(string executablePath, IReadOnlyList<string> arguments)
    {
        var parts = new[] { Quote(executablePath) }
            .Concat(arguments.Select(Quote))
            .ToArray();
        return string.Join(" ", parts);
    }

    private static string Quote(string value)
    {
        return value.Any(char.IsWhiteSpace)
            ? $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\""
            : value;
    }
}
