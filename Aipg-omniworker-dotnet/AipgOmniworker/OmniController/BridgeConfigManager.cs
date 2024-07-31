using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AipgOmniworker.OmniController;

public class BridgeConfigManager
{
    private static string ConfigPath = "/worker/bridgeData.yaml";
    private static string ConfigTemplatePath = "/worker/bridgeData_template.yaml";
    
    public async Task<BridgeConfig> LoadConfig()
    {
        bool wasJustCreated = false;
        
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

            wasJustCreated = true;
        }

        TextReader input = new StreamReader(ConfigPath);

        IDeserializer deserializer = new DeserializerBuilder()
            .Build();

        BridgeConfig config = deserializer.Deserialize<BridgeConfig>(input);

        if (config == null)
        {
            throw new Exception($"Failed to deserialize config from {ConfigPath}");
        }

        if (wasJustCreated)
        {
            if (Environment.GetEnvironmentVariable("GRID_API_KEY") != null)
            {
                config.api_key = Environment.GetEnvironmentVariable("GRID_API_KEY");
            }
            
            if (Environment.GetEnvironmentVariable("WORKER_NAME") != null)
            {
                config.worker_name = Environment.GetEnvironmentVariable("WORKER_NAME");
            }

            await SaveConfig(config);
        }

        return config;
    }

    public async Task SaveConfig(BridgeConfig config)
    {
        ISerializer serializer = new SerializerBuilder().Build();
        string yaml = serializer.Serialize(config);
       
        await File.WriteAllTextAsync(ConfigPath, yaml);
    }
}
