using PianoMapper.Practice;

namespace PianoMapper.Web.Practice;

/// <summary>The sessions the server kept, newest first, or why there are none (<see cref="Sessions"/> is then empty).</summary>
internal sealed record ProgressServerSessions(ProgressServerReach Reach, IReadOnlyList<SightReadingSessionSummary> Sessions);
