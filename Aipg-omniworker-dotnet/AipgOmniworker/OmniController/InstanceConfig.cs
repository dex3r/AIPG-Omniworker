namespace AipgOmniworker.OmniController;

public class InstanceConfig
{
    public int InstanceId { get; set; }
    public string InstanceName { get; set; }
    public WorkerType WorkerType { get; set; } = WorkerType.Auto;
    public DeviceType DeviceType { get; set; } = DeviceType.GPU;
    public string Devices { get; set; } = "0";
    public bool AutoStartWorker { get; set; }
}
