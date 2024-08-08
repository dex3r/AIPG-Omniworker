namespace AipgOmniworker.OmniController;

public class InstancesManager(IServiceProvider rootServices)
{
    private Dictionary<int, Instance> _instances = new();

    private readonly SemaphoreSlim _lock = new(1, 1);
    
    public async Task<Instance> GetInstance(int instanceId)
    {
        try
        {
            _lock.Wait();
            
            if (_instances.TryGetValue(instanceId, out var instance1))
            {
                return instance1;
            }
        
            Instance instance = await Instance.CreateNew(instanceId, rootServices);
            _instances.Add(instanceId, instance);
            return instance;
        }
        finally
        {
            _lock.Release();
        }
    }
}
