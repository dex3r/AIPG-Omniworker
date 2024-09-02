using YamlDotNet.Serialization;

namespace AipgOmniworker.OmniController;

[YamlSerializable]
public class BasicConfig
{
    public string? CustomHordeUrl { get; set; }

    public string GetHordeUrl()
    {
        string? baseUrl = CustomHordeUrl;

        if (string.IsNullOrEmpty(baseUrl))
        {
            return "https://api.aipowergrid.io/";
        }
        
        return $"{baseUrl}/";
    }
    
    public string GetApiUrl() => $"{GetHordeUrl()}api/";

    public string GetApiV2Url() => $"{GetApiUrl()}v2/";

    public string GetApiV2Url(string subPath) => $"{GetApiV2Url()}{subPath}";
}
