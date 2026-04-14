namespace BlenderToolbox.Core.Abstractions;

public interface IJsonSettingsStore
{
    T Load<T>(string fileName) where T : new();

    void Save<T>(string fileName, T settings);
}
