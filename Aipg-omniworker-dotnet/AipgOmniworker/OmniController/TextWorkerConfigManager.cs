using YamlDotNet.Serialization;

namespace AipgOmniworker.OmniController;

public class TextWorkerConfigManager
{
    private static string ConfigPath = "/worker/textWorkerConfig.yaml";
    
    public async Task<TextWorkerConfig> LoadConfig()
    {
        if (!File.Exists(ConfigPath))
        {
            return new TextWorkerConfig();
        }

        TextReader input = new StreamReader(ConfigPath);

        IDeserializer deserializer = new DeserializerBuilder()
            .Build();

        TextWorkerConfig config = deserializer.Deserialize<TextWorkerConfig>(input);

        if (config == null)
        {
            throw new Exception($"Failed to deserialize config from {ConfigPath}");
        }

        return config;
    }

    public async Task SaveConfig(TextWorkerConfig config)
    {
        ISerializer serializer = new SerializerBuilder().Build();
        string yaml = serializer.Serialize(config);
       
        await File.WriteAllTextAsync(ConfigPath, yaml);
    }
}
