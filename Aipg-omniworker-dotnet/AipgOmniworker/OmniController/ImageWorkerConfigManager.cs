using YamlDotNet.Serialization;

namespace AipgOmniworker.OmniController;

public class ImageWorkerConfigManager(PersistentStorage persistentStorage, ILogger<ImageWorkerConfigManager> logger)
    : YamlConfigManager<ImageWorkerConfig>(persistentStorage, logger)
{
    public override string ConfigName => "imageBridgeData.yaml";
    public override string ConfigPath => "/image-worker/bridgeData.yaml";
    public override string? ConfigTemplatePath => "/image-worker/bridgeData_template.yaml";
}
