using YamlDotNet.Serialization;

namespace AipgOmniworker.OmniController;

public abstract class YamlConfigManager<T>(PersistentStorage persistentStorage, ILogger logger) : YamlConfigManager
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

        T config = DeserializeConfig<T>(ConfigPath);

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

        if (!File.Exists(ConfigTemplatePath))
        {
            throw new Exception($"Config template file not found under {ConfigTemplatePath}");
        }

        logger.LogInformation("Copying config template {ConfigTemplatePath} to {ConfigPath}",
            ConfigTemplatePath, ConfigPath);
        File.Copy(ConfigTemplatePath, ConfigPath);

        if (!File.Exists(ConfigPath))
        {
            throw new Exception($"Failed to copy config template file from {ConfigTemplatePath} to {ConfigPath}");
        }
    }

    public async Task SaveConfig(T config)
    {
        string yaml = SerializeConfig(config);

        await File.WriteAllTextAsync(ConfigPath, yaml);

        await persistentStorage.SaveConfig(ConfigName, yaml);
    }
}

public abstract class YamlConfigManager
{
    public static string SerializeConfig(object config)
    {
        ISerializer serializer = new SerializerBuilder().Build();
        return serializer.Serialize(config);
    }

    public static TConfig DeserializeConfig<TConfig>(string configPath)
    {
        try
        {
            TextReader input = new StreamReader(configPath);

            IDeserializer deserializer = new DeserializerBuilder()
                .IgnoreUnmatchedProperties()
                .Build();

            return deserializer.Deserialize<TConfig>(input);
        }
        catch (Exception e)
        {
            string content;

            try
            {
                content = File.ReadAllText(configPath);
            }
            catch (Exception exception)
            {
                content = $"Failed to read config content from file, exception: {exception}";
            }

            throw new ConfigDeserializationException(typeof(TConfig).Name, configPath, content, e);
        }
    }
}
