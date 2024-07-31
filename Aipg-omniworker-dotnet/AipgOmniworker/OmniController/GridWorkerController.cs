using System.Diagnostics;
using System.Text.RegularExpressions;

namespace AipgOmniworker.OmniController;

public class GridWorkerController
{
    public List<string> GridTextWorkerOutput { get; private set; } = new();

    public event EventHandler<string> OnGridTextWorkerOutputChangedEvent;

    public string WorkingDirectory => "/worker";
    
    private Process? _gridTextWorkerProcess;
    
    
    public async Task Initialize()
    {
    }

    public async Task StartGridTextWorker()
    {
        try
        {
            await StartGridTextWorkerInternal();
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

        await Task.Delay(200);
        
        Process? process = Process.Start(new ProcessStartInfo
        {
            //FileName = "/usr/bin/python3",
            //Arguments = "-s bridge_scribe.py",
            FileName = "/worker/run-worker.sh",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            //UseShellExecute = false,
            WorkingDirectory = WorkingDirectory,
            Environment = { { "DISABLE_TERMINAL_UI", "true" } }
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
        output =  new Regex(@"\x1B\[[^@-~]*[@-~]").Replace(output, "");
        GridTextWorkerOutput.Add(output);

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
}
