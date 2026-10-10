using System.Text.Json;
using PianoMapper.Music;
using PianoMapper.Practice;

namespace PianoMapper.Tests.UnitTests;

public sealed class SightReadingSessionSerializerTests
{
    private static readonly Guid FirstV3Id = Guid.Parse("7b0e3c1a-5d24-4f6a-9c58-2a1f6e8d4b90");
    private static readonly Guid SecondV3Id = Guid.Parse("c3f1a9d2-84b6-4e07-b1d5-0a6e92f4c718");

    [Fact]
    public void TryParse_V3Fixture_ReadsEveryMemberIncludingTheSessionId()
    {
        SightReadingSessionSummary first = ParseFixture("sight-reading-history-v3.json")[0];

        Assert.Equal(3, first.SchemaVersion);
        Assert.Equal(FirstV3Id, first.SessionId);
        Assert.Equal(new DateTimeOffset(2026, 10, 5, 17, 45, 30, 125, TimeSpan.Zero), first.CompletedAt);
        Assert.Equal("GMajor", first.PresetId);
        Assert.Equal(Staff.Treble, first.Staff);
        Assert.Equal(NoteReadingMode.PitchHoldAndRhythm, first.Mode);
        Assert.Equal(TimeSpan.FromSeconds(41.5), first.ElapsedTime);
        Assert.Equal("Basic", first.RhythmPreset);
        Assert.Equal(70, first.TempoBeatsPerMinute);
        Assert.Equal("playAlong", first.Pacing);
        Assert.Equal(2, first.NoteAttempts!.Count);
        Assert.Equal(2, Assert.Single(first.Confusions!).Count);
    }

    [Fact]
    public void SerializeAll_V3Entries_ProducesExactlyTheStoredFixture()
    {
        string fixture = File.ReadAllText(FixturePath("sight-reading-history-v3.json")).Trim();

        string written = SightReadingSessionSerializer.SerializeAll(ParseFixture("sight-reading-history-v3.json"));

        Assert.Equal(fixture, written);
    }

    [Fact]
    public void Serialize_SingleEntry_IsTheSameTextAsThatEntryInsideTheArray()
    {
        SightReadingSessionSummary[] entries = ParseFixture("sight-reading-history-v3.json");

        string single = SightReadingSessionSerializer.Serialize(entries[1]);

        Assert.Equal(
            JsonDocument.Parse(SightReadingSessionSerializer.SerializeAll(entries)).RootElement[1].GetRawText(),
            single);
    }

    [Fact]
    public void Serialize_ThenTryParse_RoundTripsAFullyPopulatedEntry()
    {
        SightReadingSessionSummary original = ParseFixture("sight-reading-history-v3.json")[0];

        SightReadingSessionSummary? reparsed =
            SightReadingSessionSerializer.TryParse(SightReadingSessionSerializer.Serialize(original));

        Assert.NotNull(reparsed);
        Assert.Equal(original.SessionId, reparsed.SessionId);
        Assert.Equal(original.PitchAttempts, reparsed.PitchAttempts);
        Assert.Equal(original.NoteAttempts, reparsed.NoteAttempts);
        Assert.Equal(original.Confusions, reparsed.Confusions);
        Assert.Equal(
            SightReadingSessionSerializer.Serialize(original),
            SightReadingSessionSerializer.Serialize(reparsed));
    }

