namespace BANxOpen.SheetMetal.Beads;

/// <summary>The measurable shape of an existing bead, read back off the model.
///
/// These are the values the tool writes into a bead feature (see BeadFeatureService), plus the thickness of
/// the sheet it sits on. Width is absent on purpose: the circular cross section the tool builds has no width
/// parameter, so a SPEC's Width is never stored on the feature and cannot be recovered from it.
///
/// Values are in the units the tool writes them in, which are the SPEC workbook's units, so a bead built by
/// the tool round-trips exactly.</summary>
public sealed record BeadGeometry(double Thickness, double Height, double Radius, double DieRadius)
{
    public double ValueOf(BeadFeatureParameter parameter) => parameter switch
    {
        BeadFeatureParameter.Thickness => Thickness,
        BeadFeatureParameter.Height => Height,
        BeadFeatureParameter.Radius => Radius,
        BeadFeatureParameter.DieRadius => DieRadius,
        _ => throw new ArgumentOutOfRangeException(nameof(parameter), parameter, null),
    };
}

/// <summary>The SPEC rows a bead's geometry is consistent with.</summary>
public sealed record BeadSpecMatch(IReadOnlyList<BeadSpecRow> Candidates)
{
    /// <summary>The one SPEC the bead can be identified as, or null when there is none or more than one.</summary>
    public BeadSpecRow? Single => Candidates.Count == 1 ? Candidates[0] : null;

    public bool IsAmbiguous => Candidates.Count > 1;

    public bool IsNone => Candidates.Count == 0;
}

/// <summary>Identifies which SPEC an unstamped bead — one built by hand in NX rather than by the tool — was
/// built to, from its geometry alone.
///
/// Only an unambiguous match identifies a bead. When several SPECs fit, picking one would be a guess, and a
/// guessed SPEC would then enforce the wrong material restriction with full confidence; reporting it as
/// ambiguous lets the caller warn instead. The common cause is two SPECs that differ only in a column no
/// mapped parameter reads, such as Width.</summary>
public static class BeadSpecMatcher
{
    /// <summary>A row matches when every parameter in <see cref="BeadSettings.ParameterMapping"/> is within
    /// <see cref="BeadSettings.GeometryMatchTolerance"/> of its mapped column.</summary>
    public static BeadSpecMatch Match(BeadGeometry geometry, IEnumerable<BeadSpecRow> rows, BeadSettings settings)
    {
        var candidates = rows
            .Where(row => settings.ParameterMapping.All(mapping =>
                Math.Abs(geometry.ValueOf(mapping.Feature) - BeadSpecColumns.ValueOf(row, mapping.SpecColumn))
                    <= settings.GeometryMatchTolerance))
            // The same SPEC can reach here twice if two Standards share a workbook row; that is one SPEC,
            // not an ambiguity.
            .GroupBy(row => (row.StandardId, row.SpecId))
            .Select(group => group.First())
            .ToList();

        return new BeadSpecMatch(candidates);
    }
}
