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

    public async Task CopyConfigFromPersistentStorage(string configName, string outputPath)
    {
        File.Copy(GetConfigPath(configName), outputPath);
    }
    
    private string GetConfigPath(string configName) => Path.Combine(ConfigsPath, configName);
}
