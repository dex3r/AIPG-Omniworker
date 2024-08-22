using AipgOmniworker.OmniController;

namespace AipgOmniworker.Components.Pages;

public partial class Stats : IDisposable
{
    public WorkerStats[]? Instances { get; set; }
    public UserConfig UserConfig { get; set; }
    private bool _isDisposed;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        ShowStatsPeriodically();
    }

    private async void ShowStatsPeriodically()
    {
        while(!_isDisposed)
        {
            try
            {
                await ShowStats();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            await Task.Delay(3000).ConfigureAwait(false);
        }
    }

    private async Task ShowStats()
    {
        UserConfig = await UserConfigManager.LoadConfig();
        Instances = await statsCollector.CollectStats();

        if (_isDisposed)
        {
            return;
        }
        
        await InvokeAsync(async () => StateHasChanged());
    }

    public void Dispose()
    {
        _isDisposed = true;
    }
    
    private string GetStatusClass(WorkerStatus status)
    {
        return status switch
        {
            WorkerStatus.Running => "status-green",
            WorkerStatus.Stopped or WorkerStatus.Stopping => "status-red",
            WorkerStatus.Starting => "status-yellow",
            _ => "status-default"
        };
    }
    
    private string GetStatusIcon(WorkerStatus status)
    {
        return status switch
        {
            WorkerStatus.Running => "✔️",
            WorkerStatus.Stopped or WorkerStatus.Stopping => "❌",
            WorkerStatus.Starting => "⏳",
            _ => "❓"
        };
    }
}
