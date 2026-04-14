namespace BlenderToolbox.Tools.SplitByContext.Services;

public sealed class SplitByContextOutputParser
{
    private const string SavingMarker = "SAVING::";

    public IReadOnlyList<string> ParseCreatedFiles(string processOutput)
    {
        if (string.IsNullOrWhiteSpace(processOutput))
        {
            return [];
        }

        return processOutput
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Where(static line => line.StartsWith(SavingMarker, StringComparison.Ordinal))
            .Select(static line => line[SavingMarker.Length..].Trim())
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .ToList();
    }
}
