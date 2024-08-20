using AipgOmniworker.OmniController;
using Microsoft.AspNetCore.Components;

namespace AipgOmniworker.Components.Pages;

public partial class Home
{
    private string? GridApiKey { get; set; }
    private string? WorkerName { get; set; }
    private string? HuggingFaceToken { get; set; }
    private string? WalletAddress { get; set; }
    private bool AutoUpdateImageWorkers { get; set; }
    
    private string? TextModelName { get; set; }
    private List<string> AdditionalImageModelNames { get; set; } = new();
    private string NewImageModel { get; set; } = string.Empty;
    private WorkerType WorkerType { get; set; } = OmniController.WorkerType.Auto;
    private DeviceType DeviceType { get; set; }
    private string DevicesIds { get; set; }
    
    private OmniControllerMain? OmniControllerMain => _selectedInstance?.OmniControllerMain;

    public int SelectedInstanceId
    {
        get => _selectedInstanceId;
        set
        {
            if (_selectedInstanceId != value)
            {
                _selectedInstanceId = value;
                OnSelectedInstanceChangedNonAsync();
            }
        }
    }

    public InstanceConfig[]? InstanceConfigs { get; protected set; }
    
    private int _selectedInstanceId = -1;
    private Instance? _selectedInstance;
    private SemaphoreSlim _changeInstanceSemaphore = new(1, 1);

    protected override async Task OnInitializedAsync()
    {
        InstanceConfigs = await InstancesConfigManager.GetAllInstances();
        
        SelectedInstanceId = 0;
        await OnSelectedInstanceChanged();

        try
        {
            var userConfig = await UserConfigManager.LoadConfig();
            GridApiKey = userConfig.ApiKey;
            WorkerName = userConfig.WorkerName;
            HuggingFaceToken = userConfig.HuggingFaceToken;
            AutoUpdateImageWorkers = userConfig.AutoUpdateImageWorker;
        }
        catch (Exception e)
        {
            _selectedInstance?.OmniControllerMain.Output.Add(e.ToString());
            Logger.LogError(e, "Failed to load user config");
        }
    }

    private void OnOmniControllerStateChanged(object? sender, EventArgs e)
    {
        InvokeAsync(StateHasChanged);
    }
    
    private async void OnSelectedInstanceChangedNonAsync()
    {
        try
        {
            await OnSelectedInstanceChanged();
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Failed to change selected instance");
        }
    }

