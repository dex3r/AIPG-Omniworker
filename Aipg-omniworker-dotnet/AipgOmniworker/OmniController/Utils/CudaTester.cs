using System.Diagnostics;
using System.Text.RegularExpressions;

namespace AipgOmniworker.OmniController;

public class CudaTester(ILogger<CudaTester> logger)
{
    private SemaphoreSlim _semaphore = new(1, 1);

    public async Task<bool> IsCudaAvailable()
    {
        await _semaphore.WaitAsync();

        try
        {
            return await IsCudaAvailableInternal();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error while testing CUDA availability");
            return false;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<bool> IsCudaAvailableInternal()
    {
        string isCudaAvailableCommandOutput = await RunTorchTestCommand("torch.cuda.is_available()");
        
        if(!isCudaAvailableCommandOutput.Equals("True"))
        {
            logger.LogInformation("Expected 'True' but got {Output}", isCudaAvailableCommandOutput);
            return false;
        }
        
        string cudaRunTestOutput = await RunTorchTestCommand("torch.rand(1,1).cuda()");
        bool matches = Regex.IsMatch(cudaRunTestOutput, @"tensor\(\[\[[\d\.]+\]\], device='cuda:[\d]+'\)");

        if (!matches)
        {
            logger.LogInformation("Expected tensor([[N]], device='cuda:N') but got {Output}", cudaRunTestOutput);
            return false;
        }

        return true;
    }

    private async Task<string> RunTorchTestCommand(string command)
    {
        Process? process = Process.Start(new ProcessStartInfo
        {
            FileName = "/usr/bin/python3",
            Arguments = $"-c \"import torch; print({command})\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        });
        
        if (process == null)
        {
            throw new Exception("Failed to start python process");
        }
        
        string? output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();
        
        if (!string.IsNullOrEmpty(error))
        {
            logger.LogError("Error while running torch test command: {Error}", error);
        }
        
        return output?.Trim() ?? "";
    }
}
