using System.Diagnostics;
using System.Text.RegularExpressions;

namespace AipgOmniworker.OmniController;

public class GridWorkerController(Instance instance, ILogger<GridWorkerController> logger, UserConfigManager userConfigManager)
{
    public List<string> GridTextWorkerOutput { get; private set; } = new();

    public event EventHandler<string> OnGridTextWorkerOutputChangedEvent;

    public string WorkingDirectory => "/worker";
    
    private Process? _gridTextWorkerProcess;

    public async Task StartGridTextWorker(CancellationToken cancellationToken)
    {
        try
        {
            await StartGridTextWorkerInternal();

            cancellationToken.Register(() =>
            {
                _gridTextWorkerProcess?.Kill(true);
                _gridTextWorkerProcess = null;
            });
            
            if(_gridTextWorkerProcess == null || _gridTextWorkerProcess.HasExited)
            {
                throw new Exception("Failed to start text worker");
            }
            
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (Exception e)
        {
            PrintGridTextWorkerOutput($"Failed to start grid text worker");
            PrintGridTextWorkerOutput(e.ToString());
        }
    }
    
    private async Task StartGridTextWorkerInternal()
    {
        PrintGridTextWorkerOutput("Starting grid text worker...");
        
        string devicesString = instance.Config.Devices.Trim();
        string instanceName = instance.GetUniqueInstanceName(await userConfigManager.LoadConfig());

        await Task.Delay(200);
        
        Process? process = Process.Start(new ProcessStartInfo
        {
            //FileName = "/usr/bin/python3",
            //Arguments = "-s bridge_scribe.py",
            FileName = "/worker/run-worker.sh",
            Arguments = $"-n {instanceName}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            //UseShellExecute = false,
            WorkingDirectory = WorkingDirectory,
            Environment =
            {
                { "DISABLE_TERMINAL_UI", "true" },
                { "CUDA_VISIBLE_DEVICES",  devicesString }
            }
        });
        
        if (process == null)
        {
            PrintGridTextWorkerOutput("Failed to start GridTextWorker");
            return;
        }

        _gridTextWorkerProcess = process;
        
        process.Exited += (sender, args) =>
        {
            PrintGridTextWorkerOutput($"Grid text worker exited! Exit code: {process.ExitCode}");
        };
        
        process.OutputDataReceived += (sender, args) =>
        {
            if (args.Data != null)
            {
                PrintGridTextWorkerOutput(args.Data);
            }
        };

        process.ErrorDataReceived += (sender, args) =>
        {
            if (args.Data != null)
            {
                PrintGridTextWorkerOutput(args.Data);
            }
        };
        
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
    }
    
    private void PrintGridTextWorkerOutput(string output)
    {
        if (GridTextWorkerOutput.Count > 10000)
        {
            GridTextWorkerOutput.RemoveAt(0);
        }
        
        output =  new Regex(@"\x1B\[[^@-~]*[@-~]").Replace(output, "");
        GridTextWorkerOutput.Add(output);
        logger.LogInformation(output);

        try
        {
            OnGridTextWorkerOutputChangedEvent?.Invoke(this, output);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public async Task KillWorkers()
    {
        if(_gridTextWorkerProcess != null && !_gridTextWorkerProcess.HasExited)
        {
            _gridTextWorkerProcess.Kill(true);
        }

        await WaitForExit().WaitAsync(TimeSpan.FromSeconds(10));
    }

    private async Task WaitForExit()
    {
        while(_gridTextWorkerProcess != null && !_gridTextWorkerProcess.HasExited)
        {
            await Task.Delay(100);
        }

        _gridTextWorkerProcess = null;
    }

    public void ClearOutput()
    {
        GridTextWorkerOutput.Clear();
    }

    public async Task<bool> IsRunning()
    {
        return _gridTextWorkerProcess != null && !_gridTextWorkerProcess.HasExited;
    }
}
