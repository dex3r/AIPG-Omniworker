﻿using Newtonsoft.Json;

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
    private CancellationToken? _appClosingToken;
    
    private readonly static SemaphoreSlim _workerStartingSemaphore = new(1, 1);

    public OmniControllerMain(Instance instance, GridWorkerController gridWorkerController, AphroditeController aphroditeController,
        ImageWorkerController imageWorkerController, ILogger<OmniControllerMain> logger, UserConfigManager userConfigManager,
        TextWorkerConfigManager textWorkerConfigManager, ImageWorkerConfigManager imageWorkerConfigManager,
        BridgeConfigManager bridgeConfigManager, StatsCollector statsCollector)
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

        try
        {
            await _workerStartingSemaphore.WaitAsync(appClosing);
            
            if (_instance.Config.AutoStartWorker)
            {
                _logger.LogInformation("Auto starting worker...");
                await ApplyUserConfigsToWorkers();
                await StartGridWorkerAsync();
            }
            else
            {
                _logger.LogInformation("Auto start worker is disabled");
            }
        }
        finally
        {
            _workerStartingSemaphore.Release();
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
    
    public async Task SaveAndRestart()
    {
        try
        {
            await _workerStartingSemaphore.WaitAsync();
            
            await StartGridWorkerAsync();
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
        finally
        {
            _workerStartingSemaphore.Release();
        }
    }

    private async Task StartGridWorkerAsync()
    {
        if(Status != WorkerStatus.Stopping && Status != WorkerStatus.Stopped)
        {
            await StopWorkers();
        }

        _gridWorkerController.ClearOutput();
        _aphroditeController.ClearOutput();
        _imageWorkerController.ClearOutput();

        AddOutput($"---- {DateTime.Now.ToString()} ----");
        AddOutput("Starting worker...");
        Status = WorkerStatus.Starting;

        if(_startCancellation != null)
        {
            await _startCancellation.CancelAsync();
        }
        _startCancellation = new CancellationTokenSource();
        
        CancellationToken token = _startCancellation.Token;
        
        AddOutput("Ensuring unique worker name...");
        await EnsureUniqueWorkerName();
        
        AddOutput($"Starting worker based on type from config: {_instance.Config.WorkerType}");
        WorkerType workerType = await StartWorkerBasedOnType(_instance.Config.WorkerType, token);
        
        if(Status == WorkerStatus.Running || Status == WorkerStatus.Starting)
        {
            Task.Run(async () => await WatchdogMethod(workerType, token));
        }
    }

    private async Task EnsureUniqueWorkerName()
    {
        await _statsCollector.ClearCache();
        bool alreadyExist = await IsVisibleFromApi();

        UserConfig userConfig = await _userConfigManager.LoadConfig();
        
        if (!alreadyExist)
        {
            AddOutput($"Worker name is unique: {_instance.GetUniqueInstanceName(userConfig)}");
            return;
        }

        string oldName = _instance.GetUniqueInstanceName(userConfig);
        _instance.TempWorkerNamePostfix = Guid.NewGuid().ToString().Substring(0, 8);

        AddOutput($"Default worker name '{oldName}' is already taken. Changing to: {_instance.GetUniqueInstanceName(userConfig)}");
        
        await _statsCollector.ClearCache();
        alreadyExist = await IsVisibleFromApi();

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
            
            if(DateTime.Now - startTime > maxRunTime)
            {
                AddOutput($"Worker has been running for {maxRunTime.ToString()}, restarting...");
                SaveAndRestart();
                return;
            }

            if (workerType == WorkerType.Text)
            {
                if(!await _gridWorkerController.IsRunning()
                   || !await _aphroditeController.IsRunning())
                {
                    AddOutput("Grid Text Worker is not running, restarting...");
                    RestartSilentWithDelay(stoppingToken);
                    return;
                }
            }
            else if (workerType == WorkerType.Image)
            {
                if(!await _imageWorkerController.IsRunning())
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

    private async Task<bool> IsVisibleFromApi(bool clearCacheIfNotVisible = false)
    {
        WorkerStats[] workersStats = await _statsCollector.CollectStats();
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
        
        workersStats = await _statsCollector.CollectStats();
        workerStats = workersStats.FirstOrDefault(w => w.Instance.InstanceId == _instance.InstanceId);
        visible = workerStats != null && workerStats.VisibleOnApi;

        return visible;
    }

    private async Task RestartSilentWithDelay(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(10));
        
        if(stoppingToken.IsCancellationRequested 
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

    private async Task<WorkerType> StartWorkerBasedOnType(WorkerType workerType, CancellationToken cancellationToken)
    {
        AddOutput($"Starting worker of type: {workerType}");
        
        switch (workerType)
        {
            case WorkerType.Auto:
                return await StartWorkerAutoSelect(cancellationToken);
                break;
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

    private async Task<WorkerType> StartWorkerAutoSelect(CancellationToken cancellationToken)
    {
        WorkerType workerType = await FetchAutoPreferredWorkerType();

        if (workerType == WorkerType.Auto)
        {
            throw new Exception("Auto worker type value cannot be Auto itself");
        }
        
        return await StartWorkerBasedOnType(workerType, cancellationToken);
    }
    
    private async Task<WorkerType> FetchAutoPreferredWorkerType()
    {
        AddOutput("Fetching workers info to determine Auto worker type...");
        WorkerInfo[] workers = await GetWorkersInfo();
        
        int imageWorkerCount = workers.Count(w => w.type == "image");
        int textWorkerCount = workers.Count(w => w.type == "text");
        
        AddOutput($"Image worker count: {imageWorkerCount} Text worker count: {textWorkerCount}");
        
        if (imageWorkerCount == textWorkerCount)
        {
            AddOutput("Both worker types have equal count, selecting Text worker type");
            return WorkerType.Text;
        }

        if (imageWorkerCount > textWorkerCount)
        {
            AddOutput("There are currently more Image workers than Text workers, so starting Text worker");
            return WorkerType.Text;
        }
        else
        {
            AddOutput("There are currently more Text workers than Image workers, so starting Image worker");
            return WorkerType.Image;
        }
    }

    private async Task<WorkerInfo[]> GetWorkersInfo()
    {
        // Get array of WorkerInfo from https://api.aipowergrid.io/api/v2/workers
        
        using var client = new HttpClient();
        HttpResponseMessage response = await client.GetAsync("https://api.aipowergrid.io/api/v2/workers");

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
            
            if(DateTime.Now - startTime > timeout)
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