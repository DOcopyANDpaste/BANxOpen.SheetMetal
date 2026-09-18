using NXOpen.Features;

namespace BANxOpen.SheetMetal.NxAdapters.Beads;

/// <summary>What counts as a Bead feature. One place, because <see cref="BeadTracebackService"/> and
/// <see cref="BeadSelectionExpander"/> must agree: a feature the expander hands over as a bead that traceback
/// then refuses to recognise would show up as a phantom "new bead" the user never selected.
///
/// BEST-GUESS AREA: <see cref="FeatureTypeName"/> is inferred from NX's short-code naming convention for sheet
/// metal feature types, not confirmed against a live <c>Feature.FeatureType</c> value.</summary>
public static class BeadFeatureIdentity
{
    public const string FeatureTypeName = "BEAD";

    public static bool IsBeadFeature(Feature feature) =>
        string.Equals(feature.FeatureType, FeatureTypeName, StringComparison.OrdinalIgnoreCase);
}
