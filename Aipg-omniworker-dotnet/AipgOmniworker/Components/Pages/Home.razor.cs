using AipgOmniworker.OmniController;

namespace AipgOmniworker.Components.Pages;

public partial class Home
{
    private string? GridApiKey { get; set; }
    private string? WorkerName { get; set; }
    private string? ModelName { get; set; }
    private string? HuggingFaceToken { get; set; }
    private string? WalletAddress { get; set; }
    private WorkerType WorkerType { get; set; } = OmniController.WorkerType.Auto;

    protected override async Task OnInitializedAsync()
    {
        OmniControllerMain.StateChangedEvent += (_, _) => InvokeAsync(StateHasChanged);

        try
        {
            var userConfig = await UserConfigManager.LoadConfig();
            GridApiKey = userConfig.ApiKey;
            WorkerName = userConfig.WorkerName;
            ModelName = userConfig.TextModelName;
            HuggingFaceToken = userConfig.HuggingFaceToken;
            WorkerType = userConfig.WorkerType;
        }
        catch (Exception e)
        {
            OmniControllerMain.Output.Add(e.ToString());
            logger.LogError(e, "Failed to load user config");
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        await SaveUserConfig();
    }

    private async Task SaveUserConfig(bool? setAutostart = null)
    {
        try
        {
            UserConfig userConfig = await UserConfigManager.LoadConfig();
            userConfig.ApiKey = GridApiKey;
            userConfig.WorkerName = WorkerName;
            userConfig.TextModelName = ModelName;
            userConfig.HuggingFaceToken = HuggingFaceToken;
            userConfig.WorkerType = WorkerType;

            if (setAutostart.HasValue)
            {
                userConfig.AutoStartWorker = setAutostart.Value;
            }

            await UserConfigManager.SaveConfig(userConfig);
        }
        catch (Exception e)
        {
            OmniControllerMain.Output.Add("Failed to save user config");
            OmniControllerMain.Output.Add(e.ToString());
            logger.LogError(e, "Failed to save user config");
        }
    }

    private async Task StartWorkers()
    {
        if (string.IsNullOrWhiteSpace(ModelName))
        {
            OmniControllerMain.Output.Add("Model Name is required!");
            return;
        }

        if (string.IsNullOrWhiteSpace(GridApiKey))
        {
            OmniControllerMain.Output.Add("Grid API Key is required!");
            return;
        }

        if (string.IsNullOrWhiteSpace(WorkerName))
        {
            OmniControllerMain.Output.Add("Worker Name is required!");
            return;
        }

        await SaveUserConfig(true);
        await OmniControllerMain.ApplyUserConfigsToWorkers();
        await OmniControllerMain.SaveAndRestart();
    }

    private async Task StopWorkers()
    {
        await OmniControllerMain.StopWorkers();
    }
}