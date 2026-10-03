using PianoMapper.Music;

namespace PianoMapper.Web.Practice;

/// <summary>
/// A way the learner tends to be wrong on one staff: the played key is regularly this far from the written one
/// ("a step higher"), however many different notes it happened on. <see cref="Difference"/> is measured from the
/// written note to the played one.
/// </summary>
public sealed record ConfusionHabit(Staff Staff, PitchDifference Difference, int Count);
