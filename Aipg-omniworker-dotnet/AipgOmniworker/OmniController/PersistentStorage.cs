namespace AipgOmniworker.OmniController;

public class PersistentStorage
{
    private const string ConfigsPath = "/persistent/config/";

    public async Task SaveConfig(string configName, string fileContent)
    {
        string configPath = GetConfigPath(configName);
        Directory.CreateDirectory(Path.GetDirectoryName(configPath));
        await File.WriteAllTextAsync(configPath, fileContent);
    }

    public async Task<bool> HasFile(string configName)
    {
        return File.Exists(GetConfigPath(configName));
    }

    public async Task<string[]> GetAllFiles()
    {
        return Directory.GetFiles(ConfigsPath).Select(Path.GetFileName).ToArray();
    }

    public async Task CopyConfigFromPersistentStorage(string configName, string outputPath)
    {
        File.Copy(GetConfigPath(configName), outputPath, true);
    }
    
    public string GetConfigPath(string configName) => Path.Combine(ConfigsPath, configName);

    public async Task DeleteConfig(string fileName)
    {
        string path = GetConfigPath(fileName);
        
        if(File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
