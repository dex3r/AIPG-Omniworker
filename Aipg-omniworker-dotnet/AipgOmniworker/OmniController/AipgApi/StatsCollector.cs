using Newtonsoft.Json;

namespace AipgOmniworker.OmniController;

public class StatsCollector(
    InstancesConfigManager instancesConfigManager,
    InstancesManager instancesManager,
    UserConfigManager userConfigManager,
    ILogger<StatsCollector> logger,
    BasicConfigManager basicConfigManager)
{
    private ApiWorkerDetails[]? _cachedWorkersDetails;
    private DateTime? _lastFetchTime;

    private readonly TimeSpan _cacheDuration = TimeSpan.FromSeconds(5);
    private readonly SemaphoreSlim _fetchLock = new(1);

    public async Task<WorkerStats[]> CollectStats(CancellationToken cancellationToken = default)
    {
        InstanceConfig[] configs = await instancesConfigManager.GetAllInstances();
        cancellationToken.ThrowIfCancellationRequested();

        List<WorkerStats> stats = new List<WorkerStats>();

        ApiWorkerDetails[]? apiWorkersDetails = await FetchWorkersDetails(cancellationToken);

        foreach (InstanceConfig config in configs)
        {
            Instance instance = await instancesManager.GetInstance(config.InstanceId);
            WorkerStats stat = await CollectWorkerStats(instance, apiWorkersDetails, cancellationToken);
            stats.Add(stat);
        }

        return stats.ToArray();
    }

    private async Task<WorkerStats> CollectWorkerStats(Instance instance, ApiWorkerDetails[]? apiWorkersDetails,
        CancellationToken cancellationToken)
    {
        ApiWorkerDetails? workerDetails = null;
        UserConfig userConfig = await userConfigManager.LoadConfig();
        cancellationToken.ThrowIfCancellationRequested();

        if (apiWorkersDetails != null)
        {
            //TODO: This should fetch worker details for this specific worker based on id. However, there is no way to get ID for now, only the name
            workerDetails = apiWorkersDetails
                .FirstOrDefault(w => w.name == instance.GetUniqueInstanceName(userConfig));
        }

        if (workerDetails == null)
        {
            //logger.LogError("Failed to find worker details for instance {InstanceName}", instance.InstanceId);
            return new WorkerStats
            {
                Instance = instance,
                VisibleOnApi = false
            };
        }

        return new WorkerStats
        {
            Instance = instance,
            VisibleOnApi = true,
            RequestsFulfilled = workerDetails.requests_fulfilled,
            KudosReceived = (int) (workerDetails.kudos_rewards ?? 0),
            WorkerId = workerDetails.id,
        };
    }

    private async Task<ApiWorkerDetails[]?> FetchWorkersDetails(CancellationToken cancellationToken)
    {
        try
        {
            await _fetchLock.WaitAsync(cancellationToken);

            if (_cachedWorkersDetails != null
                && _lastFetchTime.HasValue
                && DateTime.Now - _lastFetchTime.Value < _cacheDuration)
            {
                return _cachedWorkersDetails;
            }

            _cachedWorkersDetails = await ActualFetchWorkersDetails(cancellationToken);
            _lastFetchTime = DateTime.Now;

            return _cachedWorkersDetails;
        }
        finally
        {
            _fetchLock.Release();
        }
    }

    private async Task<ApiWorkerDetails[]?> ActualFetchWorkersDetails(CancellationToken cancellationToken)
    {
        try
        {
            BasicConfig basicConfig = await basicConfigManager.LoadConfig();
            cancellationToken.ThrowIfCancellationRequested();

            string url = basicConfig.GetApiV2Url("workers");

            HttpClient client = new HttpClient();
            HttpResponseMessage response = await client.GetAsync(url, cancellationToken);
            string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            ApiWorkerDetails[] apiWorkersDetails = JsonConvert.DeserializeObject<ApiWorkerDetails[]>(responseBody);

            return apiWorkersDetails;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to fetch workers details from API");
            return null;
        }
    }

    public async Task ClearCache()
    {
        try
        {
            await _fetchLock.WaitAsync();

            _cachedWorkersDetails = null;
            _lastFetchTime = null;
        }
        finally
        {
            _fetchLock.Release();
        }
    }
}
