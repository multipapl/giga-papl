namespace BlenderToolbox.Core.Abstractions;

public interface IToolDefinition
{
    string Id { get; }

    string DisplayName { get; }

    string Description { get; }

    object View { get; }
}
