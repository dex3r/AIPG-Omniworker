using YamlDotNet.Serialization;

namespace AipgOmniworker.OmniController;

public class ImageWorkerConfigManager
{
    private static string ConfigPath = "/image-worker/bridgeData.yaml";
    private static string ConfigTemplatePath = "/image-worker/bridgeData_template.yaml";
    
    public async Task<ImageWorkerConfig> LoadConfig()
    {
        if (!File.Exists(ConfigPath))
        {
            if(!File.Exists(ConfigTemplatePath))
            {
                throw new Exception($"Config template file not found under {ConfigTemplatePath}");
            }
            
            File.Copy(ConfigTemplatePath, ConfigPath);
            
            if(!File.Exists(ConfigPath))
            {
                throw new Exception($"Failed to copy config template file from {ConfigTemplatePath} to {ConfigPath}");
            }
        }

        TextReader input = new StreamReader(ConfigPath);

        IDeserializer deserializer = new DeserializerBuilder()
            .Build();

        ImageWorkerConfig config = deserializer.Deserialize<ImageWorkerConfig>(input);

        if (config == null)
        {
            throw new Exception($"Failed to deserialize image worker config from {ConfigPath}");
        }

        return config;
    }

    public async Task SaveConfig(ImageWorkerConfig config)
    {
        ISerializer serializer = new SerializerBuilder().Build();
        string yaml = serializer.Serialize(config);
       
        await File.WriteAllTextAsync(ConfigPath, yaml);
    }
}
