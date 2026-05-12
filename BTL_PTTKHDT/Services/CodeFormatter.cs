namespace BTL_PTTKHDT.Services;

public static class CodeFormatter
{
    public static string Format(string prefix, int id, int width = 3)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            throw new ArgumentException("Prefix must not be empty.", nameof(prefix));
        }

        if (width < 1)
        {
            width = 1;
        }

        return $"{prefix}{id.ToString($"D{width}")}";
    }
}