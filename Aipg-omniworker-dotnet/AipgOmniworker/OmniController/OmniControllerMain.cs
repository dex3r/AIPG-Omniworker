namespace AipgOmniworker.OmniController;

public class OmniControllerMain
{
    public List<string> Output { get; } = new();

    public bool Status { get; private set; }

    public event EventHandler? StateChangedEvent;

    private CancellationTokenSource _startCancellation;
    private readonly GridWorkerController _gridWorkerController;
    private readonly BridgeConfigManager _bridgeConfigManager;
    private readonly TextWorkerConfigManager _textWorkerConfigManager;
    private readonly AphroditeController _aphroditeController;
    private readonly ImageWorkerController _imageWorkerController;

    public OmniControllerMain(GridWorkerController gridWorkerController, BridgeConfigManager bridgeConfigManager,
        TextWorkerConfigManager textWorkerConfigManager, AphroditeController aphroditeController,
        ImageWorkerController imageWorkerController)
    {
        _gridWorkerController = gridWorkerController;
        _bridgeConfigManager = bridgeConfigManager;
        _textWorkerConfigManager = textWorkerConfigManager;
        _aphroditeController = aphroditeController;
        _imageWorkerController = imageWorkerController;
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
        
        BridgeConfig bridgeConfig = await _bridgeConfigManager.LoadConfig();

        await StartWorkerBasedOnConfig(bridgeConfig);
    }

    private async Task StartWorkerBasedOnConfig(BridgeConfig bridgeConfig)
    {
        switch (bridgeConfig.worker_type)
        {
            case WorkerType.Auto:
                AddOutput("Worker not started: Auto worker type not supported yet! Please select text or image worker.");
                break;
            case WorkerType.Text:
                await StartTextWorker();
                break;
            case WorkerType.Image:
                await StartImageWorker();
                break;
            default:
                throw new Exception($"Unknown worker type: {bridgeConfig.worker_type}");
        }
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
        Output.Add(output);
        StateChangedEvent?.Invoke(this, EventArgs.Empty);
    }
}
