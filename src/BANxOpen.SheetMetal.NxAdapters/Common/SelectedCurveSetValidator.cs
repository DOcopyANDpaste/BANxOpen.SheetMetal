using NXOpen;
using NXOpen.Features;
using BANxOpen.Foundation.Contracts.Common;
using BANxOpen.Foundation.NxAdapters;

namespace BANxOpen.SheetMetal.NxAdapters.Common;

/// <summary>Confirms every selected curve/edge/feature resolves to the same sheet metal body before
/// anything else runs — a single chosen SPEC's validation (thickness/material) only means anything against
/// one profile.
///
/// A directly-selected <see cref="Body"/> is the answer. An <see cref="Edge"/> resolves to its owning body
/// directly via <c>Edge.GetBody()</c>. A directly-selected <see cref="Feature"/> (per the "select the bead
/// feature itself" case) resolves through <see cref="FeatureBodyResolver"/>. A free sketch <see cref="Curve"/>
/// has no such relationship anywhere
/// in the NXOpen API, so it's resolved to "the part's one sheet metal body" when there is exactly one — a
/// part with more than one sheet metal body and a sketch-curve selection can't be disambiguated this way,
/// which is a known v1 limitation reported back as a clear error rather than silently guessing.</summary>
public sealed class SelectedCurveSetValidator
{
    private readonly NxSessionContext _context;

    public SelectedCurveSetValidator(NxSessionContext context) => _context = context;

    public OperationResult<Body> ResolveSingleBody(IReadOnlyList<NXObject> selection)
    {
        if (selection.Count == 0)
            return OperationResult<Body>.Fail("NO_SELECTION", "Select at least one curve.");

        var resolvedBodyTags = new HashSet<Tag>();
        var needsSketchCurveFallback = false;

        foreach (var item in selection)
        {
            switch (item)
            {
                // The dialog lets the user point at the sheet metal body itself instead of its curves, which is
                // the one case where the answer needs no resolving at all.
                case Body selectedBody:
                    resolvedBodyTags.Add(selectedBody.Tag);
                    break;

                case Edge edge:
                    resolvedBodyTags.Add(edge.GetBody().Tag);
                    break;

                case Feature feature:
                    var body = FeatureBodyResolver.Resolve(feature);
                    if (body is null)
                    {
                        return OperationResult<Body>.Fail(
                            "FEATURE_HAS_NO_RESOLVABLE_BODY",
                            $"Could not determine which body feature '{feature.Name}' belongs to.");
                    }

                    resolvedBodyTags.Add(body.Tag);
                    break;

                default:
                    // Free sketch curves and anything else with no direct owning-body relationship.
                    needsSketchCurveFallback = true;
                    break;
            }
        }

        if (needsSketchCurveFallback)
        {
            var sheetmetalManager = _context.WorkPart.Features.SheetmetalManager;
            var sheetmetalBodies = _context.WorkPart.Bodies.Cast<Body>()
                .Where(b => IsSheetmetal(sheetmetalManager, b))
                .ToList();

            if (sheetmetalBodies.Count != 1)
            {
                return OperationResult<Body>.Fail(
                    "AMBIGUOUS_SKETCH_CURVE_BODY",
                    "One or more selected curves are sketch curves (not body edges), and this part has " +
                    $"{sheetmetalBodies.Count} sheet metal bodies, so the owning body can't be determined. " +
                    "Select curves from a part with a single sheet metal body, or select body edges instead.");
            }

            resolvedBodyTags.Add(sheetmetalBodies[0].Tag);
        }

        if (resolvedBodyTags.Count > 1)
        {
            return OperationResult<Body>.Fail(
                "MULTIPLE_BODIES_SELECTED",
                "The selected curves belong to more than one sheet metal body — select curves from a single sheet metal only.");
        }

        var bodyTag = resolvedBodyTags.Single();
        var resolvedBody = _context.WorkPart.Bodies.Cast<Body>().First(b => b.Tag.Equals(bodyTag));
        return OperationResult<Body>.Success(resolvedBody);
    }

    private static bool IsSheetmetal(NXOpen.Features.SheetMetal.SheetmetalManager manager, Body body)
    {
        try
        {
            return manager.IsSheetmetalBody(body);
        }
        catch (NXException)
        {
            return false;
        }
    }
}
