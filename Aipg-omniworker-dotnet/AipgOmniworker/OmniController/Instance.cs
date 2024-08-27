namespace AipgOmniworker.OmniController;

public class Instance
{
    public int InstanceId { get; private set; }
    public OmniControllerMain OmniControllerMain { get; private set; }
    public InstanceConfig Config { get; private set; }
    public AphroditeController AphroditeController { get; private set; }
    public GridWorkerController GridWorkerController { get; private set; }
    public ImageWorkerController ImageWorkerController { get; private set; }
    public string? TempWorkerNamePostfix { get; set; }

    private InstancesConfigManager _instancesConfigManager;
    
    public Instance(InstancesConfigManager instancesConfigManager)
    {
        _instancesConfigManager = instancesConfigManager;
    }
    
    public static async Task<Instance> CreateNew(int instanceId, IServiceProvider rootServices)
    {
        IServiceScope scope = rootServices.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        Instance instance = services.GetRequiredService<Instance>();
        instance.InstanceId = instanceId;
        
        instance.OmniControllerMain = services.GetRequiredService<OmniControllerMain>();
        instance.AphroditeController = services.GetRequiredService<AphroditeController>();
        instance.GridWorkerController = services.GetRequiredService<GridWorkerController>();
        instance.ImageWorkerController = services.GetRequiredService<ImageWorkerController>();

        instance.Config = await instance._instancesConfigManager.LoadInstanceConfig(instanceId);
        
        return instance;
    }

    public async Task SaveConfig()
    {
        await _instancesConfigManager.SaveInstanceConfig(Config);
    }

    public string GetUniqueInstanceName(UserConfig userConfig)
    {
        if (TempWorkerNamePostfix == null)
        {
            return $"{userConfig.WorkerName}#{InstanceId}";
        }
        else
        {
            return $"{userConfig.WorkerName}#{InstanceId}-{TempWorkerNamePostfix}";
        }
    }
}