    private async Task OnSelectedInstanceChanged()
    {
        try
        {
            await _changeInstanceSemaphore.WaitAsync();
            
            if (_selectedInstance != null)
            {
                _selectedInstance.OmniControllerMain.StateChangedEvent -= OnOmniControllerStateChanged;
                await SaveWorkerConfig();
            }

            _selectedInstance = await InstancesManager.GetInstance(SelectedInstanceId);
            _selectedInstance.OmniControllerMain.StateChangedEvent += OnOmniControllerStateChanged;
        
            WorkerType = _selectedInstance.Config.WorkerType;
            TextModelName = _selectedInstance.Config.TextWorkerModelName;
            AdditionalImageModelNames = _selectedInstance.Config.ImageWorkerModelsNames?.ToList() ?? new();
            DeviceType = _selectedInstance.Config.DeviceType;
            DevicesIds = _selectedInstance.Config.Devices;
            
            StateHasChanged();
        }
        finally
        {
            _changeInstanceSemaphore.Release();
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        await SaveUserConfig();
        await SaveWorkerConfig();
    }

    private async Task SaveWorkerConfig(bool? setAutostart = null)
    {
        if (_selectedInstance == null)
        {
            return;
        }
        
        try
        {
            _selectedInstance.Config.WorkerType = WorkerType;
            _selectedInstance.Config.TextWorkerModelName = TextModelName;
            _selectedInstance.Config.ImageWorkerModelsNames = AdditionalImageModelNames.ToArray();
            _selectedInstance.Config.DeviceType = DeviceType;

            if (DevicesIdsParser.TryParse(DevicesIds, out _))
            {
                _selectedInstance.Config.Devices = DevicesIds;
            }
            else
            {
                _selectedInstance.OmniControllerMain.Output.Add("Failed to parse Devices IDs");
            }

            if (setAutostart.HasValue)
            {
                _selectedInstance.Config.AutoStartWorker = setAutostart.Value;
            }

            await _selectedInstance.SaveConfig();
        }
        catch (Exception e)
        {
            _selectedInstance?.OmniControllerMain.Output.Add("Failed to save worker config");
            _selectedInstance?.OmniControllerMain.Output.Add(e.ToString());
            Logger.LogError(e, "Failed to save user config");
        }
    }

    private async Task SaveUserConfig()
    {
        try
        {
            UserConfig userConfig = await UserConfigManager.LoadConfig();
            userConfig.ApiKey = GridApiKey;
            userConfig.WorkerName = WorkerName;
            userConfig.HuggingFaceToken = HuggingFaceToken;
            userConfig.AutoUpdateImageWorker = AutoUpdateImageWorkers;

            await UserConfigManager.SaveConfig(userConfig);
        }
        catch (Exception e)
        {
            _selectedInstance?.OmniControllerMain.Output.Add("Failed to save user config");
            _selectedInstance?.OmniControllerMain.Output.Add(e.ToString());
            Logger.LogError(e, "Failed to save user config");
        }
    }

    private async Task StartWorkers()
    {
        if (_selectedInstance == null)
        {
            throw new InvalidOperationException("Selected instance is null");
        }
        
        if ((_selectedInstance.Config.WorkerType == WorkerType.Auto || _selectedInstance.Config.WorkerType == WorkerType.Text)
            && string.IsNullOrWhiteSpace(TextModelName))
        {
            _selectedInstance.OmniControllerMain.Output.Add("Text Model Name is required!");
            return;
        }
        
        if (_selectedInstance.Config.WorkerType == WorkerType.Auto || _selectedInstance.Config.WorkerType == WorkerType.Image)
        {
            if (AdditionalImageModelNames == null || AdditionalImageModelNames.Count == 0)
            {
                _selectedInstance.OmniControllerMain.Output.Add("At least one Image Model Name is required!");
                return;
            }

            if (AdditionalImageModelNames.Any(string.IsNullOrWhiteSpace))
            {
                _selectedInstance.OmniControllerMain.Output.Add("Some Image Model Names entries are empty!");
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(GridApiKey))
        {
            _selectedInstance.OmniControllerMain.Output.Add("Grid API Key is required!");
            return;
        }

        if (string.IsNullOrWhiteSpace(WorkerName))
        {
            _selectedInstance.OmniControllerMain.Output.Add("Worker Name is required!");
            return;
        }

        if (DeviceType == DeviceType.CPU && WorkerType != WorkerType.Image)
        {
            _selectedInstance.OmniControllerMain.Output.Add("CPU is only supported for Image Worker.");
            return;
        }

        if (string.IsNullOrWhiteSpace(DevicesIds))
        {
            _selectedInstance.OmniControllerMain.Output.Add("Devices IDs are required!");
            return;
        }
        
        if(!DevicesIdsParser.TryParse(DevicesIds, out _))
        {
            _selectedInstance.OmniControllerMain.Output.Add(
                """
                Invalid Devices IDs! Valid examples: "0", "0,1", "1,2,3", etc.
                """);
            return;
        }

        await SaveUserConfig();
        await SaveWorkerConfig(true);
        await _selectedInstance.OmniControllerMain.ApplyUserConfigsToWorkers();
        await _selectedInstance.OmniControllerMain.SaveAndRestart();
    }

    private async Task StopWorkers()
    {
        if (_selectedInstance == null)
        {
            throw new InvalidOperationException("Selected instance is null");
        }
        
        await SaveWorkerConfig(false);
        await _selectedInstance.OmniControllerMain.StopWorkers();
    }
    
    private void AddNewImageModel()
    {
        if (!string.IsNullOrEmpty(NewImageModel))
        {
            AdditionalImageModelNames.Add(NewImageModel);
            NewImageModel = string.Empty;
        }
    }
    
    private void RemoveImageModel(int index)
    {
        if (index >= 0 && index < AdditionalImageModelNames.Count)
        {
            AdditionalImageModelNames.RemoveAt(index);
        }
    }
}
