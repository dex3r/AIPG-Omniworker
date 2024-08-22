using Newtonsoft.Json;

namespace AipgOmniworker.OmniController;

public class StatsCollector(InstancesConfigManager instancesConfigManager, InstancesManager instancesManager,
    UserConfigManager userConfigManager, ILogger<StatsCollector> logger)
{
    public async Task<WorkerStats[]> CollectStats()
    {
        InstanceConfig[] configs = await instancesConfigManager.GetAllInstances();

        List<WorkerStats> stats = new List<WorkerStats>();

        ApiWorkerDetails[]? apiWorkersDetails = await FetchWorkersDetails();
        
        foreach (InstanceConfig config in configs)
        {
            Instance instance = await instancesManager.GetInstance(config.InstanceId);
            WorkerStats stat = await CollectWorkerStats(instance, apiWorkersDetails);
            stats.Add(stat);
        }

        return stats.ToArray();
    }

    private async Task<WorkerStats> CollectWorkerStats(Instance instance, ApiWorkerDetails[]? apiWorkersDetails)
    {
        ApiWorkerDetails? workerDetails = null;
        UserConfig userConfig = await userConfigManager.LoadConfig();

        if (apiWorkersDetails != null)
        {
            //TODO: This should fetch worker details for this specific worker based on id. However, there is no way to get ID for now, only the name
            workerDetails = apiWorkersDetails
                .FirstOrDefault(w => w.name == instance.GetUniqueInstanceName(userConfig));

            if (workerDetails == null)
            {
                workerDetails = apiWorkersDetails.FirstOrDefault(w => w.name == userConfig.WorkerName);
            }
        }

        if (workerDetails == null)
        {
            logger.LogError("Failed to find worker details for instance {InstanceName}", instance.InstanceId);
            return new WorkerStats
            {
                Instance = instance
            };
        }

        return new WorkerStats
        {
            Instance = instance,
            RequestsFulfilled = workerDetails.requests_fulfilled,
            KudosReceived = (int)(workerDetails.kudos_rewards ?? 0),
            WorkerId = workerDetails.id
        };
    }

    private async Task<ApiWorkerDetails[]?> FetchWorkersDetails()
    {
        try
        {
            string url = "https://api.aipowergrid.io/api/v2/workers";

            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + Environment.GetEnvironmentVariable("AIPG_API_KEY"));
            HttpResponseMessage response = await client.GetAsync(url);
            string responseBody = await response.Content.ReadAsStringAsync();
            ApiWorkerDetails[] apiWorkersDetails = JsonConvert.DeserializeObject<ApiWorkerDetails[]>(responseBody);

            return apiWorkersDetails;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to fetch workers details from API");
            return null;
        }
    }
}
