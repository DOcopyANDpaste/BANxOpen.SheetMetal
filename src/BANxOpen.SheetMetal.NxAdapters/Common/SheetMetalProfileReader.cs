using BANxOpen.SheetMetal.Beads;
using BANxOpen.SheetMetal.Materials;
using NXOpen;
using BANxOpen.Foundation.Contracts.Common;
using BANxOpen.Foundation.NxAdapters;
using BANxOpen.SheetMetal.Common;

namespace BANxOpen.SheetMetal.NxAdapters.Common;

/// <summary>Reads the sheet metal facts a SPEC gets validated against: thickness via
/// <c>SheetmetalManager.GetBodyThickness</c>, the physical material via the same
/// <c>MaterialManager.PhysicalMaterials.AskMaterialOfObject</c> path <c>PartMaterialService</c> uses in the Material
/// Assignment tool, and the part's Sheet Metal Preferences.
///
/// No grade is read here: it belongs to the standards file row the user picks. Preferences that cannot be read fail
/// the read, just as the material engine refuses an assignment it cannot check them for.</summary>
public sealed class SheetMetalProfileReader
{
    private readonly NxSessionContext _context;
    private readonly SheetMetalPreferenceService _preferences;

    public SheetMetalProfileReader(NxSessionContext context, SheetMetalPreferenceService preferences)
    {
        _context = context;
        _preferences = preferences;
    }

    public OperationResult<ProfileReadOutcome> ReadFor(Body body)
    {
        var sheetmetalManager = _context.WorkPart.Features.SheetmetalManager;

        try
        {
            if (!sheetmetalManager.IsSheetmetalBody(body))
                return OperationResult<ProfileReadOutcome>.Fail("NOT_SHEETMETAL", $"'{body.Name}' is not a sheet metal body.");
        }
        catch (NXException ex)
        {
            _context.Log.Error($"IsSheetmetalBody failed for '{body.Name}': NX {ex.ErrorCode}: {ex.Message}");
            return OperationResult<ProfileReadOutcome>.Fail("NOT_SHEETMETAL", $"'{body.Name}' could not be checked as sheet metal.");
        }

        double thickness;
        try
        {
            thickness = sheetmetalManager.GetBodyThickness(body);
        }
        catch (NXException ex)
        {
            _context.Log.Error($"GetBodyThickness failed for '{body.Name}': NX {ex.ErrorCode}: {ex.Message}");
            return OperationResult<ProfileReadOutcome>.Fail("THICKNESS_READ_FAILED", "Could not read this sheet metal's thickness.");
        }

        // Part-level: the sheet metal check and thickness are already done above, so not ReadFor(body), which repeats them.
        var preferenceRead = _preferences.ReadForPart();
        if (preferenceRead.Preference is not { } preference)
        {
            return OperationResult<ProfileReadOutcome>.Fail(
                SheetMetalPreferenceConstraintProvider.PreferencesUnreadableCode,
                $"Could not read this part's Sheet Metal Preferences: {preferenceRead.ReadError ?? "no preferences were returned"}.");
        }

        string? materialName = null;
        try
        {
            materialName = _context.WorkPart.MaterialManager.PhysicalMaterials.AskMaterialOfObject(body)?.Name;
        }
        catch (NXException)
        {
            // A body with no physical material assigned throws rather than returning null — the normal
            // "unassigned" path, not an error (same behavior PartMaterialService.ReadPhysicalMaterial relies on).
        }

        return OperationResult<ProfileReadOutcome>.Success(new ProfileReadOutcome(
            new BodyId(body.JournalIdentifier),
            body.Name ?? body.JournalIdentifier,
            thickness,
            string.IsNullOrEmpty(materialName) ? null : materialName,
            preference));
    }
}

/// <param name="PhysicalMaterialName">The body's physical material, or null when it has none.</param>
/// <param name="Preference">The part's Sheet Metal Preferences, whose <see cref="SheetMetalPartPreference.Row"/> is the
/// row they are set to.</param>
public sealed record ProfileReadOutcome(
    BodyId BodyId, string BodyName, double Thickness, string? PhysicalMaterialName, SheetMetalPartPreference Preference);
