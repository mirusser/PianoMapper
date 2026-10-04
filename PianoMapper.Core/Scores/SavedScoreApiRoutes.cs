namespace PianoMapper.Scores;

public static class SavedScoreApiRoutes
{
    public const string Collection = "/api/scores";

    public static string ById(Guid id) => $"{Collection}/{id}";

    public static string List(int page, int pageSize, string? title)
    {
        string route = $"{Collection}?page={page}&pageSize={pageSize}";
        return string.IsNullOrWhiteSpace(title)
            ? route
            : $"{route}&title={Uri.EscapeDataString(title)}";
    }
}
