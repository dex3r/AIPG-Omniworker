namespace AipgOmniworker.OmniController;

/*public class WorkerSlot
{
    public WorkerStatus Status { get; private set; }
    
    public List<string> Output { get; } = new();
    
    public event EventHandler? StateChangedEvent;
    
    private readonly GridWorkerController _gridWorkerController;
    private readonly AphroditeController _aphroditeController;
    private readonly ImageWorkerController _imageWorkerController;
    private readonly ILogger<OmniControllerMain> _logger;
    private readonly UserConfigManager _userConfigManager;
    private readonly TextWorkerConfigManager _textWorkerConfigManager;
    private readonly ImageWorkerConfigManager _imageWorkerConfigManager;
    private readonly BridgeConfigManager _bridgeConfigManager;
    
    private CancellationTokenSource? _startCancellation;
    private CancellationToken? _appClosingToken;

    public WorkerSlot(GridWorkerController gridWorkerController, AphroditeController aphroditeController,
        ImageWorkerController imageWorkerController, ILogger<OmniControllerMain> logger, UserConfigManager userConfigManager,
        TextWorkerConfigManager textWorkerConfigManager, ImageWorkerConfigManager imageWorkerConfigManager,
        BridgeConfigManager bridgeConfigManager)
    {
        _gridWorkerController = gridWorkerController;
        _aphroditeController = aphroditeController;
        _imageWorkerController = imageWorkerController;
        _logger = logger;
        _userConfigManager = userConfigManager;
        _textWorkerConfigManager = textWorkerConfigManager;
        _imageWorkerConfigManager = imageWorkerConfigManager;
        _bridgeConfigManager = bridgeConfigManager;
        _gridWorkerController = gridWorkerController;
        _aphroditeController = aphroditeController;

        _gridWorkerController.OnGridTextWorkerOutputChangedEvent += OnGridTextWorkerOutputChanged;
        _aphroditeController.OnAphroditeOutputChangedEvent += OnAphroditeOutputChanged;
        _imageWorkerController.OnOutputChangedEvent += OnImageWorkerOutputChanged;
    }
}*/
