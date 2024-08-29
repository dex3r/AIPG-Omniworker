// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
namespace AipgOmniworker.OmniController;

[Serializable]
public class BridgeConfig
{
    public string horde_url { get; set; }
    public string worker_name { get; set; }
    public string api_key { get; set; }
    public int max_threads { get; set; }
    public int queue_size { get; set; }
    public string scribe_name { get; set; }
    public string kai_url { get; set; }
    public int max_length { get; set; }
    public int max_context_length { get; set; }
}
