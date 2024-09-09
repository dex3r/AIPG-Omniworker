namespace AipgOmniworker.OmniController;

[Serializable]
public class InstanceConfig
{
    public int InstanceId { get; set; }
    public string InstanceName { get; set; }
    public WorkerType WorkerType { get; set; } = WorkerType.Auto;
    public DeviceType DeviceType { get; set; } = DeviceType.GPU;
    public string Devices { get; set; } = "0";
    public string TextWorkerModelName { get; set; } = "TheBloke/Mistral-7B-v0.1-GPTQ";
    public bool AutoStartWorker { get; set; }
    public string[]? ImageWorkerModelsNames { get; set; } = ["AIPG_RED"];
}
