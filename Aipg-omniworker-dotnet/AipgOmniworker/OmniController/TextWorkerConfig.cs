namespace AipgOmniworker.OmniController;

public class TextWorkerConfig
{
    public string model_name { get; set; } = "TheBloke/Mistral-7B-v0.1-GPTQ";
    public float gpu_utilization { get; set; } = 0.9f;
    public string hugging_face_token { get; set; }
    public string gpus { get; set; } = "all";
}
