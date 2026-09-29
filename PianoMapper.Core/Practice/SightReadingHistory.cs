using System.Text.Json;
using System.Text.Json.Serialization;
using PianoMapper.Music;

namespace PianoMapper.Practice;

/// <summary>
/// A pure, bounded, newest-first collection of completed exercise summaries. Has no knowledge of browser storage;
/// <c>PianoMapper.Web</c> adapts <see cref="FromJson"/>/<see cref="ToJson"/> to local storage. Parsing tolerates
/// malformed or unrecognized individual entries (a future schema version, corrupt JSON for one array element) by
/// skipping just that entry rather than losing the whole history.
/// </summary>
public sealed class SightReadingHistory
{
    /// <summary>
    /// The fewest attempts a pitch needs across history before its accuracy is considered meaningful enough to
    /// rank in <see cref="ComputeMasteryWeakestFirst"/>. Pitches below this show as "not enough data yet" instead.
    /// </summary>
    public const int MinimumMasteryAttempts = 5;

    private const int MaxEntries = 100;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly List<SightReadingSessionSummary> entries;

    private SightReadingHistory(IEnumerable<SightReadingSessionSummary> entries)
    {
        this.entries = entries
            .OrderByDescending(entry => entry.CompletedAt)
            .Take(MaxEntries)
            .ToList();
    }

    public static SightReadingHistory Empty { get; } = new([]);

    public IReadOnlyList<SightReadingSessionSummary> Entries => entries;

    public static SightReadingHistory FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Empty;
        }

        JsonElement root;
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            root = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return Empty;
        }

        if (root.ValueKind != JsonValueKind.Array)
        {
            return Empty;
        }

        var validEntries = new List<SightReadingSessionSummary>();
        foreach (JsonElement element in root.EnumerateArray())
        {
            if (TryParseEntry(element) is { } summary)
            {
                validEntries.Add(summary);
            }
        }

        return new SightReadingHistory(validEntries);
    }

    public string ToJson() => JsonSerializer.Serialize(entries, JsonOptions);

    public SightReadingHistory WithCompletedSession(SightReadingSessionSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        return new SightReadingHistory(entries.Prepend(summary));
    }

    public IReadOnlyList<PitchMastery> ComputeMastery()
    {
        var totals = new Dictionary<Pitch, (int Correct, int Attempts)>();
        foreach (SightReadingSessionSummary entry in entries)
        {
            foreach (PitchAttemptSummary pitchAttempt in entry.PitchAttempts)
            {
                (int correct, int attempts) = totals.GetValueOrDefault(pitchAttempt.Pitch);
                totals[pitchAttempt.Pitch] = (
                    correct + pitchAttempt.CorrectFirstTryCount,
                    attempts + pitchAttempt.AttemptCount);
            }
        }

        return totals
            .Select(pair => new PitchMastery(pair.Key, pair.Value.Correct, pair.Value.Attempts))
            .ToArray();
    }

    /// <summary>
    /// Pitches with at least <see cref="MinimumMasteryAttempts"/> attempts, weakest accuracy first, then by pitch
    /// for deterministic ordering when accuracy ties.
    /// </summary>
    public IReadOnlyList<PitchMastery> ComputeMasteryWeakestFirst() =>
        ComputeMastery()
            .Where(mastery => mastery.AttemptCount >= MinimumMasteryAttempts)
            .OrderBy(mastery => mastery.AccuracyPercent)
            .ThenBy(mastery => mastery.Pitch.MidiNumber)
            .ToArray();

    private static SightReadingSessionSummary? TryParseEntry(JsonElement element)
    {
        try
        {
            SightReadingSessionSummary? summary = element.Deserialize<SightReadingSessionSummary>(JsonOptions);
            return summary is { SchemaVersion: SightReadingSessionSummary.CurrentSchemaVersion }
                ? summary
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