    [Fact]
    public void SerializeAll_NoEntries_IsAnEmptyArray()
    {
        Assert.Equal("[]", SightReadingSessionSerializer.SerializeAll([]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(999)]
    public void TryParse_UnsupportedSchemaVersion_ReturnsNull(int schemaVersion)
    {
        string json = SightReadingSessionSerializer.Serialize(
            ParseFixture("sight-reading-history-v3.json")[0] with { SchemaVersion = schemaVersion });

        Assert.Null(SightReadingSessionSerializer.TryParse(json));
    }

    [Fact]
    public void TryParse_VersionThreeWithoutASessionId_ReturnsNull()
    {
        string json = SightReadingSessionSerializer.Serialize(
            ParseFixture("sight-reading-history-v3.json")[0] with { SessionId = Guid.Empty });

        Assert.Null(SightReadingSessionSerializer.TryParse(json));
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    [InlineData("{}")]
    [InlineData("""{"schemaVersion":3,"sessionId":"not-a-guid"}""")]
    public void TryParse_NotAnEntry_ReturnsNull(string json)
    {
        Assert.Null(SightReadingSessionSerializer.TryParse(json));
    }

    [Theory]
    [InlineData("\"presetId\":\"OneOctave\"", "\"presetId\":null")]
    [InlineData(
        "\"pitchAttempts\":[{\"pitch\":{\"letter\":\"c\",\"alter\":0,\"octave\":3},\"correctFirstTryCount\":4,\"attemptCount\":4}]",
        "\"pitchAttempts\":null")]
    public void TryParse_EntryWithANullRequiredMember_ReturnsNull(string from, string to)
    {
        string original = ReadFixtureEntry("sight-reading-history-v3.json", 1);
        string json = original.Replace(from, to, StringComparison.Ordinal);

        Assert.NotEqual(original, json);
        Assert.Null(SightReadingSessionSerializer.TryParse(json));
    }

    [Fact]
    public void TryParse_PitchWithAnImpossibleAlteration_ReturnsNullInsteadOfThrowing()
    {
        string json = SightReadingSessionSerializer
            .Serialize(ParseFixture("sight-reading-history-v3.json")[0])
            .Replace("\"alter\":1", "\"alter\":9", StringComparison.Ordinal);

        Assert.Null(SightReadingSessionSerializer.TryParse(json));
    }

    [Theory]
    [InlineData("sight-reading-history-v1.json")]
    [InlineData("sight-reading-history-v2.json")]
    public void TryParse_EntriesWrittenBeforeSessionIds_GetTheSameDerivedIdOnEveryParse(string fixture)
    {
        Guid[] firstParse = ParseFixture(fixture).Select(entry => entry.SessionId).ToArray();
        Guid[] secondParse = ParseFixture(fixture).Select(entry => entry.SessionId).ToArray();

        Assert.Equal(firstParse, secondParse);
        Assert.All(firstParse, id => Assert.NotEqual(Guid.Empty, id));
        Assert.Equal(firstParse.Length, firstParse.Distinct().Count());
    }

    [Fact]
    public void TryParse_EntriesWrittenBeforeSessionIds_KeepTheirSchemaVersion()
    {
        Assert.All(ParseFixture("sight-reading-history-v1.json"), entry => Assert.Equal(1, entry.SchemaVersion));
        Assert.All(ParseFixture("sight-reading-history-v2.json"), entry => Assert.Equal(2, entry.SchemaVersion));
    }

    [Fact]
    public void TryParse_DerivedIds_AreFrozenSoTwoBrowsersKeepAgreeingOnOldEntries()
    {
        // A known-good literal per fixture entry (all but the first v2 entry were also worked out independently as
        // SHA-256 over the documented fields): changing the derivation would give every browser's old entries a new
        // identity and upload them to the server a second time.
        Assert.Equal(
            [
                Guid.Parse("d3c577aa-c846-b368-4743-606bdd9d9fba"),
                Guid.Parse("7386acc0-c0ba-78d7-2663-da69dd8cd448"),
            ],
            ParseFixture("sight-reading-history-v2.json").Select(entry => entry.SessionId));
        Assert.Equal(
            [
                Guid.Parse("2babcb64-76fd-b506-74fe-695e65a40c9a"),
                Guid.Parse("d9458868-1eab-9e92-2ce3-605f65e57dda"),
                Guid.Parse("69242b38-06a0-49a7-21d2-4cc9e3cd2fa0"),
            ],
            ParseFixture("sight-reading-history-v1.json").Select(entry => entry.SessionId));
    }

    [Fact]
    public void TryParse_LegacyEntriesCompletedAMillisecondApart_GetDifferentIds()
    {
        string original = ReadFixtureEntry("sight-reading-history-v2.json", 0);
        string aMillisecondLater = original.Replace("17:45:30.125", "17:45:30.126", StringComparison.Ordinal);

        Assert.NotEqual(original, aMillisecondLater);
        Assert.NotEqual(
            SightReadingSessionSerializer.TryParse(original)!.SessionId,
            SightReadingSessionSerializer.TryParse(aMillisecondLater)!.SessionId);
    }

    [Theory]
    [InlineData("\"presetId\":\"GMajor\"", "\"presetId\":\"FMajor\"")]
    [InlineData("\"promptCount\":8", "\"promptCount\":9")]
    [InlineData("\"elapsedTime\":\"00:00:41.5000000\"", "\"elapsedTime\":\"00:00:41.6000000\"")]
    [InlineData("\"mode\":\"pitchHoldAndRhythm\"", "\"mode\":\"pitchAndRhythm\"")]
    [InlineData("\"tempoBeatsPerMinute\":70", "\"tempoBeatsPerMinute\":75")]
    [InlineData("\"attemptCount\":2},{\"pitch\":{\"letter\":\"f\"", "\"attemptCount\":3},{\"pitch\":{\"letter\":\"f\"")]
    public void TryParse_LegacyEntriesDifferingInAnyIdentifyingField_GetDifferentIds(string from, string to)
    {
        string original = ReadFixtureEntry("sight-reading-history-v2.json", 0);
        string changed = original.Replace(from, to, StringComparison.Ordinal);

        Assert.NotEqual(original, changed);
        Assert.NotEqual(
            SightReadingSessionSerializer.TryParse(original)!.SessionId,
            SightReadingSessionSerializer.TryParse(changed)!.SessionId);
    }

    [Fact]
    public void TryParse_LegacyEntryThatAlreadyCarriesAnId_KeepsIt()
    {
        string withId = ReadFixtureEntry("sight-reading-history-v2.json", 0)
            .Replace("\"schemaVersion\":2,", $"\"schemaVersion\":2,\"sessionId\":\"{SecondV3Id}\",", StringComparison.Ordinal);

        Assert.Equal(SecondV3Id, SightReadingSessionSerializer.TryParse(withId)!.SessionId);
    }

    private static SightReadingSessionSummary[] ParseFixture(string name)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(FixturePath(name)));
        return document.RootElement
            .EnumerateArray()
            .Select(element => SightReadingSessionSerializer.TryParse(element)!)
            .ToArray();
    }

    private static string ReadFixtureEntry(string name, int index)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(FixturePath(name)));
        return document.RootElement[index].GetRawText();
    }

    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
}
