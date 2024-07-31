namespace AipgOmniworker.OmniController;

public static class StringListExtensions
{
    public static string ToOutputString(this List<string> list)
    {
        return string.Join("\n", list);
    }
}
