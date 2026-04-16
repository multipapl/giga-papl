namespace BlenderToolbox.Tools.RenderManager.Services;

public static class RenderCollectionOverrideParser
{
    public static IReadOnlyList<string> Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
