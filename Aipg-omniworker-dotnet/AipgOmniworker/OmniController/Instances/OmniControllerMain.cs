using Newtonsoft.Json;

namespace AipgOmniworker.OmniController;

public class OmniControllerMain
{
    public List<string> Output { get; } = new();

    public WorkerStatus Status { get; private set; }

    public event EventHandler? StateChangedEvent;

    private CancellationTokenSource? _startCancellation;
    private readonly Instance _instance;
    private readonly GridWorkerController _gridWorkerController;
    private readonly AphroditeController _aphroditeController;
    private readonly ImageWorkerController _imageWorkerController;
    private readonly ILogger<OmniControllerMain> _logger;
    private readonly UserConfigManager _userConfigManager;
    private readonly TextWorkerConfigManager _textWorkerConfigManager;
    private readonly ImageWorkerConfigManager _imageWorkerConfigManager;
    private readonly BridgeConfigManager _bridgeConfigManager;
    private readonly StatsCollector _statsCollector;
    private readonly CudaTester _cudaTester;
    private readonly BasicConfigManager _basicConfigManager;
    private CancellationToken? _appClosingToken;

    private readonly static SemaphoreSlim _workerStartingSemaphore = new(1, 1);
    private readonly static SemaphoreSlim _applyUserConfigsToWorkersLock = new(1, 1);

    public OmniControllerMain(Instance instance, GridWorkerController gridWorkerController, AphroditeController aphroditeController,
        ImageWorkerController imageWorkerController, ILogger<OmniControllerMain> logger, UserConfigManager userConfigManager,
        TextWorkerConfigManager textWorkerConfigManager, ImageWorkerConfigManager imageWorkerConfigManager,
        BridgeConfigManager bridgeConfigManager, StatsCollector statsCollector, CudaTester cudaTester,
        BasicConfigManager basicConfigManager)
    {
        _instance = instance;
        _gridWorkerController = gridWorkerController;
        _aphroditeController = aphroditeController;
        _imageWorkerController = imageWorkerController;
        _logger = logger;
        _userConfigManager = userConfigManager;
        _textWorkerConfigManager = textWorkerConfigManager;
        _imageWorkerConfigManager = imageWorkerConfigManager;
        _bridgeConfigManager = bridgeConfigManager;
        _statsCollector = statsCollector;
        _cudaTester = cudaTester;
        _basicConfigManager = basicConfigManager;
        _gridWorkerController = gridWorkerController;
        _aphroditeController = aphroditeController;

        _gridWorkerController.OnGridTextWorkerOutputChangedEvent += OnGridTextWorkerOutputChanged;
        _aphroditeController.OnAphroditeOutputChangedEvent += OnAphroditeOutputChanged;
        _imageWorkerController.OnOutputChangedEvent += OnImageWorkerOutputChanged;
    }

    public async Task OnAppStarted(CancellationToken appClosing)
    {
        _appClosingToken = appClosing;
        appClosing.Register(() =>
        {
            Task stopWorkersTask = Task.Run(StopWorkers);
            stopWorkersTask.Wait(TimeSpan.FromSeconds(5));
        });

        if (_instance.Config.AutoStartWorker)
        {
            _logger.LogInformation("Auto starting worker...");
            await ApplyUserConfigsToWorkers();
            await SaveAndRestart(); // Call SaveAndRestart rather than StartGridWorkerAsync to ensure catching exceptions
        }
        else
        {
            _logger.LogInformation("Auto start worker is disabled");
        }
    }

    private void OnImageWorkerOutputChanged(object? sender, string e)
    {
        StateChangedEvent?.Invoke(this, EventArgs.Empty);
    }

    private void OnAphroditeOutputChanged(object? sender, string e)
    {
        StateChangedEvent?.Invoke(this, EventArgs.Empty);
    }

    private void OnGridTextWorkerOutputChanged(object? sender, string output)
    {
        StateChangedEvent?.Invoke(this, EventArgs.Empty);
    }

