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

    public OmniControllerMain(GridWorkerController gridWorkerController, BridgeConfigManager bridgeConfigManager,
        TextWorkerConfigManager textWorkerConfigManager, AphroditeController aphroditeController)
    {
        _gridWorkerController = gridWorkerController;
        _bridgeConfigManager = bridgeConfigManager;
        _textWorkerConfigManager = textWorkerConfigManager;
        _aphroditeController = aphroditeController;
        _gridWorkerController = gridWorkerController;
        _aphroditeController = aphroditeController;

        _gridWorkerController.OnGridTextWorkerOutputChangedEvent += OnGridTextWorkerOutputChanged;
        _aphroditeController.OnAphroditeOutputChangedEvent += OnAphroditeOutputChanged;
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
            Status = false;
            await StartGridTextWorkerAsync();
            Status = true;
        }
        catch (Exception e)
        {
            AddOutput(e.ToString());
            Status = false;
        }
    }

    private async Task StartGridTextWorkerAsync()
    {
        AddOutput("Stopping worker...");
        _startCancellation?.Cancel();

        await _gridWorkerController.KillWorkers();
        await _aphroditeController.KillWorkers();

        _gridWorkerController.ClearOutput();
        _aphroditeController.ClearOutput();
        Output.Clear();

        AddOutput("Starting worker...");

        _startCancellation = new CancellationTokenSource();

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
    }
    
    private void AddOutput(string output)
    {
        Output.Add(output);
        StateChangedEvent?.Invoke(this, EventArgs.Empty);
    }
}
