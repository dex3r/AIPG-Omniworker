using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http;
using System.Security.Principal;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace AIPG_Omniworker_Windows_Installer;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        
        if(Environment.GetCommandLineArgs().Length > 1
           && Environment.GetCommandLineArgs()[1] == "installNow")
        {
            RunInstall();
        }
    }

    private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        RunInstall();
    }

    private void RunInstall()
    {
        InstallButton.IsEnabled = false;
        Task.Run(Install);
    }

    private async Task Install()
    {
        await InstallButton.Dispatcher.InvokeAsync(() =>
        {
            InstallButton.IsEnabled = false;
            InstallButton.Content = "Installing...";
        });
            
        AppendLine("Starting installation...");
        
        try
        {
            if (await CheckPrivilagesRestartIfNeeded())
            {
                return;
            }
            
            await InstallChocolatey();
            await InstallWsl();
            await InstallDocker();
            await ValidateDocker();
            await InstallCuda();
            
            await InstallOmniworker();
            
            await WaitForOmniworker();
            await OpenBrowser();
        }
        catch (Exception e)
        {
            AppendLine(e.StackTrace);
            AppendLine("");
            AppendLine("Installation failed:");
            AppendLine(e.Message);
        }
        finally
        {
            await InstallButton.Dispatcher.InvokeAsync(() =>
            {
                InstallButton.IsEnabled = true;
                InstallButton.Content = "Retry Installation";
            });
        }
    }

    private async Task InstallCuda()
    {
        var output = await RunProcessAndGetOutput("nvidia-smi", "");
        if(output.Any(x => x != null && (
               x.Contains("CUDA Version: 12.6", StringComparison.InvariantCultureIgnoreCase)
               || x.Contains("CUDA Version: 12.5", StringComparison.InvariantCultureIgnoreCase)
               || x.Contains("CUDA Version: 12.4", StringComparison.InvariantCultureIgnoreCase)
               || x.Contains("CUDA Version: 12.3", StringComparison.InvariantCultureIgnoreCase)
               )))
        {
            AppendLine("CUDA already installed");
            return;
        }
        
        await InstallPackage("cuda --version 12.6.0.560");
    }

    private async Task WaitForOmniworker()
    {
        while (true)
        {
            await Task.Delay(1000);
            
            string address = "http://localhost:7870";
            
            try
            {
                using var client = new HttpClient();
                var response = await client.GetAsync(address);
                if (response.IsSuccessStatusCode)
                {
                    AppendLine("Omniworker is running!");
                    return;
                }
            }
            catch (Exception)
            {
                AppendLine("Omniworker is not running yet, waiting...");
            }
        }
    }

    private async Task<bool> CheckPrivilagesRestartIfNeeded()
    {
        if(IsAdministrator())
        {
            return false;
        }
        
        if(Environment.GetCommandLineArgs().Length > 1
           && Environment.GetCommandLineArgs()[1] == "installNow")
        {
            throw new Exception("Failed to upgrade to Administrator privileges. Please run the install as Administrator manually");
        }
        
        AppendLine("Requesting administrator privileges...");
        
        var exeName = Process.GetCurrentProcess().MainModule.FileName;
        ProcessStartInfo startInfo = new ProcessStartInfo(exeName, "installNow");
        startInfo.Verb = "runas";
        startInfo.UseShellExecute = true;
        Process.Start(startInfo);

        Application.Current.Dispatcher.Invoke(() =>
        {
            Application.Current.Shutdown();
        });
        
        return true;
    }

    private static bool IsAdministrator()
    {
        WindowsIdentity identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }


    private async Task OpenBrowser()
    {
        await RunCommand("explorer", "http://localhost:7870");
    }

    private async Task ValidateDocker()
    {
        if(!(await TryToRunProcess("docker", "--version")))
        {
            throw new Exception("Docker installation failed or computer needs restart. Try to restart your computer and run the installer again.");
        }
        
        AppendLine("Docker installed successfully");
    }

    private async Task InstallOmniworker()
    {
        await TryToRunProcess("docker", "rm -f aipg-omniworker");
        await RunCommand("docker", "pull dex3r/aipg-omniworker");
        await RunCommand("docker", 
            "run -d -p 7870:8080 --gpus \"all\" --shm-size 8g --mount source=aipg-omniworker-volume,target=/persistent --restart=unless-stopped --name aipg-omniworker dex3r/aipg-omniworker");
    }

    private async Task InstallDocker()
    {
        var output = await RunProcessAndGetOutput("docker", "--version");
        if(output.Any(x => x != null && x.Contains("Docker version")))
        {
            AppendLine("Docker already installed");
            return;
        }
        
        await InstallPackage("docker-desktop");
    }

    private async Task InstallWsl()
    {
        var processResults = await RunProcessAndGetOutput("wsl", "--version");
        if (processResults.Any(x => x != null && x.Contains("WSL version: 2.", StringComparison.InvariantCultureIgnoreCase)))
        {
            AppendLine("WSL2 already installed");
            return;
        }
        
        if(await TryToRunProcess("wsl", "--install --no-launch --web-download --no-distribution"))
        {
            return;
        }
        
        AppendLine("Failed to install WSL2 with build-in Windows feature. Trying to install with Chocolatey...");
        
        await InstallPackage("wsl2");
    }

    private async Task InstallPackage(string packageName)
    {
        await RunCommand("choco", $"install {packageName} -y");
    }

    private async Task InstallChocolatey()
    {
        var output = await RunProcessAndGetOutput("choco", "");
        if (output.Any(x => x != null && x.Contains("Chocolatey v")))
        {
            AppendLine("Chocolatey already installed");
            return;
        }
        
        AppendLine("Installing Chocolatey...");

        await RunCommand("powershell.exe",
            "Set-ExecutionPolicy Bypass -Scope Process -Force; [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072; iwr https://community.chocolatey.org/install.ps1 -UseBasicParsing | iex");
    }

    private async Task RunCommand(string path, string arguments)
    {
        int exitCode = await RunProcessAndGetExitCode(path, arguments);

        if (exitCode == 0)
        {
            AppendLine("Process executed successfully");
            return;
        }

        throw new Exception($"Process failed. Exit code: {exitCode}. Command: {path} {arguments}");
    }
    
    private async Task<bool> TryToRunProcess(string path, string arguments)
    {
        return await RunProcessAndGetExitCode(path, arguments) == 0;
    }

    private async Task<int> RunProcessAndGetExitCode(string path, string arguments)
    {
        AppendLine($"Running: {path} {arguments}");
        
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = path,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            }
        };
        
        process.OutputDataReceived += (sender, args) =>
        {
            if (args.Data == null || string.IsNullOrWhiteSpace(args.Data.Trim()))
            {
                return;
            }
            
            AppendLine($"   [Process]{args.Data.Trim()}");
        };
        process.ErrorDataReceived += (sender, args) =>
        {
            if (args.Data == null || string.IsNullOrWhiteSpace(args.Data.Trim()))
            {
                return;
            }
            
            AppendLine("   [Process][ERROR] " + args.Data.Trim());
        };
        
        process.Start();
        process.BeginOutputReadLine();
        await process.WaitForExitAsync();

        return process.ExitCode;
    }
    
    private async Task<IReadOnlyList<string?>> RunProcessAndGetOutput(string path, string arguments)
    {
        AppendLine($"Running: {path} {arguments}");
        
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = path,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            }
        };
        
        ConcurrentBag<string?> output = new();
        
        process.OutputDataReceived += (sender, args) =>
        {
            output.Add(args.Data);
            if (args.Data == null || string.IsNullOrWhiteSpace(args.Data.Trim()))
            {
                return;
            }
            
            AppendLine($"   [Process]{args.Data.Trim()}");
        };
        process.ErrorDataReceived += (sender, args) =>
        {
            output.Add(args.Data);
            if (args.Data == null || string.IsNullOrWhiteSpace(args.Data.Trim()))
            {
                return;
            }
            
            AppendLine("   [Process][ERROR] " + args.Data.Trim());
        };
        
        process.Start();
        process.BeginOutputReadLine();
        await process.WaitForExitAsync();

        return output.ToArray();
    }
    
    private void AppendLine(string text)
    {
        Output.Dispatcher.Invoke(() =>
        {
            text = text.ReplaceLineEndings();
            
            if(text.StartsWith(Environment.NewLine))
            {
                text = text.Substring(Environment.NewLine.Length);
            }
            
            if(text.EndsWith(Environment.NewLine))
            {
                text = text.Substring(0, text.Length - Environment.NewLine.Length);
            }
            
            Output.AppendText(text + "\n");
            Output.ScrollToEnd();
        });
    }
}