    public async Task ApplyUserConfigsToWorkers()
    {
        try
        {
            await _applyUserConfigsToWorkersLock.WaitAsync();

            UserConfig userConfig = await _userConfigManager.LoadConfig();

            if (string.IsNullOrWhiteSpace(userConfig.ApiKey))
            {
                throw new Exception("API Key not provided");
            }

            if (string.IsNullOrWhiteSpace(userConfig.WorkerName))
            {
                throw new Exception("Worker name not provided");
            }

            BridgeConfig bridgeConfig = await _bridgeConfigManager.LoadConfig();
            bridgeConfig.api_key = userConfig.ApiKey;
            bridgeConfig.worker_name = userConfig.WorkerName;
            bridgeConfig.scribe_name = userConfig.WorkerName;
            await _bridgeConfigManager.SaveConfig(bridgeConfig);

            TextWorkerConfig textWorkerConfig = await _textWorkerConfigManager.LoadConfig();
            textWorkerConfig.model_name = _instance.Config.TextWorkerModelName;
            textWorkerConfig.hugging_face_token = userConfig.HuggingFaceToken;
            textWorkerConfig.gpus = _instance.Config.Devices.Trim();
            await _textWorkerConfigManager.SaveConfig(textWorkerConfig);

            ImageWorkerConfig imageWorkerConfig = await _imageWorkerConfigManager.LoadConfig();
            imageWorkerConfig.api_key = userConfig.ApiKey;
            imageWorkerConfig.scribe_name = userConfig.WorkerName;
            imageWorkerConfig.alchemist_name = userConfig.WorkerName;
            imageWorkerConfig.disable_terminal_ui = true;
            imageWorkerConfig.dreamer_name = userConfig.WorkerName;
            imageWorkerConfig.models_to_load = _instance.Config.ImageWorkerModelsNames ?? new string[0];
            await _imageWorkerConfigManager.SaveConfig(imageWorkerConfig);
        }
        finally
        {
            _applyUserConfigsToWorkersLock.Release();
        }
    }

    public async Task SaveAndRestart()
    {
        try
        {
            await StartGridWorkerAsync();
        }
        catch (OperationCanceledException)
        {
            AddOutput("Workers start operation was cancelled");
            await StopWorkers();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to save and restart");
            AddOutput(e.ToString());

            if (Status != WorkerStatus.Stopping && Status != WorkerStatus.Stopped)
            {
                await StopWorkers();
                Status = WorkerStatus.Stopped;
            }
        }
    }

    private async Task StartGridWorkerAsync()
    {
        if (Status != WorkerStatus.Stopping && Status != WorkerStatus.Stopped)
        {
            await StopWorkers();
        }

        _gridWorkerController.ClearOutput();
        _aphroditeController.ClearOutput();
        _imageWorkerController.ClearOutput();

        AddOutput($"---- {DateTime.Now.ToString()} ----");
        AddOutput("Starting worker...");
        Status = WorkerStatus.Starting;
        
        if (_startCancellation != null)
        {
            await _startCancellation.CancelAsync();
        }
        _startCancellation = new CancellationTokenSource();
        CancellationToken token = _startCancellation.Token;

        try
        {
            if (_workerStartingSemaphore.CurrentCount == 0)
            {
                AddOutput("Another worker is already starting. Waiting for it to finish starting before starting this worker.");
            }

            await _workerStartingSemaphore.WaitAsync(token);
            
            token.ThrowIfCancellationRequested();

            if (_instance.Config.DeviceType == DeviceType.GPU)
            {
                AddOutput("Testing CUDA availability...");
                if (!await TestCuda())
                {
                    return;
                }
            }

            token.ThrowIfCancellationRequested();

            AddOutput("Ensuring unique worker name...");
            await EnsureUniqueWorkerName(token);

            AddOutput("Connecting to the API...");
            // Get recommended worker type even if Auto is not selected to ensure API is reachable and working
            //WorkerType recommendedWorkerType = await FetchAutoPreferredWorkerType(token);
            WorkerType recommendedWorkerType = WorkerType.Text; //TODO: Uncomment above once new API is deployed

            AddOutput($"Starting worker based on type from config: {_instance.Config.WorkerType}");
            WorkerType workerType = await StartWorkerBasedOnType(_instance.Config.WorkerType, token, recommendedWorkerType);

            if (Status == WorkerStatus.Running || Status == WorkerStatus.Starting)
            {
                Task.Run(async () => await WatchdogMethod(workerType, token));
            }
        }
        finally
        {
            _workerStartingSemaphore.Release();
        }
    }

