using PianoMapper.Music;
using PianoMapper.Rendering;

namespace PianoMapper.Web.Rendering;

/// <summary>
/// Where one score window's grand staff sits on its canvas, vertically: scene Y becomes <see cref="Scale"/> * Y +
/// <see cref="Offset"/>. A window whose notation reaches past what the canvas shows (a C7 is five ledger lines over the
/// treble staff, and its fingering goes above that) is moved down, or up, just far enough to fit, and only when a move is
/// not enough is the whole system shrunk, uniformly, just far enough. A window that already fits is not touched
/// (<see cref="Identity"/>), so every ordinary window renders exactly as the layout places it.
/// </summary>
/// <remarks>
/// The fit belongs to the window's static parts, and everything drawn over them (the playback cursor, the held notes and
/// their ledger lines) maps its own Y through the same fit, so no layer drifts from the notation it belongs to.
/// </remarks>
internal sealed class GrandStaffVerticalFit
{
    // canvas.js maps scene Y -1..1 onto the canvas less 18 px at each end, so a canvas h px tall shows scene Y out to
    // 1 + 18 / ((h - 36) / 2): the taller the canvas the less of that margin can be used. .score-row-canvas is
    // clamp(15rem, 22vw, 19rem), at most 304 px. Measured on that canvas the limit holds on every shorter one too, where
    // one scene unit is fewer pixels and the same margin reaches further.
    private const double CanvasMarginPixels = 18;
    private const double TallestScoreCanvasPixels = 304;
    private const double PixelsPerSceneUnit = (TallestScoreCanvasPixels - (2 * CanvasMarginPixels)) / 2;
    // Canvas kept clear between the outermost ink and the canvas edge, the 1 px border included: it is as much air as a
    // window that fits has at the edge today (3 to 4 px under a note name on the last row).
    private const double EdgeClearancePixels = 4;
    private const double VisibleLimit = 1 + ((CanvasMarginPixels - EdgeClearancePixels) / PixelsPerSceneUnit);

    // Canvas text is fixed-size pixels, whatever the scene's scale: canvas.js draws a note name at 16 px hanging below its
    // row (the ink of a name measures 12 px of that) and a fingering at 15 px centred on its row (ink reaches 5 px either
    // side, 6 here), and an unsized text glyph such as an octave-shift numeral in an 18 px font.
    private const double NoteNameInkPixels = 12;
    private const double FingeringInkReachPixels = 6;
    private const double TextGlyphHalfHeightPixels = 9;
    // A tie's arc stands at most 0.57 staff space off its note (canvas.js: 0.45 of height and 0.12 of tip bias), rounded up;
    // a slur's at most GrandStaffSceneBuilder.SlurMaximumApexInStaffSpaces.
    private const double TieMaximumApexInStaffSpaces = 0.6;
    // A stem without an end of its own (an unbeamed, unimported note) is canvas.js's 3 staff spaces long.
    private const double DefaultStemLengthInStaffSpaces = 3;

    private GrandStaffVerticalFit(double scale, double offset)
    {
        Scale = scale;
        Offset = offset;
    }

    internal static GrandStaffVerticalFit Identity { get; } = new(1, 0);

    internal double Scale { get; }

    internal double Offset { get; }

    internal bool IsIdentity => ReferenceEquals(this, Identity);

    internal double Map(double y) => (Scale * y) + Offset;

    /// <summary>
    /// The outermost point of some ink, in scene Y before any fit, and how many pixels of it reach past that point at a
    /// fixed size that no scale changes (canvas text).
    /// </summary>
    internal readonly record struct InkEdge(double Y, double FixedPixels = 0);

    /// <summary>
    /// The largest scale, at most 1, and then the smallest shift, that keeps every edge on the canvas: each top edge below
    /// the top limit and each bottom edge above the bottom one.
    /// </summary>
    internal static GrandStaffVerticalFit Create(IEnumerable<InkEdge> topEdges, IEnumerable<InkEdge> bottomEdges)
    {
        // Of the edges that reach the same fixed number of pixels past their Y, only the outermost can bind.
        InkEdge[] tops = topEdges.GroupBy(edge => edge.FixedPixels).Select(group => group.MaxBy(edge => edge.Y)).ToArray();
        InkEdge[] bottoms = bottomEdges.GroupBy(edge => edge.FixedPixels).Select(group => group.MinBy(edge => edge.Y)).ToArray();
        if (tops.Length == 0 || bottoms.Length == 0)
        {
            return Identity;
        }

        double scale = 1;
        foreach (InkEdge top in tops)
        {
            foreach (InkEdge bottom in bottoms)
            {
                double span = top.Y - bottom.Y;
                double room = (2 * VisibleLimit) - ((top.FixedPixels + bottom.FixedPixels) / PixelsPerSceneUnit);
                if (span > room)
                {
                    scale = Math.Min(scale, room / span);
                }
            }
        }

        double highestOffset = tops.Min(edge => VisibleLimit - (edge.FixedPixels / PixelsPerSceneUnit) - (scale * edge.Y));
        double lowestOffset = bottoms.Max(edge => -VisibleLimit + (edge.FixedPixels / PixelsPerSceneUnit) - (scale * edge.Y));
        bool fitsWhereItIs = scale >= 1 && lowestOffset <= 0 && highestOffset >= 0;
        return fitsWhereItIs
            ? Identity
            : new GrandStaffVerticalFit(scale, Math.Min(Math.Max(0, lowestOffset), highestOffset));
    }

