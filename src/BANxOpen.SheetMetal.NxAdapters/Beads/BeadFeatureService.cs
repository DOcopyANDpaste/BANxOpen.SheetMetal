using BANxOpen.SheetMetal.Beads;
using NXOpen;
using NXOpen.Features;
using NXOpen.Features.SheetMetal;
using BANxOpen.Foundation.Contracts.Common;
using BANxOpen.SheetMetal.NxAdapters.Common;
using BANxOpen.Foundation.NxAdapters;

namespace BANxOpen.SheetMetal.NxAdapters.Beads;

/// <summary>Creates or updates one Bead feature from a single curve/edge, per the plan's "one selected
/// curve maps to one Bead feature" rule (no chaining). Cross section is fixed to Circular and end type
/// fixed to Formed (per the requirements); minimum tool clearance is never touched, so NX's own default
/// stands. Depth/Radius/DieRadius are bound to named expressions via <see cref="ExpressionService"/>
/// rather than set as literals.</summary>
public sealed class BeadFeatureService
{
    private readonly NxSessionContext _context;
    private readonly ExpressionService _expressionService;

    public BeadFeatureService(NxSessionContext context, ExpressionService expressionService)
    {
        _context = context;
        _expressionService = expressionService;
    }

    /// <summary>Create-mode when <paramref name="existingFeature"/> is null; edit-mode (re-opens the
    /// existing feature's builder in place, curve/Section left untouched) otherwise.</summary>
    public OperationResult<Feature> CreateOrUpdate(NXObject curve, BeadSpecRow spec, Feature? existingFeature)
    {
        var sheetmetalManager = _context.WorkPart.Features.SheetmetalManager;
        BeadBuilder builder;
        try
        {
            builder = sheetmetalManager.CreateBeadFeatureBuilder(existingFeature);
        }
        catch (NXException ex)
        {
            _context.Log.Error($"CreateBeadFeatureBuilder failed: NX {ex.ErrorCode}: {ex.Message}");
            return OperationResult<Feature>.Fail("BEAD_BUILDER_CREATE_FAILED", ex.Message);
        }

        try
        {
            builder.CrossSectionType = BeadBuilder.CrossSectionTypeOptions.Circular;
            builder.EndType = BeadBuilder.EndTypeOptions.Formed;
            // MinimumToolClearance intentionally left alone — NX's own default stands, per the requirement.

            if (existingFeature is null)
                builder.Section = BuildSingleCurveSection(curve);

            // The builder's own Height/Radius/DieRadius are pre-created NXOpen.Expression handles (owned by
            // the builder, not settable as objects) — the unit is read off Height so our named expressions
            // are created in the same unit system the builder already expects, rather than guessing a unit.
            var expressionSet = _expressionService.EnsureSpecExpressions(
                spec.StandardId, spec.SpecId, spec.Height, spec.RadiusAndRadS, spec.DieRadiusP, builder.Height.Units);

            ExpressionService.BindToExpression(builder.Height, expressionSet.Depth);
            ExpressionService.BindToExpression(builder.Radius, expressionSet.Radius);
            ExpressionService.BindToExpression(builder.DieRadius, expressionSet.DieRadius);

            var validity = builder.ValidateBuilderData();
            if (validity != 0)
                return OperationResult<Feature>.Fail("BEAD_VALIDATION_FAILED", $"NX rejected the bead builder data (code {validity}).");

            var feature = builder.CommitFeature();
            return OperationResult<Feature>.Success(feature);
        }
        catch (NXException ex)
        {
            _context.Log.Error($"Bead feature create/update failed: NX {ex.ErrorCode}: {ex.Message}");
            return OperationResult<Feature>.Fail("BEAD_CREATE_FAILED", ex.Message);
        }
        finally
        {
            builder.Destroy();
        }
    }

    private Section BuildSingleCurveSection(NXObject curve)
    {
        if (curve is not IBaseCurve baseCurve)
            throw new ArgumentException($"Selected object is not a curve or edge: {curve.GetType().Name}", nameof(curve));

        var workPart = _context.WorkPart;
        var section = workPart.Sections.CreateSection();
        var rule = workPart.ScRuleFactory.CreateRuleBaseCurveDumb(new[] { baseCurve });

        section.AddToSection(new SelectionIntentRule[] { rule }, curve, null, null, new Point3d(0, 0, 0), Section.Mode.Create);
        return section;
    }
}