    private async Task<bool> TestCuda()
    {
        bool isCudaAvailable = await _cudaTester.IsCudaAvailable();

        if (!isCudaAvailable)
        {
            await StopWorkers();

            AddOutput("");
            AddOutput("--------------------");
            AddOutput("Cannot run worker on GPU: CUDA is not available.");
            AddOutput("Possible reasons:");
            AddOutput("1. You have just installed CUDA but have not restarted the system.");
            AddOutput("2. No NVIDIA GPU is present in the system.");
            AddOutput("3. NVIDIA driver is not installed.");
            AddOutput("4. CUDA is not installed.");
            AddOutput("");
            AddOutput("To install CUDA, use the following links:");
            AddOutput("For Windows host: https://developer.nvidia.com/cuda-12-6-0-download-archive?target_os=Windows&target_arch=x86_64");
            AddOutput("For Linux host: https://github.com/dex3r/AIPG-Omniworker/blob/main/Linux-Nvidia-Toolkit-Instructions.md");

            return false;
        }

        AddOutput("CUDA is available!");
        return true;
    }

    private async Task EnsureUniqueWorkerName(CancellationToken cancellationToken)
    {
        await _statsCollector.ClearCache();
        bool alreadyExist = await IsVisibleFromApi(cancellationToken: cancellationToken);

        UserConfig userConfig = await _userConfigManager.LoadConfig();
        cancellationToken.ThrowIfCancellationRequested();

        if (!alreadyExist)
        {
            AddOutput($"Worker name is unique: {_instance.GetUniqueInstanceName(userConfig)}");
            return;
        }

        string oldName = _instance.GetUniqueInstanceName(userConfig);
        _instance.TempWorkerNamePostfix = Guid.NewGuid().ToString().Substring(0, 8);

        AddOutput($"Default worker name '{oldName}' is already taken. Changing to: {_instance.GetUniqueInstanceName(userConfig)}");

        await _statsCollector.ClearCache();
        alreadyExist = await IsVisibleFromApi(cancellationToken: cancellationToken);

        if (alreadyExist)
        {
            throw new Exception("Failed to ensure unique worker name!");
        }
    }

    private async Task WatchdogMethod(WorkerType workerType, CancellationToken stoppingToken)
    {
        try
        {
            AddOutput("Watchdog method started.");

            await WatchdogMethodUnsafe(workerType, stoppingToken);
        }
        catch (Exception e) when (e is TaskCanceledException || e is OperationCanceledException)
        {
            AddOutput("Watchdog method was cancelled");
        }
        catch (Exception e)
        {
            AddOutput("Exception in Watchdog method:");
            AddOutput(e.ToString());
        }
        finally
        {
            AddOutput("Watchdog method stopped!!");
        }
    }

