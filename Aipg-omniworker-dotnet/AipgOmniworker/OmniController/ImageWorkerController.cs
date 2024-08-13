using System.Diagnostics;
using System.Text.RegularExpressions;

namespace AipgOmniworker.OmniController;

public class ImageWorkerController(Instance instance, UserConfigManager userConfigManager,
    ILogger<ImageWorkerController> logger)
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
            
            if(_workerProcess == null || _workerProcess.HasExited)
            {
                throw new Exception("Failed to start image worker");
            }
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

        UserConfig userConfig = await userConfigManager.LoadConfig();
        
        string folderPath = "/image-worker";
        
        string scriptPath = userConfig.AutoUpdateImageWorker ? "update-and-run.sh" : "horde-bridge.sh";
        
        string fullPath = Path.Combine(folderPath, scriptPath);
        
        PrintGridTextWorkerOutput($"Checking if script exists at path: {fullPath}");
        PrintGridTextWorkerOutput($"Actual full path: {Path.GetFullPath(fullPath)}");
        
        if(!Directory.Exists(folderPath))
        {
            PrintGridTextWorkerOutput($"Script directory not found: {folderPath}");
            return;
        }
        
        if(!File.Exists(fullPath))
        {
            PrintGridTextWorkerOutput($"Script file not found: {scriptPath}");
            return;
        }
        
        PrintGridTextWorkerOutput($"Script exists confirmed, running at path: {fullPath}");
        
        string devicesString = instance.Config.Devices.Trim();
        string instanceName = $"{userConfig.WorkerName} #{instance.InstanceId}";
        
        Process? process = Process.Start(new ProcessStartInfo
        {
            FileName = fullPath,
            //Arguments = $"-n {instanceName}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = WorkingDirectory,
            Environment =
            {
                {"AI_HORDE_URL", "https://api.aipowergrid.io/api/"},
                {"CUDA_VISIBLE_DEVICES",  devicesString},
                {"AIWORKER_DREAMER_NAME", instanceName}
            }
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
        if (Output.Count > 10000)
        {
            Output.RemoveAt(0);
        }
        
        output =  new Regex(@"\x1B\[[^@-~]*[@-~]").Replace(output, "");
        Output.Add(output);
        logger.LogInformation(output);

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

    public async Task<bool> IsRunning()
    {
        return _workerProcess != null && !_workerProcess.HasExited;
    }
}
