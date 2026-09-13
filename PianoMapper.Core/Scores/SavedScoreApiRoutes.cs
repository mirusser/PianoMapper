namespace PianoMapper.Scores;

public static class SavedScoreApiRoutes
{
    public const string Collection = "/api/scores";

    public static string ById(Guid id) => $"{Collection}/{id}";
}
