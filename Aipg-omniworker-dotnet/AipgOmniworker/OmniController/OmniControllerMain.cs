using System.Diagnostics;
using Newtonsoft.Json;

namespace AipgOmniworker.OmniController;

public class OmniControllerMain
{
    public List<string> Output { get; } = new();

    public bool Status { get; private set; }

    public event EventHandler? StateChangedEvent;

    private CancellationTokenSource? _startCancellation;
    private readonly GridWorkerController _gridWorkerController;
    private readonly AphroditeController _aphroditeController;
    private readonly ImageWorkerController _imageWorkerController;
    private readonly ILogger<OmniControllerMain> _logger;
    private readonly UserConfigManager _userConfigManager;

    public OmniControllerMain(GridWorkerController gridWorkerController, AphroditeController aphroditeController,
        ImageWorkerController imageWorkerController, ILogger<OmniControllerMain> logger, UserConfigManager userConfigManager)
    {
        _gridWorkerController = gridWorkerController;
        _aphroditeController = aphroditeController;
        _imageWorkerController = imageWorkerController;
        _logger = logger;
        _userConfigManager = userConfigManager;
        _gridWorkerController = gridWorkerController;
        _aphroditeController = aphroditeController;

        _gridWorkerController.OnGridTextWorkerOutputChangedEvent += OnGridTextWorkerOutputChanged;
        _aphroditeController.OnAphroditeOutputChangedEvent += OnAphroditeOutputChanged;
        _imageWorkerController.OnOutputChangedEvent += OnImageWorkerOutputChanged;
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

    public async Task SaveAndRestart()
    {
        try
        {
            await StartGridWorkerAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to save and restart");
            AddOutput(e.ToString());
            Status = false;
        }
    }

    private async Task StartGridWorkerAsync()
    {
        Status = false;
        AddOutput("Stopping worker...");
        _startCancellation?.Cancel();

        await _gridWorkerController.KillWorkers();
        await _aphroditeController.KillWorkers();
        await _imageWorkerController.KillWorkers();

        _gridWorkerController.ClearOutput();
        _aphroditeController.ClearOutput();
        _imageWorkerController.ClearOutput();
        Output.Clear();

        AddOutput("Starting worker...");

        _startCancellation = new CancellationTokenSource();

        UserConfig userConfig = await _userConfigManager.LoadConfig();

        await StartWorkerBasedOnType(userConfig.WorkerType);
    }

    private async Task StartWorkerBasedOnType(WorkerType workerType)
    {
        switch (workerType)
        {
            case WorkerType.Auto:
                await StartWorkerAutoSelect();
                break;
            case WorkerType.Text:
                await StartTextWorker();
                break;
            case WorkerType.Image:
                await StartImageWorker();
                break;
            default:
                throw new Exception($"Unknown worker type: {workerType}");
        }
    }

    private async Task StartWorkerAutoSelect()
    {
        WorkerType workerType = await FetchAutoPreferredWorkerType();

        if (workerType == WorkerType.Auto)
        {
            throw new Exception("Auto worker type value cannot be Auto itself");
        }
        
        await StartWorkerBasedOnType(workerType);
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

    private async Task StartImageWorker()
    {
        AddOutput("Starting image worker...");
        await _imageWorkerController.StartImageWorker();
        
        AddOutput("Image worker started");
        Status = true;
    }

    private async Task StartTextWorker()
    {
        AddOutput("Starting Aphrodite and downloading model... (this may take a few minutes)");
        await _aphroditeController.StarAphrodite();

        bool started = await _aphroditeController.WaitForAphriditeToStart(_startCancellation.Token);

        if (!started)
        {
            AddOutput("Aphrodite failed to start !!!");
            return;
        }
        
        AddOutput("Aphrodite started!");

        AddOutput("Starting Grid Text Worker...");
        await _gridWorkerController.StartGridTextWorker();
        AddOutput("Grid Text Worker started!");
        Status = true;
    }
    
    

    private void AddOutput(string output)
    {
        _logger.LogInformation(output);
        Output.Add(output);
        StateChangedEvent?.Invoke(this, EventArgs.Empty);
    }
}
