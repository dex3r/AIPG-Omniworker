namespace AipgOmniworker.OmniController;

public class BasicConfigManager(PersistentStorage persistentStorage, ILogger<BasicConfigManager> logger)
    : YamlConfigManager<BasicConfig>(persistentStorage, logger)
{
    public override string ConfigName => "basicConfig.yaml";
    public override string ConfigPath => "/tmp/basicConfig.yaml";
    public override string? ConfigTemplatePath => null;
}
