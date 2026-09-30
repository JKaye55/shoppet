namespace ShoppetApp.Helpers;

public static class MediaUrlHelper
{
#if ANDROID
    private const string ApiOrigin = "http://10.0.2.2:5020";
#else
    private const string ApiOrigin = "http://localhost:5020";
#endif

    public static string Resolve(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = value.Trim();
        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        if (trimmed.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            return ApiOrigin + trimmed;

        return trimmed;
    }

    public static bool IsServerMedia(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        (value.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase) ||
         value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
         value.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
}
