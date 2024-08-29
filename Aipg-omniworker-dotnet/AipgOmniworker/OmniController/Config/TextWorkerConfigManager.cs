using YamlDotNet.Serialization;

namespace AipgOmniworker.OmniController;

public class TextWorkerConfigManager(PersistentStorage persistentStorage, ILogger<TextWorkerConfigManager> logger) 
    : YamlConfigManager<TextWorkerConfig>(persistentStorage, logger)
{
    public override string ConfigName => "textWorkerConfig.yaml";
    public override string ConfigPath => "/worker/textWorkerConfig.yaml";
    public override string? ConfigTemplatePath => null;
}
