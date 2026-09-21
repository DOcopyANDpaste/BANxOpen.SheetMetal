using System.Globalization;
using BANxOpen.SheetMetal.Beads;
using NXOpen;
using NXOpen.Features;
using NXOpen.Features.SheetMetal;
using BANxOpen.Foundation.Contracts.Common;
using BANxOpen.SheetMetal.NxAdapters.Common;
using BANxOpen.Foundation.NxAdapters;

namespace BANxOpen.SheetMetal.NxAdapters.Beads;

/// <summary>Creates or updates one Bead feature from one chain of curves: the bead dialog's rule is one chain
/// collected in its curve selection maps to one Bead feature, and a single curve is simply a chain of one.
/// Cross section is fixed to Circular and end type
/// fixed to Formed (per the requirements); minimum tool clearance is never touched, so NX's own default
/// stands. Depth/Radius/DieRadius are bound to named expressions via <see cref="ExpressionService"/>
/// rather than set as literals.
///
/// The side the bead is formed to is absolute: the dialog builds every bead to the side the user chose, an
/// existing bead included, so the same selection and SPEC always produce the same bead.
///
/// Which SPEC column supplies each builder parameter comes from <see cref="BeadSettings.ParameterMapping"/>,
/// the same mapping <see cref="BeadSpecMatcher"/> uses to identify an unstamped bead — so what is written and
/// what is later recognised cannot disagree.</summary>
public sealed class BeadFeatureService
{
    private readonly NxSessionContext _context;
    private readonly ExpressionService _expressionService;
    private readonly BeadSettings _settings;

    public BeadFeatureService(NxSessionContext context, ExpressionService expressionService, BeadSettings settings)
    {
        _context = context;
        _expressionService = expressionService;
        _settings = settings;
    }

    /// <summary>Create-mode when <paramref name="existingFeature"/> is null; edit-mode (re-opens the
    /// existing feature's builder in place, curve/Section left untouched) otherwise.</summary>
    /// <param name="chain">The curves of the bead's section. Only read in create-mode.</param>
    /// <param name="side">The side of the section the bead is formed to, set in both modes.</param>
    public OperationResult<Feature> CreateOrUpdate(
        IReadOnlyList<NXObject> chain, BeadSpecRow spec, Feature? existingFeature, BeadBuilder.HeightSideOptions side)
    {
        if (!TryCreateBuilder(existingFeature, out var builder, out var failure))
            return failure!;

        try
        {
            Configure(builder!, chain, existingFeature, side);

            // The builder's own Height/Radius/DieRadius are pre-created NXOpen.Expression handles (owned by
            // the builder, not settable as objects) — the unit is read off Height so our named expressions
            // are created in the same unit system the builder already expects, rather than guessing a unit.
            var expressionSet = _expressionService.EnsureSpecExpressions(
                spec.StandardId, spec.SpecId,
                _settings.SpecValueFor(spec, BeadFeatureParameter.Height),
                _settings.SpecValueFor(spec, BeadFeatureParameter.Radius),
                _settings.SpecValueFor(spec, BeadFeatureParameter.DieRadius),
                builder!.Height.Units);

            ExpressionService.BindToExpression(builder.Height, expressionSet.Depth);
            ExpressionService.BindToExpression(builder.Radius, expressionSet.Radius);
            ExpressionService.BindToExpression(builder.DieRadius, expressionSet.DieRadius);

            if (Validate(builder) is { } invalid)
                return OperationResult<Feature>.Fail("BEAD_VALIDATION_FAILED", invalid);

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
            builder!.Destroy();
        }
    }

    /// <summary>Opens a builder for one bead and shows NX's own preview of it, without committing anything. The
    /// values are set as literals, not bound to the SPEC's named expressions: creating or editing those would
    /// change the part just to show a preview. Dispose the result to remove the preview.</summary>
    public OperationResult<BeadPreview> OpenPreview(
        IReadOnlyList<NXObject> chain, BeadSpecRow spec, Feature? existingFeature, BeadBuilder.HeightSideOptions side)
    {
        if (!TryCreateBuilder(existingFeature, out var builder, out var failure))
            return OperationResult<BeadPreview>.Fail(failure!.ErrorCode ?? "BEAD_BUILDER_CREATE_FAILED", failure.Message ?? "");

        try
        {
            Configure(builder!, chain, existingFeature, side);

            builder!.Height.SetFormula(Literal(_settings.SpecValueFor(spec, BeadFeatureParameter.Height)));
            builder.Radius.SetFormula(Literal(_settings.SpecValueFor(spec, BeadFeatureParameter.Radius)));
            builder.DieRadius.SetFormula(Literal(_settings.SpecValueFor(spec, BeadFeatureParameter.DieRadius)));

            if (Validate(builder) is { } invalid)
            {
                builder.Destroy();
                return OperationResult<BeadPreview>.Fail("BEAD_VALIDATION_FAILED", invalid);
            }

            builder.PreviewBuilder.Preview();
            return OperationResult<BeadPreview>.Success(new BeadPreview(builder, _context.Log.Warn));
        }
        catch (NXException ex)
        {
            builder!.Destroy();
            return OperationResult<BeadPreview>.Fail("BEAD_PREVIEW_FAILED", $"NX {ex.ErrorCode}: {ex.Message}");
        }
    }

