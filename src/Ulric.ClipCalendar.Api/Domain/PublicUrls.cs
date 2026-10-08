namespace Ulric.ClipCalendar.Api.Domain;

public static class PublicUrls
{
    public static string Combine(string? publicBaseUrl, string relativePath)
    {
        var relative = relativePath.TrimStart('/');
        if (string.IsNullOrWhiteSpace(publicBaseUrl))
        {
            return relative;
        }

        return publicBaseUrl.TrimEnd('/') + "/" + relative;
    }
}
