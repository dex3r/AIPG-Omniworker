using Newtonsoft.Json;

namespace AipgOmniworker.OmniController;

[Serializable]
public class UserConfig
{
    public string? ApiKey { get; set; }
    public string? WorkerName { get; set; }
    public string? TextModelName { get; set; } = "TheBloke/Mistral-7B-v0.1-GPTQ";
    public string? HuggingFaceToken { get; set; }
    public WorkerType WorkerType { get; set; } = WorkerType.Auto;
    public bool AutoStartWorker { get; set; }
}
