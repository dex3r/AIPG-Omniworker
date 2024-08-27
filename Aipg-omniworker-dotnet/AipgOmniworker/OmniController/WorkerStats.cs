namespace AipgOmniworker.OmniController;

public class WorkerStats
{
    public Instance Instance { get; set; }
    
    public bool VisibleOnApi { get; set; }
    
    public int? RequestsFulfilled { get; set; }
    public int? KudosReceived { get; set; }
    public string? WorkerId { get; set; }
}
