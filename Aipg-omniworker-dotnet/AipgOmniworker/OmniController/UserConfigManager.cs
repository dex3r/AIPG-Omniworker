namespace AipgOmniworker.OmniController;

public class UserConfigManager(PersistentStorage persistentStorage, ILogger<UserConfigManager> logger)
    : YamlConfigManager<UserConfig>(persistentStorage, logger)
{
    public override string ConfigName => "userConfig.yaml";
    public override string ConfigPath => "/tmp/userConfig.yaml";
    public override string? ConfigTemplatePath => null;
    
    public override async Task OnConfigLoaded(UserConfig config)
    {
        if (Environment.GetEnvironmentVariable("GRID_API_KEY") != null)
        {
            config.ApiKey = Environment.GetEnvironmentVariable("GRID_API_KEY");
        }
            
        if (Environment.GetEnvironmentVariable("WORKER_NAME") != null)
        {
            config.WorkerName = Environment.GetEnvironmentVariable("WORKER_NAME");
        }

        if (Environment.GetEnvironmentVariable("WORKER_TYPE") != null)
        {
            if (Enum.TryParse(Environment.GetEnvironmentVariable("WORKER_TYPE"), out WorkerType configWorkerType))
            {
                config.WorkerType = configWorkerType;
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
                config.AutoStartWorker = autostartWorker;
            }
            else
            {
                throw new Exception("Failed to parse AUTOSTART_WORKER environment variable");
            }
        }

        await SaveConfig(config);
    }
}
