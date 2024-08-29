namespace AipgOmniworker.OmniController;

public static class DevicesIdsParser
{
    public static bool TryParse(string input, out int[] devicesIds)
    {
        devicesIds = Array.Empty<int>();
        
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        string[] ids = input.Split(',', StringSplitOptions.RemoveEmptyEntries);
        
        List<int> parsedIds = new();

        foreach (string id in ids)
        {
            if (int.TryParse(id.Trim(), out int parsedId))
            {
                parsedIds.Add(parsedId);
            }
            else
            {
                return false;
            }
        }

        devicesIds = parsedIds.ToArray();
        return true;
    }
    
    public static string ToString(int[]? devicesIds)
    {
        if (devicesIds == null)
        {
            return "0";
        }
        
        return string.Join(',', devicesIds);
    }
}
