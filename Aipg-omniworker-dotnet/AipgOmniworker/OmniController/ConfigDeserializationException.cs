namespace AipgOmniworker.OmniController;

[Serializable]
public class ConfigDeserializationException : Exception
{
    public string ConfigType { get; private set; }
    public string Path { get; private set; }
    public string Content { get; private set; }

    public ConfigDeserializationException(string configType, string path, string content)
        : base($"Failed to deserialize {configType} config from {path}")
    {
        ConfigType = configType;
        Path = path;
        Content = content;
    }
    
    public ConfigDeserializationException(string configType, string path, string content, Exception innerException)
        : base($"Failed to deserialize {configType} config from {path}", innerException)
    {
        ConfigType = configType;
        Path = path;
        Content = content;
    }
}
