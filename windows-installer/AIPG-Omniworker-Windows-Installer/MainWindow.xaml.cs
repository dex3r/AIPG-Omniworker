using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http;
using System.Security.Principal;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    private bool _requiresRestart;

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
            
            if(_requiresRestart)
            {
                AppendLine("Installation requires restart. Please restart your computer and run the installer again.");
                MessageBox.Show("Installation requires restart. Please restart your computer and run the installer again.", "Omniworker");
                return;
            }
            
            await InstallOmniworker();
            
            await WaitForOmniworker();
            await OpenBrowser();

            string text = "Installation completed successfully! Open http://localhost:7870 in your browser to access Omniworker.";
            AppendLine(text);
            MessageBox.Show(text, "Omniworker");
        }
        catch (Exception e)
        {
            AppendLine(e.StackTrace);
            AppendLine("");

            string error = "Installation failed:\n";

            if(e.GetType() != typeof(Exception))
            {
                error+=(e.GetType().Name + ": " + e.Message);
            }
            else
            {
                error+=(e.Message);
            }
            
            AppendLine(error);
            MessageBox.Show(error, "Omniworker Installation Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            if (!_requiresRestart)
            {
                await InstallButton.Dispatcher.InvokeAsync(() =>
                {
                    InstallButton.IsEnabled = true;
                    InstallButton.Content = "Retry Installation";
                });
            }
        }
    }

    private async Task InstallCuda()
    {
        var output = await RunProcessAndGetOutputSafe("nvcc", "--version");
        if(output.Any(x => x != null && (
               x.Contains("release 12.6", StringComparison.InvariantCultureIgnoreCase)
               || x.Contains("release 12.5", StringComparison.InvariantCultureIgnoreCase)
               || x.Contains("release 12.4", StringComparison.InvariantCultureIgnoreCase)
               || x.Contains("release 12.3", StringComparison.InvariantCultureIgnoreCase)
               )))
        {
            AppendLine("CUDA already installed");
            return;
        }
        
        await InstallPackage("cuda --version 12.6.0.560", true);
        _requiresRestart = true;
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
        await RunProcessAndGetExitCode("explorer", "http://localhost:7870");
    }

    private async Task ValidateDocker()
    {
        if (_requiresRestart)
        {
            return;
        }
        
        if(!(await TryToRunProcess("docker", "--version")))
        {
            throw new Exception("Docker installation failed or computer needs restart. Try to restart your computer and run the installer again.");
        }

        await RunProcessAndGetOutputSafe(@"C:\Program Files\Docker\Docker\DockerCli.exe", "-SwitchLinuxEngine");

        int code = await RunProcessAndGetExitCode("docker", "ps");

        if (code != 0)
        {
            AppendLine("Docker is not running. Trying to start Docker Desktop...");
            
            await RunProcessAndGetOutputSafe(@"C:\Program Files\Docker\Docker\Docker Desktop.exe", "");
            
            AppendLine("Waiting for Docker engine to start...");
            await Task.Delay(TimeSpan.FromSeconds(20));
            
            code = await RunProcessAndGetExitCode("docker", "ps");
            
            if (code != 0)
            {
                throw new Exception("Docker is not running. Please start Docker Desktop and start the Docker Engine and retry the installation.");
            }
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
        IReadOnlyList<string?> output = null;
        
        try
        {
            output = await RunProcessAndGetOutput("docker", "--version");
        }
        catch (Win32Exception)
        {
            AppendLine("Docker not found.");
        }
        
        if(output != null && output.Any(x => x != null && x.Contains("Docker version")))
        {
            AppendLine("Docker already installed");
            return;
        }
        
        await InstallPackage("docker-desktop", true);
        _requiresRestart = true;
    }

    private async Task InstallWsl()
    {
        var processResults = await RunProcessAndGetOutputSafe("wsl", "--version");
        if (processResults.Any(x => x != null && x.Contains("WSL version: 2.", StringComparison.InvariantCultureIgnoreCase)))
        {
            AppendLine("WSL2 already installed");
            return;
        }
        
        AppendLine("WSL2 Installation not found. Trying to install WSL2 with a build-in windows tool...");

        try
        {
            if(await TryToRunProcess("wsl", "--install --no-launch --web-download --no-distribution"))
            {
                return;
            }
        }
        catch (Win32Exception e)
        {
            AppendLine($"Failed to install WSL2 with build-in Windows feature: {e.Message}");
            AppendLine("Trying to install WSL2 with Chocolatey...");
        }
        
        await InstallPackage("wsl2");
        _requiresRestart = true;
    }

    private async Task InstallPackage(string packageName, bool force = false)
    {
        if (force)
        {
            await RunCommand("choco", $"install {packageName} -y -force");
        }
        else
        {
            await RunCommand("choco", $"install {packageName} -y");
        }
    }

    private async Task InstallChocolatey()
    {
        var output = await RunProcessAndGetOutputSafe("choco", "");
        if (output.Any(x => x != null && x.Contains("Chocolatey v")))
        {
            AppendLine("Chocolatey already installed");
            return;
        }
        
        AppendLine("Chocolatey not found.");
        AppendLine("Installing Chocolatey...");

        await RunCommand("powershell.exe",
            "Set-ExecutionPolicy Bypass -Scope Process -Force; [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072; iwr https://community.chocolatey.org/install.ps1 -UseBasicParsing | iex");

        string path = Environment.GetEnvironmentVariable("PATH")!;
        path += @";C:\ProgramData\chocolatey\bin";
        Environment.SetEnvironmentVariable("PATH", path);
        
        output = await RunProcessAndGetOutputSafe("choco", "");
        if (output.Any(x => x != null && x.Contains("Chocolatey v")))
        {
            AppendLine("Chocolatey installed successfully");
            return;
        }
        
        throw new Exception("Failed to install Chocolatey");
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

    private async Task<IReadOnlyList<string?>> RunProcessAndGetOutputSafe(string path, string arguments)
    {
        try
        {
            return await RunProcessAndGetOutput(path, arguments);
        }
        catch (Win32Exception e)
        {
            return new[] {e.Message};
        }
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

            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }
            
            Output.AppendText(text + "\n");
            Output.ScrollToEnd();
        });
    }
}
