using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AipgOmniworker.OmniController;

public class AphroditeController
{
    public List<string> AphroditeOutput { get; private set; } = new();

    public event EventHandler<string> OnAphroditeOutputChangedEvent;

    public string WorkingDirectory => "/worker";

    private readonly TextWorkerConfigManager _textWorkerConfigManager;
    private Process? _aphroditeProcess;

    public AphroditeController(TextWorkerConfigManager textWorkerConfigManager)
    {
        _textWorkerConfigManager = textWorkerConfigManager;
    }

    public async Task StarAphrodite()
    {
        try
        {
            await StartAphroditeInternal();
        }
        catch (Exception e)
        {
            PrintGridTextWorkerOutput($"Failed to start Aphrodite");
            PrintGridTextWorkerOutput(e.ToString());
        }
    }

    public async Task<bool> WaitForAphriditeToStart(CancellationToken cancellationToken)
    {
        string address = "http://localhost:2242";

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(100, cancellationToken);

            try
            {
                using HttpClient client = new HttpClient();
                HttpResponseMessage response = await client.GetAsync(address);

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
            }
            catch (HttpRequestException e)
            {
            }
        }

        return false;
    }

    private async Task StartAphroditeInternal()
    {
        PrintGridTextWorkerOutput("Download model and starting Aphrodite (this may take a few minutes)...");

        var textWorkerConfig = await _textWorkerConfigManager.LoadConfig();
        string ModelName = textWorkerConfig.model_name;
        string HuggingFaceToken = textWorkerConfig.hugging_face_token;
        string gpu_utilization = textWorkerConfig.gpu_utilization.ToString(CultureInfo.InvariantCulture).
            Replace(",", ".");

        Process? process = Process.Start(new ProcessStartInfo
        {
            FileName = "/usr/bin/python3",
            Arguments = $"-m aphrodite.endpoints.openai.api_server" +
                        $" --model {ModelName}" +
                        $" --gpu-memory-utilization {gpu_utilization}" +
                        $" --launch-kobold-api",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            //UseShellExecute = false,
            WorkingDirectory = WorkingDirectory,
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
        output = new Regex(@"\x1B\[[^@-~]*[@-~]").Replace(output, "");
        AphroditeOutput.Add(output);

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
}