    private bool TryCreateBuilder(Feature? existingFeature, out BeadBuilder? builder, out OperationResult<Feature>? failure)
    {
        try
        {
            builder = _context.WorkPart.Features.SheetmetalManager.CreateBeadFeatureBuilder(existingFeature);
            failure = null;
            return true;
        }
        catch (NXException ex)
        {
            _context.Log.Error($"CreateBeadFeatureBuilder failed: NX {ex.ErrorCode}: {ex.Message}");
            builder = null;
            failure = OperationResult<Feature>.Fail("BEAD_BUILDER_CREATE_FAILED", ex.Message);
            return false;
        }
    }

    private void Configure(BeadBuilder builder, IReadOnlyList<NXObject> chain, Feature? existingFeature, BeadBuilder.HeightSideOptions side)
    {
        builder.CrossSectionType = BeadBuilder.CrossSectionTypeOptions.Circular;
        builder.EndType = BeadBuilder.EndTypeOptions.Formed;
        builder.HeightSide = side;
        // MinimumToolClearance intentionally left alone — NX's own default stands, per the requirement.

        if (existingFeature is null)
            FillChainSection(builder.Section, chain);
    }

    /// <summary>Null when NX accepts the builder's data, else why not.</summary>
    private string? Validate(BeadBuilder builder)
    {
        var validity = builder.ValidateBuilderData();
        if (validity == 0)
            return null;

        _context.UFSession.UF.GetFailMessage(validity, out var nxMessage);
        return $"NX rejected the bead builder data (code {validity}): {nxMessage}";
    }

    private static string Literal(double value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Fills the builder's own pre-created Section the way the interactive dialog does: part modeling
    /// tolerances (chaining = 0.95 × distance, as NX records it) and curves-only. A fresh parameterless
    /// <c>Sections.CreateSection()</c> assigned over it carries zero tolerances and fails validation.</summary>
    private void FillChainSection(Section section, IReadOnlyList<NXObject> chain)
    {
        if (chain.Count == 0)
            throw new ArgumentException("A bead needs at least one curve.", nameof(chain));

        var curves = new List<IBaseCurve>();
        foreach (var item in chain)
        {
            if (item is not IBaseCurve baseCurve)
                throw new ArgumentException($"Selected object is not a curve or edge: {item.GetType().Name}", nameof(chain));
            curves.Add(baseCurve);
        }

        var workPart = _context.WorkPart;
        var distanceTolerance = workPart.Preferences.Modeling.DistanceToleranceData;
        section.DistanceTolerance = distanceTolerance;
        section.ChainingTolerance = distanceTolerance * 0.95;
        section.SetAllowedEntityTypes(Section.AllowTypes.OnlyCurves);

        var rule = workPart.ScRuleFactory.CreateRuleBaseCurveDumb(curves.ToArray());
        section.AddToSection(new SelectionIntentRule[] { rule }, chain[0], null, null, new Point3d(0, 0, 0), Section.Mode.Create);
    }
}

/// <summary>One bead's live preview: the open builder NX draws it from. Disposing removes the preview and destroys
/// the builder without committing, so the part is left exactly as it was. Safe to dispose twice.</summary>
public sealed class BeadPreview : IDisposable
{
    private BeadBuilder? _builder;
    private readonly Action<string> _logWarning;

    internal BeadPreview(BeadBuilder builder, Action<string> logWarning)
    {
        _builder = builder;
        _logWarning = logWarning;
    }

    public void Dispose()
    {
        if (_builder is not { } builder)
            return;

        _builder = null;

        // Each step on its own: a preview NX already dropped must not stop the builder being destroyed.
        try
        {
            builder.PreviewBuilder.DeletePreview();
        }
        catch (NXException ex)
        {
            _logWarning($"Removing a bead preview failed: NX {ex.ErrorCode}: {ex.Message}");
        }

        try
        {
            builder.Destroy();
        }
        catch (NXException ex)
        {
            _logWarning($"Destroying a bead preview builder failed: NX {ex.ErrorCode}: {ex.Message}");
        }
    }
}
