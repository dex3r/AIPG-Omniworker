using System.Diagnostics;
using System.Text.RegularExpressions;

namespace AipgOmniworker.OmniController;

public class ImageWorkerController
{
    public List<string> Output { get; private set; } = new();

    public event EventHandler<string> OnOutputChangedEvent;

    public string WorkingDirectory => "/image-worker";
    
    private Process? _workerProcess;

    public async Task StartImageWorker()
    {
        try
        {
            await StartWorkerInternal();
        }
        catch (Exception e)
        {
            PrintGridTextWorkerOutput($"Failed to start image worker");
            PrintGridTextWorkerOutput(e.ToString());
        }
    }
    
    private async Task StartWorkerInternal()
    {
        PrintGridTextWorkerOutput("Starting image worker...");

        await Task.Delay(200);
        
        Process? process = Process.Start(new ProcessStartInfo
        {
            FileName = "/image-worker/update-and-run.sh",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = WorkingDirectory,
        });
        
        if (process == null)
        {
            PrintGridTextWorkerOutput("Failed to start image worker");
            return;
        }

        _workerProcess = process;
        
        process.Exited += (sender, args) =>
        {
            PrintGridTextWorkerOutput($"Image worker exited! Exit code: {process.ExitCode}");
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
        Output.Add(output);

        try
        {
            OnOutputChangedEvent?.Invoke(this, output);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public async Task KillWorkers()
    {
        if(_workerProcess != null && !_workerProcess.HasExited)
        {
            _workerProcess.Kill(true);
        }

        await WaitForExit().WaitAsync(TimeSpan.FromSeconds(10));
    }

    private async Task WaitForExit()
    {
        while(_workerProcess != null && !_workerProcess.HasExited)
        {
            await Task.Delay(100);
        }

        _workerProcess = null;
    }

    public void ClearOutput()
    {
        Output.Clear();
    }
}
