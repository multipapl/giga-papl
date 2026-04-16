using System.Text.Json;
using BlenderToolbox.Tools.RenderManager.Models;

namespace BlenderToolbox.Tools.RenderManager.Services;

public static class RendersetOutputParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static RendersetRenderEvent? TryParse(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        return TryParseMarker(line, "<<RSET_JOB>>", RendersetRenderEventKind.Job)
               ?? TryParseMarker(line, "<<RSET_START>>", RendersetRenderEventKind.Start)
               ?? TryParseMarker(line, "<<RSET_FRAME>>", RendersetRenderEventKind.Frame)
               ?? TryParseMarker(line, "<<RSET_DONE>>", RendersetRenderEventKind.Done)
               ?? TryParseMarker(line, "<<RSET_ERROR>>", RendersetRenderEventKind.Error)
               ?? TryParseMarker(line, "<<RSET_ALL_DONE>>", RendersetRenderEventKind.AllDone);
    }

    private static RendersetRenderEvent? TryParseMarker(
        string line,
        string marker,
        RendersetRenderEventKind kind)
    {
        if (!line.StartsWith(marker, StringComparison.Ordinal))
        {
            return null;
        }

        var json = line[marker.Length..].Trim();
        if (string.IsNullOrWhiteSpace(json))
        {
            return new RendersetRenderEvent { Kind = kind };
        }

        var parsed = JsonSerializer.Deserialize<RendersetRenderEvent>(json, Options)
                     ?? new RendersetRenderEvent();
        parsed.Kind = kind;
        return parsed;
    }
}
