using Newtonsoft.Json;

namespace AipgOmniworker.OmniController;

[JsonObject]
public class ApiWorkerDetails
{
    public int requests_fulfilled { get; set; }
    public double? kudos_rewards { get; set; }
    public Kudos_details? kudos_details { get; set; }
    public string performance { get; set; }
    public int threads { get; set; }
    public int uptime { get; set; }
    public bool maintenance_mode { get; set; }
    public bool nsfw { get; set; }
    public bool trusted { get; set; }
    public bool flagged { get; set; }
    public int uncompleted_jobs { get; set; }
    public string[] models { get; set; }
    public Team team { get; set; }
    public string bridge_agent { get; set; }
    public int max_pixels { get; set; }
    public double megapixelsteps_generated { get; set; }
    public bool img2img { get; set; }
    public bool painting { get; set; }
    public bool post_processing { get; set; }
    public bool lora { get; set; }
    public bool controlnet { get; set; }
    public bool sdxl_controlnet { get; set; }
    public string type { get; set; }
    public string name { get; set; }
    public string id { get; set; }
    public bool online { get; set; }
}

public class Kudos_details
{
    public double? generated { get; set; }
    public double? uptime { get; set; }
}

public class Team
{
    public object? name { get; set; }
    public object? id { get; set; }
}
