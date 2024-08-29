// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
namespace AipgOmniworker.OmniController;

[Serializable]
public class ImageWorkerConfig
{
    public string horde_url { get; set; }
    public string api_key { get; set; }
    public object[] priority_usernames { get; set; }
    public int max_threads { get; set; }
    public int queue_size { get; set; }
    public int max_batch { get; set; }
    public bool safety_on_gpu { get; set; }
    public bool require_upfront_kudos { get; set; }
    public string dreamer_name { get; set; }
    public int max_power { get; set; }
    public object[] blacklist { get; set; }
    public bool nsfw { get; set; }
    public bool censor_nsfw { get; set; }
    public object[] censorlist { get; set; }
    public bool allow_img2img { get; set; }
    public bool allow_painting { get; set; }
    public bool allow_unsafe_ip { get; set; }
    public bool allow_post_processing { get; set; }
    public bool allow_controlnet { get; set; }
    public bool allow_lora { get; set; }
    public int max_lora_cache_size { get; set; }
    public bool dynamic_models { get; set; }
    public int number_of_dynamic_models { get; set; }
    public int max_models_to_download { get; set; }
    public int stats_output_frequency { get; set; }
    public string cache_home { get; set; }
    public string temp_dir { get; set; }
    public bool always_download { get; set; }
    public bool disable_terminal_ui { get; set; }
    public string vram_to_leave_free { get; set; }
    public string ram_to_leave_free { get; set; }
    public bool disable_disk_cache { get; set; }
    public string[] models_to_load { get; set; }
    public string[] models_to_skip { get; set; }
    public bool suppress_speed_warnings { get; set; }
    public string scribe_name { get; set; }
    public string kai_url { get; set; }
    public int max_length { get; set; }
    public int max_context_length { get; set; }
    public bool branded_model { get; set; }
    public string alchemist_name { get; set; }
    public string[] forms { get; set; }
}
