using Newtonsoft.Json;
using YamlDotNet.Serialization;

namespace AipgOmniworker.OmniController;

[YamlSerializable]
public class UserConfig
{
    public string? ApiKey { get; set; }
    public string? WorkerName { get; set; }
    public string? HuggingFaceToken { get; set; }
    public bool AutoUpdateImageWorker { get; set; } = true;
}
