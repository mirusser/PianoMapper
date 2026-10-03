namespace PianoMapper.Web.Practice;

/// <summary>The mistakes to look at after an exercise: the first few, and how many more there are.</summary>
public sealed record ExerciseReview(IReadOnlyList<ExerciseReviewLine> Lines, int MoreCount);
