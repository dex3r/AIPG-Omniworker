namespace AipgOmniworker.OmniController;

public class PersistentStorage
{
    private const string ConfigsPath = "/persistent/config/";
    
    private readonly SemaphoreSlim _filesystemSemaphore = new(1, 1);

    public async Task SaveConfig(string configName, string fileContent)
    {
        try
        {
            await _filesystemSemaphore.WaitAsync();
            
            string configPath = GetConfigPath(configName);
            Directory.CreateDirectory(Path.GetDirectoryName(configPath));
            await File.WriteAllTextAsync(configPath, fileContent);
        }
        finally
        {
            _filesystemSemaphore.Release();
        }
    }

    public async Task<bool> HasFile(string configName)
    {
        try
        {
            await _filesystemSemaphore.WaitAsync();
            return File.Exists(GetConfigPath(configName));
        }
        finally
        {
            _filesystemSemaphore.Release();
        }
    }

    public async Task<string[]> GetAllFiles()
    {
        try
        {
            await _filesystemSemaphore.WaitAsync();
            
            if (!Directory.Exists(ConfigsPath))
            {
                return Array.Empty<string>();
            }
            
            return Directory.GetFiles(ConfigsPath).Select(Path.GetFileName).ToArray();
        }
        finally
        {
            _filesystemSemaphore.Release();
        }
    }

    public async Task CopyConfigFromPersistentStorage(string configName, string outputPath)
    {
        try
        {
            await _filesystemSemaphore.WaitAsync();
            
            string directory = Path.GetDirectoryName(outputPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            File.Copy(GetConfigPath(configName), outputPath, true);
        }
        finally
        {
            _filesystemSemaphore.Release();
        }
    }
    
    public string GetConfigPath(string configName) => Path.Combine(ConfigsPath, configName);

    public async Task DeleteConfig(string fileName)
    {
        try
        {
            await _filesystemSemaphore.WaitAsync();
            
            string path = GetConfigPath(fileName);
        
            if(File.Exists(path))
            {
                File.Delete(path);
            }
        }
        finally
        {
            _filesystemSemaphore.Release();
        }
    }
}
