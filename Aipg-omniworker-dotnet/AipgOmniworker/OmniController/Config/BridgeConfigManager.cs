using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AipgOmniworker.OmniController;

public class BridgeConfigManager(PersistentStorage persistentStorage, ILogger<BridgeConfigManager> logger) 
    : YamlConfigManager<BridgeConfig>(persistentStorage, logger)
{
    public override string ConfigName => "bridgeData.yaml";
    public override string ConfigPath => "/worker/bridgeData.yaml";
    public override string ConfigTemplatePath => "/worker/bridgeData_template.yaml";
}