    /// <summary>
    /// Fits a window's static parts to its canvas: measures what the window draws, and when that does not all fit, maps
    /// every Y of the parts through the fit that makes it. A window that fits comes back as it went in.
    /// </summary>
    internal static GrandStaffStaticScoreParts Apply(GrandStaffStaticScoreParts parts)
    {
        var topEdges = new List<InkEdge>();
        var bottomEdges = new List<InkEdge>();
        AddInkEdges(parts, topEdges, bottomEdges);
        GrandStaffVerticalFit fit = Create(topEdges, bottomEdges);
        return fit.IsIdentity ? parts : fit.Remap(parts);
    }

    private static void AddInkEdges(
        GrandStaffStaticScoreParts parts,
        List<InkEdge> topEdges,
        List<InkEdge> bottomEdges)
    {
        // Annotation bands are geometry for the review halos' avoidance, never ink, so they are not measured.
        double staffSpace = GrandStaffLayout.GetRenderedStaffSpace(Staff.Treble);
        double strokeReach = GrandStaffLayout.NotationStrokePaddingInStaffSpaces * staffSpace;
        double noteHeadReach = (GrandStaffLayout.NoteHeadHalfHeightInStaffSpaces
            + GrandStaffLayout.NotationStrokePaddingInStaffSpaces) * staffSpace;

        void AddInk(double y0, double y1, double reach, double fixedPixels = 0)
        {
            topEdges.Add(new InkEdge(Math.Max(y0, y1) + reach, fixedPixels));
            bottomEdges.Add(new InkEdge(Math.Min(y0, y1) - reach, fixedPixels));
        }

        void AddArc(double y0, double y1, StemDirection curveDirection, double apex)
        {
            if (curveDirection == StemDirection.Up)
            {
                topEdges.Add(new InkEdge(Math.Max(y0, y1) + apex));
            }
            else
            {
                bottomEdges.Add(new InkEdge(Math.Min(y0, y1) - apex));
            }
        }

        foreach (var line in parts.Lines)
        {
            AddInk(line.Y0, line.Y1, strokeReach);
        }

        foreach (var glyph in parts.Glyphs)
        {
            if (glyph.Height is { } glyphHeight)
            {
                AddInk(glyph.Y, glyph.Y, glyphHeight / 2);
            }
            else
            {
                AddInk(glyph.Y, glyph.Y, 0, TextGlyphHalfHeightPixels);
            }
        }

        foreach (var note in parts.Notes)
        {
            AddInk(note.Y, note.Y, noteHeadReach);
            if (note.HasStem)
            {
                double stemEndY = note.StemEndY
                    ?? note.Y + ((note.StemDirection == StemDirection.Up ? 1 : -1) * DefaultStemLengthInStaffSpaces * staffSpace);
                AddInk(stemEndY, stemEndY, strokeReach);
            }

            if (note.FingeringY is { } fingeringY)
            {
                AddInk(fingeringY, fingeringY, 0, FingeringInkReachPixels);
            }

            if (note.LabelY is { } labelY)
            {
                topEdges.Add(new InkEdge(labelY));
                bottomEdges.Add(new InkEdge(labelY, NoteNameInkPixels));
            }
        }

        foreach (var beam in parts.Beams)
        {
            AddInk(beam.Y0, beam.Y1, strokeReach);
        }

        foreach (var arpeggioMark in parts.ArpeggioMarks)
        {
            AddInk(arpeggioMark.Y0, arpeggioMark.Y1, strokeReach);
        }

        foreach (var tie in parts.Ties)
        {
            AddArc(tie.Y0, tie.Y1, tie.CurveDirection, TieMaximumApexInStaffSpaces * staffSpace);
        }

        foreach (var slur in parts.Slurs)
        {
            AddArc(slur.Y0, slur.Y1, slur.CurveDirection, GrandStaffSceneBuilder.SlurMaximumApexInStaffSpaces * staffSpace);
        }
    }

    private GrandStaffStaticScoreParts Remap(GrandStaffStaticScoreParts parts) => parts with
    {
        Lines = [.. parts.Lines.Select(line => line with { Y0 = Map(line.Y0), Y1 = Map(line.Y1) })],
        Glyphs = [.. parts.Glyphs.Select(glyph => glyph with { Y = Map(glyph.Y), Height = glyph.Height * Scale })],
        Notes =
        [
            .. parts.Notes.Select(note => note with
            {
                Y = Map(note.Y),
                StemEndY = note.StemEndY is { } stemEndY ? Map(stemEndY) : null,
                LabelY = note.LabelY is { } labelY ? Map(labelY) : null,
                FingeringY = note.FingeringY is { } fingeringY ? Map(fingeringY) : null,
            }),
        ],
        Beams = [.. parts.Beams.Select(beam => beam with { Y0 = Map(beam.Y0), Y1 = Map(beam.Y1) })],
        Bands = [.. parts.Bands.Select(band => band with { Y0 = Map(band.Y0), Y1 = Map(band.Y1) })],
        TrebleAnnotationLabelY = parts.TrebleAnnotationLabelY is { } trebleLabelY ? Map(trebleLabelY) : null,
        BassAnnotationLabelY = parts.BassAnnotationLabelY is { } bassLabelY ? Map(bassLabelY) : null,
        Slurs = [.. parts.Slurs.Select(slur => slur with { Y0 = Map(slur.Y0), Y1 = Map(slur.Y1) })],
        ArpeggioMarks = [.. parts.ArpeggioMarks.Select(mark => mark with { Y0 = Map(mark.Y0), Y1 = Map(mark.Y1) })],
        Ties = [.. parts.Ties.Select(tie => tie with { Y0 = Map(tie.Y0), Y1 = Map(tie.Y1) })],
        VerticalFit = this,
    };
}
