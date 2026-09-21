using NXOpen;
using NXOpen.Features;
using BANxOpen.SheetMetal.NxAdapters.Beads;
using BANxOpen.Foundation.NxAdapters;
using CoreBeads = BANxOpen.SheetMetal.Beads;

namespace BANxOpen.SheetMetal.NxAdapters.Beads;

public sealed record CurveTraceback(Feature? ExistingFeature, CoreBeads.BeadTracebackResult Result);

/// <summary>Finds whether a selection already belongs to an existing Bead feature, and if so, reads back
/// its <see cref="BeadAttributeWriter"/> stamp. No stamp on an otherwise-matching Bead feature is reported
/// as <see cref="CoreBeads.BeadTracebackResult.HasUnstampedFeature"/> — the presenter's hard-error case,
/// since the tool refuses to guess a SPEC that was never recorded.
///
/// <see cref="Trace"/> accepts either a curve/edge (the original section-membership lookup) or a Bead
/// Feature selected directly — both are valid ways for a user to point at an existing bead. It also
/// resolves *pattern instances*: an NX Pattern Feature does not copy custom attributes onto the members it
/// generates, only the feature that was originally patterned keeps them, so a curve/edge/feature belonging
/// to an unstamped pattern member is followed back to whichever sibling member in the same pattern IS
/// stamped, and that one's SPEC is reported instead (<see cref="CoreBeads.BeadTracebackResult.IsPatternInstance"/>).
///
/// BEST-GUESS AREAS — flagged for you to confirm/correct against a live NX session with an actual
/// patterned bead:
/// 1. <see cref="BeadFeatureIdentity.FeatureTypeName"/> — "BEAD" is inferred from NX's short-code naming
///    convention for sheet metal feature types, not confirmed against a live <c>Feature.FeatureType</c> value.
/// 2. "The stamped sibling is the original" in <see cref="FindStampedPatternOriginal"/> — there is no
///    direct "get the pattern's seed feature" API in NXOpen; this infers it from which member happens to
///    carry the stamp, which only works because this tool is the one that wrote the stamp in the first
///    place.
/// 3. Editing a pattern-traced bead (<c>BeadDialogPresenter</c>/<c>BeadFeatureService</c>) re-opens the
///    ORIGINAL member's builder, not the selected instance's — assumed safe because pattern members aren't
///    independently re-editable in NX's model, not confirmed against a live pattern.</summary>
public sealed class BeadTracebackService
{
    private readonly NxSessionContext _context;

    public BeadTracebackService(NxSessionContext context) => _context = context;

    public CurveTraceback Trace(NXObject selection)
    {
        var candidate = selection is Feature selectedFeature
            ? (IsBeadFeature(selectedFeature) ? selectedFeature : null)
            : FindBeadFeatureUsingCurve(selection);

        if (candidate is null)
            return new CurveTraceback(null, new CoreBeads.BeadTracebackResult(false, false, null, null, null));

        var ownStamp = ReadStamp(candidate);
        if (ownStamp.Found)
            return new CurveTraceback(candidate, ownStamp);

        var original = FindStampedPatternOriginal(candidate);
        if (original is not null)
        {
            var originalStamp = ReadStamp(original);
            return new CurveTraceback(original, originalStamp with { IsPatternInstance = true });
        }

        // Genuinely unstamped, and not a pattern member of anything stamped either.
        return new CurveTraceback(candidate, new CoreBeads.BeadTracebackResult(false, true, null, null, null));
    }

    private static bool IsBeadFeature(Feature feature) => BeadFeatureIdentity.IsBeadFeature(feature);

    private Feature? FindBeadFeatureUsingCurve(NXObject curve)
    {
        foreach (Feature feature in _context.WorkPart.Features)
        {
            if (IsBeadFeature(feature) && UsesCurve(feature, curve))
                return feature;
        }

        return null;
    }

    /// <summary>Reads the bead's sections off the feature itself. It used to open an edit-mode builder on every bead
    /// in the part for each selected curve, and an edit builder rolls the model back to that feature while open.</summary>
    private bool UsesCurve(Feature feature, NXObject curve)
    {
        try
        {
            foreach (var section in feature.GetSections())
            {
                section.GetOutputCurves(out var sectionCurves);
                if (sectionCurves.Any(c => c.Tag.Equals(curve.Tag)))
                    return true;
            }

            return false;
        }
        catch (NXException ex)
        {
            _context.Log.Warn($"Could not read Section for feature '{feature.Name}': NX {ex.ErrorCode}: {ex.Message}");
            return false;
        }
    }

    /// <summary>Scans every PatternFeature in the part for one that contains <paramref name="instance"/>,
    /// then returns whichever other Bead member of that same pattern carries the attribute stamp — see the
    /// class doc's best-guess note #2. Null if <paramref name="instance"/> isn't a pattern member at all,
    /// or its pattern has no stamped member.</summary>
    private Feature? FindStampedPatternOriginal(Feature instance)
    {
        foreach (Feature feature in _context.WorkPart.Features)
        {
            if (feature is not PatternFeature patternFeature)
                continue;

            Feature[] members;
            try
            {
                members = patternFeature.GetAllContainedFeatures();
            }
            catch (NXException ex)
            {
                _context.Log.Warn($"Could not read members of pattern feature '{feature.Name}': NX {ex.ErrorCode}: {ex.Message}");
                continue;
            }

            if (!members.Any(m => m.Tag.Equals(instance.Tag)))
                continue;

            var stampedSibling = members.FirstOrDefault(m => IsBeadFeature(m) && ReadStamp(m).Found);
            if (stampedSibling is not null)
                return stampedSibling;
        }

        return null;
    }

    private static CoreBeads.BeadTracebackResult ReadStamp(Feature feature)
    {
        var standardId = BeadAttributeWriter.Read(feature, BeadAttributeWriter.StandardIdAttribute);
        var specId = BeadAttributeWriter.Read(feature, BeadAttributeWriter.SpecIdAttribute);

        if (standardId is null || specId is null)
            return new CoreBeads.BeadTracebackResult(false, true, null, null, null);

        // Optional on purpose: a bead stamped before this attribute existed carries the Standard and the SPEC but
        // not the bead SPEC name. That is still a complete stamp as far as Found is concerned — reporting it as
        // unstamped would tell the user this tool did not build their own bead. The caller looks the SPEC id up.
        var beadSpec = BeadAttributeWriter.Read(feature, BeadAttributeWriter.BeadSpecAttribute);

        DateTime? createdUtc = null;
        if (DateTime.TryParse(BeadAttributeWriter.Read(feature, BeadAttributeWriter.CreatedUtcAttribute), out var parsed))
            createdUtc = parsed;

        return new CoreBeads.BeadTracebackResult(true, false, standardId, specId, createdUtc, BeadSpec: beadSpec);
    }
}
