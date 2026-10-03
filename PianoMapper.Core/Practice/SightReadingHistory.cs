using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
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
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { StoreOnlyThePitchSpelling } },
    };

    /// <summary>
    /// A stored pitch needs only the three values that define it; the other members of <see cref="Pitch"/> are derived
    /// (MIDI number, frequency, staff position) and would make up two thirds of every stored pitch. Entries written
    /// before this still read fine: the extra members are ignored. Only history storage is affected, never the score
    /// wire format.
    /// </summary>
    private static void StoreOnlyThePitchSpelling(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Type != typeof(Pitch))
        {
            return;
        }

        for (int index = typeInfo.Properties.Count - 1; index >= 0; index--)
        {
            if (typeInfo.Properties[index].Name is not ("letter" or "alter" or "octave"))
            {
                typeInfo.Properties.RemoveAt(index);
            }
        }
    }

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
        foreach (SightReadingSessionSummary entry in entries.Where(ContributesToPitchMastery))
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
    /// Mastery per note and staff. Data recorded without a staff (older entries) forms a staff-agnostic bucket: it is
    /// merged into every staff of the same pitch, and stands alone (a row with a null staff) when that pitch has no
    /// staff-specific data. Legacy entries whose first-try flag mixed pitch with timing stay excluded, as in
    /// <see cref="ComputeMastery"/>. <see cref="PitchMastery"/> and its two members remain the staff-merged view.
    /// </summary>
    public IReadOnlyList<NoteMastery> ComputeNoteMastery()
    {
        Dictionary<(Pitch Pitch, Staff? Staff), NoteBucket> buckets = BuildNoteBuckets();
        var rows = new List<(Pitch Pitch, Staff? Staff, NoteBucket Bucket)>();
        foreach (Pitch pitch in buckets.Keys.Select(key => key.Pitch).Distinct())
        {
            buckets.TryGetValue((pitch, null), out NoteBucket? agnostic);
            var staffRows = buckets
                .Where(pair => pair.Key.Pitch == pitch && pair.Key.Staff is not null)
                .ToArray();
            if (staffRows.Length == 0 && agnostic is not null)
            {
                rows.Add((pitch, null, agnostic));
                continue;
            }

            foreach (var pair in staffRows)
            {
                rows.Add((pitch, pair.Key.Staff, agnostic is null ? pair.Value : pair.Value.MergedWith(agnostic)));
            }
        }

        TimeSpan? learnerMedian = GetLearnerMedianResponse(buckets);
        return rows
            .Select(row => CreateNoteMastery(row.Pitch, row.Staff, row.Bucket, learnerMedian))
            .OrderBy(mastery => mastery.Pitch.MidiNumber)
            .ThenBy(mastery => mastery.Staff)
            .ToArray();
    }

    private Dictionary<(Pitch Pitch, Staff? Staff), NoteBucket> BuildNoteBuckets()
    {
        var buckets = new Dictionary<(Pitch Pitch, Staff? Staff), NoteBucket>();
        foreach (SightReadingSessionSummary entry in entries.Where(ContributesToPitchMastery))
        {
            if (entry.NoteAttempts is { } noteAttempts)
            {
                foreach (NoteAttemptSummary attempt in noteAttempts)
                {
                    GetBucket(buckets, attempt.Pitch, attempt.Staff).Add(
                        attempt.AttemptCount,
                        attempt.PitchFirstTryCorrectCount,
                        attempt is { ResponseSampleCount: > 0, TotalResponseMilliseconds: { } total }
                            ? total / (double)attempt.ResponseSampleCount
                            : null);
                }

                continue;
            }

            foreach (PitchAttemptSummary pitchAttempt in entry.PitchAttempts.Where(attempt => attempt.AttemptCount > 0))
            {
                GetBucket(buckets, pitchAttempt.Pitch, null).Add(
                    pitchAttempt.AttemptCount,
                    pitchAttempt.CorrectFirstTryCount,
                    null);
            }
        }

        return buckets;
    }

    private static TimeSpan? GetLearnerMedianResponse(Dictionary<(Pitch Pitch, Staff? Staff), NoteBucket> buckets) =>
        Median(buckets.Values
            .Select(bucket => bucket.MedianResponseMilliseconds)
            .OfType<double>()
            .ToArray());

    private static NoteMastery CreateNoteMastery(
        Pitch pitch,
        Staff? staff,
        NoteBucket bucket,
        TimeSpan? learnerMedian)
    {
        TimeSpan? median = bucket.MedianResponseMilliseconds is { } milliseconds
            ? TimeSpan.FromMilliseconds(milliseconds)
            : null;
        double accuracy = bucket.Attempts == 0 ? 0 : 100.0 * bucket.Correct / bucket.Attempts;
        return new NoteMastery(
            pitch,
            staff,
            bucket.Attempts,
            bucket.Correct,
            median,
            NoteMastery.CalculateWeakness(accuracy, median, learnerMedian));
    }

    /// <summary>
    /// Notes with at least <see cref="MinimumMasteryAttempts"/> attempts, most in need of work first (highest weakness
    /// score), then by pitch and staff so the order is deterministic.
    /// </summary>
    public IReadOnlyList<NoteMastery> ComputeNoteMasteryWeakestFirst() =>
        ComputeNoteMastery()
            .Where(mastery => mastery.AttemptCount >= MinimumMasteryAttempts)
            .OrderByDescending(mastery => mastery.WeaknessScore)
            .ThenBy(mastery => mastery.Pitch.MidiNumber)
            .ThenBy(mastery => mastery.Staff)
            .ToArray();

    /// <summary>
    /// What is known about a note on a staff: the staff's own data with staff-agnostic data merged in, or just the
    /// staff-agnostic data (reported with a null staff) when the staff has none of its own, or null.
    /// </summary>
    public NoteMastery? GetNoteMastery(Pitch pitch, Staff staff)
    {
        Dictionary<(Pitch Pitch, Staff? Staff), NoteBucket> buckets = BuildNoteBuckets();
        buckets.TryGetValue((pitch, staff), out NoteBucket? own);
        buckets.TryGetValue((pitch, null), out NoteBucket? agnostic);
        NoteBucket? bucket = own is not null && agnostic is not null
            ? own.MergedWith(agnostic)
            : own ?? agnostic;
        return bucket is null
            ? null
            : CreateNoteMastery(pitch, own is null ? null : staff, bucket, GetLearnerMedianResponse(buckets));
    }

    private static NoteBucket GetBucket(
        Dictionary<(Pitch Pitch, Staff? Staff), NoteBucket> buckets,
        Pitch pitch,
        Staff? staff)
    {
        if (!buckets.TryGetValue((pitch, staff), out NoteBucket? bucket))
        {
            bucket = new NoteBucket();
            buckets[(pitch, staff)] = bucket;
        }

        return bucket;
    }

    private static TimeSpan? Median(IReadOnlyList<double> milliseconds)
    {
        if (milliseconds.Count == 0)
        {
            return null;
        }

        double[] ordered = [.. milliseconds.Order()];
        int middle = ordered.Length / 2;
        double median = ordered.Length % 2 == 1 ? ordered[middle] : (ordered[middle - 1] + ordered[middle]) / 2;
        return TimeSpan.FromMilliseconds(median);
    }

    private sealed class NoteBucket
    {
        private readonly List<double> sessionAverages = [];

        internal int Attempts { get; private set; }

        internal int Correct { get; private set; }

        internal double? MedianResponseMilliseconds => Median(sessionAverages)?.TotalMilliseconds;

        internal void Add(int attempts, int correct, double? sessionAverageMilliseconds)
        {
            Attempts += attempts;
            Correct += correct;
            if (sessionAverageMilliseconds is { } average)
            {
                sessionAverages.Add(average);
            }
        }

        internal NoteBucket MergedWith(NoteBucket other)
        {
            var merged = new NoteBucket();
            merged.Add(Attempts, Correct, null);
            merged.Add(other.Attempts, other.Correct, null);
            merged.sessionAverages.AddRange(sessionAverages);
            merged.sessionAverages.AddRange(other.sessionAverages);
            return merged;
        }
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

    /// <summary>
    /// A legacy entry recorded in a timed mode fused wrong-key and early/late/short/long outcomes into one first-try
    /// flag, so its per-pitch counts cannot be trusted as pitch accuracy. Pitch-only legacy entries and every
    /// current-schema entry are fine.
    /// </summary>
    private static bool ContributesToPitchMastery(SightReadingSessionSummary entry) =>
        entry.SchemaVersion > SightReadingSessionSummary.LegacySchemaVersion ||
        entry.Mode == NoteReadingMode.PitchAndOrder;

    private static SightReadingSessionSummary? TryParseEntry(JsonElement element)
    {
        try
        {
            SightReadingSessionSummary? summary = element.Deserialize<SightReadingSessionSummary>(JsonOptions);
            return summary is
            {
                SchemaVersion: >= SightReadingSessionSummary.LegacySchemaVersion
                    and <= SightReadingSessionSummary.CurrentSchemaVersion,
            }
                ? summary
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