    private async Task WatchdogMethodUnsafe(WorkerType workerType, CancellationToken stoppingToken)
    {
        DateTime startTime = DateTime.Now;
        TimeSpan maxRunTime = TimeSpan.FromHours(1);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

            if (Status != WorkerStatus.Running)
            {
                continue;
            }

            if (DateTime.Now - startTime > maxRunTime)
            {
                AddOutput($"Worker has been running for {maxRunTime.ToString()}, restarting...");
                SaveAndRestart();
                return;
            }

            if (workerType == WorkerType.Text)
            {
                if (!await _gridWorkerController.IsRunning()
                    || !await _aphroditeController.IsRunning())
                {
                    AddOutput("Grid Text Worker is not running, restarting...");
                    RestartSilentWithDelay(stoppingToken);
                    return;
                }
            }
            else if (workerType == WorkerType.Image)
            {
                if (!await _imageWorkerController.IsRunning())
                {
                    AddOutput("Image Worker is not running, restarting...");
                    RestartSilentWithDelay(stoppingToken);
                    return;
                }
            }
            else
            {
                throw new Exception($"Unexpected worker type in Watchdog method: {workerType}");
            }

            if (!await IsVisibleFromApi(true))
            {
                AddOutput("Worker is not visible from API, probably died. Restarting...");
                RestartSilentWithDelay(stoppingToken);
                return;
            }
        }
    }

    private async Task<bool> IsVisibleFromApi(bool clearCacheIfNotVisible = false, CancellationToken cancellationToken = default)
    {
        WorkerStats[] workersStats = await _statsCollector.CollectStats(cancellationToken);
        WorkerStats? workerStats = workersStats.FirstOrDefault(w => w.Instance.InstanceId == _instance.InstanceId);
        bool visible = workerStats != null && workerStats.VisibleOnApi;

        if (!visible)
        {
            if (!clearCacheIfNotVisible)
            {
                return false;
            }

            await _statsCollector.ClearCache();
        }

        workersStats = await _statsCollector.CollectStats(cancellationToken);
        workerStats = workersStats.FirstOrDefault(w => w.Instance.InstanceId == _instance.InstanceId);
        visible = workerStats != null && workerStats.VisibleOnApi;

        return visible;
    }

    private async Task RestartSilentWithDelay(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(10));

        if (stoppingToken.IsCancellationRequested
            || _appClosingToken?.IsCancellationRequested == true)
        {
            return;
        }

        await SaveAndRestart();
    }

    public async Task StopWorkers()
    {
        if (Status == WorkerStatus.Starting)
        {
            AddOutput("Workers are starting, cancelling and force stopping");
        }

        Status = WorkerStatus.Stopping;
        AddOutput("Stopping workers...");

        if (_startCancellation != null)
        {
            await _startCancellation.CancelAsync();
        }

        try
        {
            await _gridWorkerController.KillWorkers();
        }
        catch (Exception e)
        {
            AddOutput("Exception while stopping Grid Text Worker");
            AddOutput(e.ToString());
        }

        try
        {
            await _aphroditeController.KillWorkers();
        }
        catch (Exception e)
        {
            AddOutput("Exception while stopping Aphrodite");
            AddOutput(e.ToString());
        }

        try
        {
            await _imageWorkerController.KillWorkers();
        }
        catch (Exception e)
        {
            AddOutput("Exception while stopping Image Worker");
            AddOutput(e.ToString());
        }

        AddOutput("Workers stopped!");
        Status = WorkerStatus.Stopped;
    }

    private async Task<WorkerType> StartWorkerBasedOnType(WorkerType workerType, CancellationToken cancellationToken,
        WorkerType recommendedWorkerType)
    {
        AddOutput($"Starting worker of type: {workerType}");

        if (workerType == WorkerType.Auto)
        {
            workerType = recommendedWorkerType;
            AddOutput($"Starting worker of type {workerType} based on recommended worker type.");
        }

        switch (workerType)
        {
            case WorkerType.Auto:
                throw new Exception("Auto worker type value cannot be Auto itself");
            case WorkerType.Text:
                await StartTextWorker(cancellationToken);
                return WorkerType.Text;
            case WorkerType.Image:
                await StartImageWorker(cancellationToken);
                return WorkerType.Image;
            default:
                throw new Exception($"Unknown worker type: {workerType}");
        }
    }

    private async Task<WorkerType> FetchAutoPreferredWorkerType(CancellationToken cancellationToken)
    {
        AddOutput("Fetching recommended auto worker type from API...");

        BasicConfig basicConfig = await _basicConfigManager.LoadConfig();
        string url = basicConfig.GetApiV2Url("auto_worker_type");

        UserConfig userConfig = await _userConfigManager.LoadConfig();

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("apikey", userConfig.ApiKey);

        HttpResponseMessage response = await client.GetAsync(url, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        if (!response.IsSuccessStatusCode)
        {
            try
            {
                string responseString = await response.Content.ReadAsStringAsync(cancellationToken);
                AddOutput($"Failed to connect to the API, HTTP Code {response.StatusCode}, body: {responseString}");
            }
            catch
            {
                AddOutput($"Failed to connect to the API, HTTP Code {response.StatusCode}");
            }

            throw new Exception("Failed to connect to the API.");
        }

        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        AutoWorkerTypeResponse? autoWorkerTypeResponse = JsonConvert.DeserializeObject<AutoWorkerTypeResponse>(json);

        if (autoWorkerTypeResponse == null)
        {
            AddOutput($"Auto worker info received from the API: {json}");
            _logger.LogInformation("Auto worker info: {Info}", json);

            throw new Exception("Failed to parse auto worker info received from the API");
        }

        string recommendedWorkerType = autoWorkerTypeResponse.recommended_worker_type;

        // Do not use Enum.Parse in case the WorkerType is ever refactored
        return recommendedWorkerType switch
        {
            "text" => WorkerType.Text,
            "image" => WorkerType.Image,
            _ => throw new Exception($"Unknown worker type received from the API: {recommendedWorkerType}")
        };
    }

    private async Task<WorkerInfo[]> GetWorkersInfo()
    {
        // Get array of WorkerInfo from https://api.aipowergrid.io/api/v2/workers

        BasicConfig basicConfig = await _basicConfigManager.LoadConfig();
        string url = basicConfig.GetApiV2Url("workers");

        using var client = new HttpClient();
        HttpResponseMessage response = await client.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("Failed to get workers info");
        }

        string json = await response.Content.ReadAsStringAsync();
        WorkerInfo[] workers = JsonConvert.DeserializeObject<WorkerInfo[]>(json);
        if (workers == null)
        {
            throw new Exception("Failed to parse workers info");
        }

        return workers;
    }

    private async Task StartImageWorker(CancellationToken cancellationToken)
    {
        AddOutput("Starting image worker...");
        Status = WorkerStatus.Starting;

        await _imageWorkerController.StartImageWorker(cancellationToken);

        AddOutput("Image worker process, downloading models... (it may take a few minutes)");
        AddOutput("Waiting for worker to appear on the API...");

        await WaitForWorkerToAppearOnApi(cancellationToken);

        AddOutput("Image worker started!");

        Status = WorkerStatus.Running;
    }

    private async Task StartTextWorker(CancellationToken cancellationToken)
    {
        AddOutput("Starting Aphrodite and downloading model... (this may take a few minutes)");
        await _aphroditeController.StarAphrodite(cancellationToken);

        bool started = await _aphroditeController.WaitForAphriditeToStart(cancellationToken);

        if (!started)
        {
            AddOutput("Aphrodite failed to start !!!");
            return;
        }

        AddOutput("Aphrodite started!");

        AddOutput("Starting Grid Text Worker...");
        Status = WorkerStatus.Starting;

        await _gridWorkerController.StartGridTextWorker(cancellationToken);

        AddOutput("Text Worker process started.");

        AddOutput("Waiting for worker to appear on the API...");
        await WaitForWorkerToAppearOnApi(cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        AddOutput("Text Worker started!");
        Status = WorkerStatus.Running;
    }

    private async Task WaitForWorkerToAppearOnApi(CancellationToken cancellationToken)
    {
        TimeSpan timeout = TimeSpan.FromHours(1);
        DateTime startTime = DateTime.Now;

        do
        {
            cancellationToken.ThrowIfCancellationRequested();

            bool visible = await IsVisibleFromApi();

            if (visible)
            {
                break;
            }

            if (DateTime.Now - startTime > timeout)
            {
                throw new Exception("Worker failed to start: failed to appear on the API in time.");
            }

            await Task.Delay(TimeSpan.FromSeconds(5));
        } while (true);
    }

    private void AddOutput(string output)
    {
        if (Output.Count > 10000)
        {
            Output.RemoveAt(0);
        }

        _logger.LogInformation(output);
        Output.Add(output);
        StateChangedEvent?.Invoke(this, EventArgs.Empty);
    }
}
