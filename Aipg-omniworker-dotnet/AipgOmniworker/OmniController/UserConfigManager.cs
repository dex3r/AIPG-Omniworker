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

        await SaveConfig(config);
    }
}
