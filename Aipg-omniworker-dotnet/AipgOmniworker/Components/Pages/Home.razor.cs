using AipgOmniworker.OmniController;
using Microsoft.AspNetCore.Components;

namespace AipgOmniworker.Components.Pages;

public partial class Home
{
    private string? GridApiKey { get; set; }
    private string? WorkerName { get; set; }
    private string? ModelName { get; set; }
    private string? HuggingFaceToken { get; set; }
    private string? WalletAddress { get; set; }
    private WorkerType WorkerType { get; set; } = OmniController.WorkerType.Auto;
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
            ModelName = userConfig.TextModelName;
            HuggingFaceToken = userConfig.HuggingFaceToken;
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
            userConfig.TextModelName = ModelName;
            userConfig.HuggingFaceToken = HuggingFaceToken; ;

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
        
        if (string.IsNullOrWhiteSpace(ModelName))
        {
            _selectedInstance.OmniControllerMain.Output.Add("Model Name is required!");
            return;
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
        
        await _selectedInstance.OmniControllerMain.StopWorkers();
    }
}
