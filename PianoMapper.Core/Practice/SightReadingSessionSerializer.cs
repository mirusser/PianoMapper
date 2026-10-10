using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using PianoMapper.Music;

namespace PianoMapper.Practice;

/// <summary>
/// The one JSON shape of a stored <see cref="SightReadingSessionSummary"/>. The browser cache, the HTTP wire format and
/// the server's <c>jsonb</c> document all read and write through here, so there is no second shape to drift from the
/// first. The existing casing is pinned by a golden fixture, not tidied: <c>staff</c> and <c>mode</c> are camel-case
/// enum strings, while <c>presetId</c>, <c>rhythmPreset</c> and <c>motion</c> hold enum member names.
/// </summary>
public static class SightReadingSessionSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { StoreOnlyThePitchSpelling } },
    };

    public static string Serialize(SightReadingSessionSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        return JsonSerializer.Serialize(summary, JsonOptions);
    }

    /// <summary>The entries as one JSON array, in the order given.</summary>
    public static string SerializeAll(IEnumerable<SightReadingSessionSummary> summaries)
    {
        ArgumentNullException.ThrowIfNull(summaries);
        return JsonSerializer.Serialize(summaries, JsonOptions);
    }

    /// <summary>
    /// Reads one entry, or null when it is not one this version understands: malformed, an unsupported schema version,
    /// or a version 3 entry without a usable id. An entry written before schema version 3 has no id and gets a
    /// deterministic one derived from its content (the cache is not rewritten), so two browsers that upload the same
    /// old entry agree on its identity.
    /// </summary>
    public static SightReadingSessionSummary? TryParse(JsonElement element)
    {
        SightReadingSessionSummary? summary;
        try
        {
            summary = element.Deserialize<SightReadingSessionSummary>(JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            // A value the domain types refuse, such as a pitch alteration beyond a double sharp.
            return null;
        }

        // The JSON null check is on top of the type's annotations, which System.Text.Json does not enforce here.
        if (summary is not
            {
                SchemaVersion: >= SightReadingSessionSummary.LegacySchemaVersion
                    and <= SightReadingSessionSummary.CurrentSchemaVersion,
                PresetId: not null,
                PitchAttempts: not null,
            })
        {
            return null;
        }

        if (summary.SessionId != Guid.Empty)
        {
            return summary;
        }

        return summary.SchemaVersion < SightReadingSessionSummary.CurrentSchemaVersion
            ? summary with { SessionId = DeriveLegacySessionId(summary) }
            : null;
    }

    public static SightReadingSessionSummary? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            return TryParse(document.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

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

    /// <summary>
    /// A name-based id from every scalar member and the per-pitch counts, with the completion time to the millisecond,
    /// written out by hand rather than through the JSON options so a later change to the stored shape cannot change
    /// the id of an entry that is already in someone's cache. Two real sessions only collide when all of it matches.
    /// </summary>
    private static Guid DeriveLegacySessionId(SightReadingSessionSummary summary)
    {
        var identity = new StringBuilder();
        Append(identity, summary.CompletedAt.ToUnixTimeMilliseconds());
        Append(identity, summary.PresetId);
        Append(identity, summary.Staff);
        Append(identity, summary.Mode);
        Append(identity, summary.PromptCount);
        Append(identity, summary.ElapsedTime.Ticks);
        Append(identity, summary.FirstTryCorrectCount);
        Append(identity, summary.WrongAttemptCount);
        Append(identity, summary.RhythmPreset);
        Append(identity, summary.IsGrandStaff);
        Append(identity, summary.TempoBeatsPerMinute);
        Append(identity, summary.Pacing);
        Append(identity, summary.Motion);
        Append(identity, summary.PitchFirstTryCorrectCount);
        Append(identity, summary.TimingMistakeCount);
        Append(identity, summary.EarlyCount);
        Append(identity, summary.LateCount);
        Append(identity, summary.TooShortCount);
        Append(identity, summary.TooLongCount);
        foreach (PitchAttemptSummary attempt in summary.PitchAttempts)
        {
            Append(identity, attempt.Pitch.ToString());
            Append(identity, attempt.CorrectFirstTryCount);
            Append(identity, attempt.AttemptCount);
        }

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity.ToString()));
        return new Guid(hash.AsSpan(0, 16));
    }

    private static void Append(StringBuilder identity, object? value) =>
        identity.Append(Convert.ToString(value, CultureInfo.InvariantCulture)).Append('|');
}
