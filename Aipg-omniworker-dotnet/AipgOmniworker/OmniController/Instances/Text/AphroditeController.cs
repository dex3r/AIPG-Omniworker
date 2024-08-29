using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AipgOmniworker.OmniController;

public class AphroditeController
{
    public List<string> AphroditeOutput { get; private set; } = new();

    public event EventHandler<string> OnAphroditeOutputChangedEvent;

    public string WorkingDirectory => "/worker";

    private readonly UserConfigManager _userConfigManager;
    private readonly Instance _instance;
    private readonly TextWorkerConfigManager _textWorkerConfigManager;
    private readonly ILogger<AphroditeController> _logger;
    private Process? _aphroditeProcess;

    private readonly string _address = "http://localhost:2242";
    
    public AphroditeController(UserConfigManager userConfigManager,Instance instance,
        TextWorkerConfigManager textWorkerConfigManager, ILogger<AphroditeController> logger)
    {
        _userConfigManager = userConfigManager;
        _instance = instance;
        _textWorkerConfigManager = textWorkerConfigManager;
        _logger = logger;
    }

    public async Task StarAphrodite(CancellationToken cancellationToken)
    {
        try
        {
            await StartAphroditeInternal();

            cancellationToken.Register(() =>
            {
                _aphroditeProcess?.Kill(true);
                _aphroditeProcess = null;
            });
            
            if (_aphroditeProcess == null || _aphroditeProcess.HasExited)
            {
                throw new Exception("Failed to start Aphrodite");
            }
            
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (Exception e)
        {
            PrintGridTextWorkerOutput($"Failed to start Aphrodite");
            PrintGridTextWorkerOutput(e.ToString());
        }
    }

    public async Task<bool> WaitForAphriditeToStart(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(100, cancellationToken);
            
            if (_aphroditeProcess == null || _aphroditeProcess.HasExited)
            {
                return false;
            }

            try
            {
                using HttpClient client = new HttpClient();
                HttpResponseMessage response = await client.GetAsync(_address, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
            }
            catch (HttpRequestException e)
            {
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return false;
    }

    private async Task StartAphroditeInternal()
    {
        PrintGridTextWorkerOutput("Download model and starting Aphrodite (this may take a few minutes)...");

        var textWorkerConfig = await _textWorkerConfigManager.LoadConfig();
        string ModelName = textWorkerConfig.model_name;
        string HuggingFaceToken = textWorkerConfig.hugging_face_token;
        string gpu_utilization = textWorkerConfig.gpu_utilization.ToString(CultureInfo.InvariantCulture).Replace(",", ".");
        
        string devicesString = _instance.Config.Devices.Trim();
        string instanceName = _instance.GetUniqueInstanceName(await _userConfigManager.LoadConfig());

        Process? process = Process.Start(new ProcessStartInfo
        {
            FileName = "/usr/bin/python3",
            Arguments = $"-m aphrodite.endpoints.openai.api_server" +
                        $" --model {ModelName}" +
                        $" --gpu-memory-utilization {gpu_utilization}" +
                        $" --launch-kobold-api" +
                        $" --download-dir /persistent/aphrodite/models/",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            //UseShellExecute = false,
            WorkingDirectory = WorkingDirectory,
            Environment =
            {
                {"CUDA_VISIBLE_DEVICES",  devicesString}
            }
        });

        if (process == null)
        {
            PrintGridTextWorkerOutput("Failed to start Aphrodite");
            return;
        }

        _aphroditeProcess = process;

        process.Exited += (sender, args) => { PrintGridTextWorkerOutput($"Aphrodite exited! Exit code: {process.ExitCode}"); };

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
        if (AphroditeOutput.Count > 10000)
        {
            AphroditeOutput.RemoveAt(0);
        }
        
        output = new Regex(@"\x1B\[[^@-~]*[@-~]").Replace(output, "");
        AphroditeOutput.Add(output);
        _logger.LogInformation(output);

        try
        {
            OnAphroditeOutputChangedEvent?.Invoke(this, output);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public async Task KillWorkers()
    {
        if (_aphroditeProcess != null && !_aphroditeProcess.HasExited)
        {
            _aphroditeProcess.Kill(true);
        }

        await WaitForExit().WaitAsync(TimeSpan.FromSeconds(10));
    }

    private async Task WaitForExit()
    {
        while (_aphroditeProcess != null && !_aphroditeProcess.HasExited)
        {
            await Task.Delay(100);
        }

        _aphroditeProcess = null;
    }

    public void ClearOutput()
    {
        AphroditeOutput.Clear();
    }

    public async Task<bool> IsRunning()
    {
        bool isProcessAlive = _aphroditeProcess != null && !_aphroditeProcess.HasExited;

        if (!isProcessAlive)
        {
            return false;
        }
        
        try
        {
            using HttpClient client = new HttpClient();
            HttpResponseMessage response = await client.GetAsync(_address);

            if (response.IsSuccessStatusCode)
            {
                return true;
            }
        }
        catch
        {
        }

        return false;
    }
}
