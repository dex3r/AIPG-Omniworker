using YamlDotNet.Serialization;

namespace AipgOmniworker.OmniController;

public abstract class YamlConfigManager<T>(PersistentStorage persistentStorage, ILogger logger)
    where T : class, new()
{
    public abstract string ConfigName { get; }
    public abstract string ConfigPath { get; }
    public abstract string? ConfigTemplatePath { get; }
    
    public async Task<T> LoadConfig()
    {
        bool wasJustCreated = false;
        
        if (!File.Exists(ConfigPath))
        {
            await CreateDefaultConfig();
            wasJustCreated = true;
        }

        TextReader input = new StreamReader(ConfigPath);

        IDeserializer deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .Build();

        T config = deserializer.Deserialize<T>(input);

        if (config == null)
        {
            throw new Exception($"Failed to deserialize config from {ConfigPath}");
        }
        
        await OnConfigLoaded(config);
        
        return config;
    }

    public virtual Task OnConfigLoaded(T config)
    {
        return Task.CompletedTask;
    }

    private async Task CreateDefaultConfig()
    {
        if (await persistentStorage.HasFile(ConfigName))
        {
            logger.LogInformation("Copying config {ConfigName} from existing persistent storage to {ConfigPath}",
                ConfigName, ConfigPath);
            await persistentStorage.CopyConfigFromPersistentStorage(ConfigName, ConfigPath);
            return;
        }

        if (ConfigTemplatePath == null)
        {
            logger.LogInformation("Creating default config {ConfigPath} from scratch", ConfigPath);
            await SaveConfig(new T());
            return;
        }
            
        if(!File.Exists(ConfigTemplatePath))
        {
            throw new Exception($"Config template file not found under {ConfigTemplatePath}");
        }
        
        logger.LogInformation("Copying config template {ConfigTemplatePath} to {ConfigPath}",
            ConfigTemplatePath, ConfigPath);
        File.Copy(ConfigTemplatePath, ConfigPath);
            
        if(!File.Exists(ConfigPath))
        {
            throw new Exception($"Failed to copy config template file from {ConfigTemplatePath} to {ConfigPath}");
        }
    }

    public async Task SaveConfig(T config)
    {
        ISerializer serializer = new SerializerBuilder().Build();
        string yaml = serializer.Serialize(config);
       
        await File.WriteAllTextAsync(ConfigPath, yaml);

        await persistentStorage.SaveConfig(ConfigName, yaml);
    }
}
