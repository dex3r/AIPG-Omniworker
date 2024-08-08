namespace AipgOmniworker.OmniController;

public class InstancesConfigManager(PersistentStorage persistentStorage)
{
    private readonly string _instanceConfigPrefix = "instanceConfig_";
    
    public async Task<InstanceConfig[]> GetAllInstances()
    {
        List<string> configs = (await persistentStorage.GetAllFiles())
            .Where(x => x.StartsWith(_instanceConfigPrefix))
            .ToList();

        if (!configs.Contains("0"))
        {
            await CreateDefaultInstanceConfig();
            configs.Add("0");
        }
        
        InstanceConfig?[] result = await Task.WhenAll(configs.Select(LoadInstanceConfigFromFile));
        return result.Where(x => x != null).ToArray()!;
    }

    private async Task<InstanceConfig?> LoadInstanceConfigFromFile(string fileName)
    {
        if(!fileName.StartsWith(_instanceConfigPrefix))
        {
            return null;
        }
        
        string idPart = fileName.Replace(_instanceConfigPrefix, "").Replace(".yaml", "");
        if(!int.TryParse(idPart, out int instanceId))
        {
            return null;
        }

        string fullPath = persistentStorage.GetConfigPath(fileName);
        
        InstanceConfig instanceConfig = YamlConfigManager.DeserializeConfig<InstanceConfig>(fullPath);
        
        instanceConfig.InstanceId = instanceId;
        
        return instanceConfig;
    }

    private async Task CreateDefaultInstanceConfig()
    {
        InstanceConfig defaultConfig = await CreateNewInstance("Default Worker", 0);
        await SaveInstanceConfig(defaultConfig);
    }

    public async Task SaveInstanceConfig(InstanceConfig config)
    {
        await persistentStorage.SaveConfig($"{_instanceConfigPrefix}{config.InstanceId}.yaml",
            YamlConfigManager.SerializeConfig(config));
    }

    public async Task<InstanceConfig> CreateNewInstance(string name, int? forceInstanceId = null)
    {
        InstanceConfig instanceConfig = new InstanceConfig
        {
            InstanceName = name,
            InstanceId = forceInstanceId ?? await GetNextInstanceId()
        };
        
        await SaveInstanceConfig(instanceConfig);

        return instanceConfig;
    }

    private async Task<int> GetNextInstanceId()
    {
        List<int> instanceIds = (await GetAllInstances()).Select(x => x.InstanceId).ToList();
        return instanceIds.Count == 0 ? 0 : instanceIds.Max() + 1;
    }

    public async Task DeleteInstance(InstanceConfig instance)
    {
        string fileName = $"{_instanceConfigPrefix}{instance.InstanceId}.yaml";
        
        await persistentStorage.DeleteConfig(fileName);
    }
}
