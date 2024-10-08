﻿namespace AipgOmniworker.OmniController;

public class InstancesConfigManager(PersistentStorage persistentStorage, ILogger<InstancesConfigManager> logger)
{
    private readonly string _instanceConfigPrefix = "instanceConfig_";
    
    private readonly SemaphoreSlim _getAllInstancesSemaphore = new(1, 1);
    private readonly SemaphoreSlim _saveInstanceConfigSemaphore = new(1, 1);
    
    public async Task<InstanceConfig[]> GetAllInstances()
    {
        try
        {
            await _getAllInstancesSemaphore.WaitAsync();
            
            List<string> configs = (await persistentStorage.GetAllFiles())
                .Where(x => x.StartsWith(_instanceConfigPrefix))
                .ToList();

            if (!configs.Contains($"{_instanceConfigPrefix}0.yaml"))
            {
                await CreateDefaultInstanceConfig();
                configs.Add("0");
            }
        
            InstanceConfig?[] result = await Task.WhenAll(configs.Select(LoadInstanceConfigFromFile));
            return result.Where(x => x != null).ToArray()!;
        }
        finally
        {
            _getAllInstancesSemaphore.Release();
        }
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

        InsertEnvironmentVariables(instanceConfig);
        
        return instanceConfig;
    }
    
    private void InsertEnvironmentVariables(InstanceConfig instanceConfig)
    {
        if (Environment.GetEnvironmentVariable("WORKER_TYPE") != null)
        {
            if (Enum.TryParse(Environment.GetEnvironmentVariable("WORKER_TYPE"), true, out WorkerType configWorkerType))
            {
                instanceConfig.WorkerType = configWorkerType;
            }
            else
            {
                throw new Exception("Failed to parse WORKER_TYPE environment variable");
            }
        }
        
        if (Environment.GetEnvironmentVariable("AUTOSTART_WORKER") != null)
        {
            if (bool.TryParse(Environment.GetEnvironmentVariable("AUTOSTART_WORKER"), out bool autostartWorker))
            {
                instanceConfig.AutoStartWorker = autostartWorker;
            }
            else
            {
                throw new Exception("Failed to parse AUTOSTART_WORKER environment variable");
            }
        }
    }
    
    public async Task<InstanceConfig> LoadInstanceConfig(int instanceId)
    {
        InstanceConfig? instanceConfig = await LoadInstanceConfigFromFile($"{_instanceConfigPrefix}{instanceId}.yaml");
        
        if(instanceConfig == null)
        {
            logger.LogInformation("Creating new instance config for instance {InstanceId}", instanceId);
            instanceConfig = await CreateNewInstance($"Worker {instanceId}", instanceId);
        }
        else
        {
            logger.LogInformation("Loaded existing instance config for instance {InstanceId}: with WorkerType: {WorkerType}", 
                instanceId, instanceConfig.WorkerType);
        }
        
        return instanceConfig;
    }

    private async Task CreateDefaultInstanceConfig()
    {
        logger.LogInformation("Creating default instance config");
        InstanceConfig defaultConfig = await CreateNewInstance("Default Worker", 0);
        await SaveInstanceConfig(defaultConfig);
    }

    public async Task SaveInstanceConfig(InstanceConfig config)
    {
        try
        {
            await _saveInstanceConfigSemaphore.WaitAsync();
            
            await persistentStorage.SaveConfig($"{_instanceConfigPrefix}{config.InstanceId}.yaml",
                YamlConfigManager.SerializeConfig(config));
        }
        finally
        {
            _saveInstanceConfigSemaphore.Release();
        }
    }

    public async Task<InstanceConfig> CreateNewInstance(string name, int? forceInstanceId = null)
    {
        InstanceConfig instanceConfig = new InstanceConfig
        {
            InstanceName = name,
            InstanceId = forceInstanceId ?? await GetNextInstanceId()
        };
        
        await SaveInstanceConfig(instanceConfig);
        
        InsertEnvironmentVariables(instanceConfig);

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
