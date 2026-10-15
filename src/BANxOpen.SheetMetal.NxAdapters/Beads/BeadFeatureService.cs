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
/// fixed to Formed (per the requirements); minimum tool clearance gets NX's own default, the link to the Sheet Metal
/// Preferences that the Bead dialog records. Depth/Radius/DieRadius are bound to named expressions via
/// <see cref="ExpressionService"/> rather than set as literals.
///
/// Every builder setting follows a journal recorded from NX's own Bead dialog (NX 2412); where this class differs
/// from the dialog, the dialog wins.
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
        // The SPEC's named expressions are created or edited BEFORE the builder opens: changing part expressions
        // while a feature builder is open on the same part is not something the interactive dialog ever does. They
        // are in the part's base length unit, the unit the builder's own Height/Radius/DieRadius carry.
        BeadExpressionSet expressionSet;
        try
        {
            expressionSet = _expressionService.EnsureSpecExpressions(
                spec.StandardId, spec.SpecId,
                _settings.SpecValueFor(spec, BeadFeatureParameter.Height),
                _settings.SpecValueFor(spec, BeadFeatureParameter.Radius),
                _settings.SpecValueFor(spec, BeadFeatureParameter.DieRadius),
                _context.WorkPart.UnitCollection.GetBase("Length"));
        }
        catch (NXException ex)
        {
            _context.Log.Error($"Bead SPEC expressions could not be created: NX {ex.ErrorCode}: {ex.Message}");
            return OperationResult<Feature>.Fail("BEAD_EXPRESSIONS_FAILED", ex.Message);
        }

        if (!TryCreateBuilder(existingFeature, out var builder, out var failure))
            return failure!;

        Section? section = null;
        var committed = false;
        try
        {
            section = Configure(builder!, chain, existingFeature, side);

            // The builder's own Height/Radius/DieRadius are pre-created NXOpen.Expression handles (owned by
            // the builder, not settable as objects); they are pointed at the named expressions by formula.
            ExpressionService.BindToExpression(builder!.Height, expressionSet.Depth);
            ExpressionService.BindToExpression(builder.Radius, expressionSet.Radius);
            ExpressionService.BindToExpression(builder.DieRadius, expressionSet.DieRadius);

            if (Validate(builder) is { } invalid)
                return OperationResult<Feature>.Fail("BEAD_VALIDATION_FAILED", invalid);

            var feature = builder.CommitFeature();
            committed = true;
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

            // A committed feature owns its Section; one no feature took is left behind otherwise.
            if (!committed)
                DestroySection(_context, section, _context.Log.Warn);
        }
    }

    /// <summary>Destroys a Section this service created and no feature took, unless NX already freed it.</summary>
    public static void DestroySection(NxSessionContext context, Section? section, Action<string> logWarning)
    {
        if (section is null || !context.IsAlive(section.Tag))
            return;

        try
        {
            section.Destroy();
        }
        catch (NXException ex)
        {
            logWarning($"Destroying an unused bead Section failed: NX {ex.ErrorCode}: {ex.Message}");
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

        Section? section = null;
        try
        {
            section = Configure(builder!, chain, existingFeature, side);

            builder!.Height.SetFormula(Literal(_settings.SpecValueFor(spec, BeadFeatureParameter.Height)));
            builder.Radius.SetFormula(Literal(_settings.SpecValueFor(spec, BeadFeatureParameter.Radius)));
            builder.DieRadius.SetFormula(Literal(_settings.SpecValueFor(spec, BeadFeatureParameter.DieRadius)));

            if (Validate(builder) is { } invalid)
            {
                builder.Destroy();
                DestroySection(_context, section, _context.Log.Warn);
                return OperationResult<BeadPreview>.Fail("BEAD_VALIDATION_FAILED", invalid);
            }

            builder.PreviewBuilder.Preview();
            return OperationResult<BeadPreview>.Success(new BeadPreview(_context, builder, section, _context.Log.Warn));
        }
        catch (NXException ex)
        {
            builder!.Destroy();
            DestroySection(_context, section, _context.Log.Warn);
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

    /// <returns>The Section created for a new bead's chain, for the caller to destroy should no feature take it; null
    /// for an existing bead, whose Section is left untouched.</returns>
    private Section? Configure(BeadBuilder builder, IReadOnlyList<NXObject> chain, Feature? existingFeature, BeadBuilder.HeightSideOptions side)
    {
        builder.IncludeRounding = true;
        builder.CrossSectionType = BeadBuilder.CrossSectionTypeOptions.Circular;
        builder.EndType = BeadBuilder.EndTypeOptions.Formed;
        builder.HeightSide = side;

        // FeatureBuilder defaults this to true, which turns the bead's latest timestamped parent feature — typically
        // the previous sheet metal feature on the body — internal: hidden from the Part Navigator, yet still
        // building geometry. The recorded journal of the Bead dialog sets it false.
        builder.ParentFeatureInternal = false;

        if (existingFeature is not null)
            return null;

        // NX's own default for a new bead, as the Bead dialog records it: linked to the Sheet Metal Preferences'
        // Minimum Tool Clearance expression. A builder opened through the API does not get that link by itself.
        builder.MinimumToolClearance.SetFormula(_context.WorkPart.Preferences.SheetMetalPreferences.GetMinimumToolClearance().Name);

        return SetChainSection(builder, chain);
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

    /// <summary>Gives the builder a new Section holding the chain, in the order the recorded Bead dialog journal
    /// does it: no slave sketch, then the Section assigned. The builder's own Section is not filled in place.</summary>
    private Section SetChainSection(BeadBuilder builder, IReadOnlyList<NXObject> chain)
    {
        builder.Sketch = null;
        var section = CurveSectionFactory.Create(_context.WorkPart, chain);
        builder.Section = section;
        return section;
    }
}

/// <summary>One bead's live preview: the open builder NX draws it from. Disposing removes the preview and destroys
/// the builder — and the Section it was given — without committing, so the part is left exactly as it was. Safe to
/// dispose twice.</summary>
public sealed class BeadPreview : IDisposable
{
    private readonly NxSessionContext _context;
    private BeadBuilder? _builder;
    private Section? _section;
    private readonly Action<string> _logWarning;

    internal BeadPreview(NxSessionContext context, BeadBuilder builder, Section? section, Action<string> logWarning)
    {
        _context = context;
        _builder = builder;
        _section = section;
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

        BeadFeatureService.DestroySection(_context, _section, _logWarning);
        _section = null;
    }
}
