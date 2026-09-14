using BANxOpen.SheetMetal.Beads;
using BANxOpen.SheetMetal.Materials;
using NXOpen;
using BANxOpen.Foundation.Contracts.Common;
using BANxOpen.Foundation.NxAdapters;
using BANxOpen.SheetMetal.Common;

namespace BANxOpen.SheetMetal.NxAdapters.Common;

/// <summary>Reads the sheet metal facts (<see cref="SheetMetalProfile"/>) a SPEC gets validated against:
/// thickness via <c>SheetmetalManager.GetBodyThickness</c>, material via the same
/// <c>MaterialManager.PhysicalMaterials.AskMaterialOfObject</c> path <c>PartMaterialService</c> uses in the
/// Material Assignment tool, mapped to the workbook's short grade label via <see cref="MaterialGradeMap"/>.
/// <see cref="MaterialMissing"/> on the result is what tells the presenter to show the material picker
/// instead of treating "no material" as a hard error.
///
/// The part's Sheet Metal Preferences are read alongside (<see cref="ProfileReadOutcome.Preference"/>) so the
/// presenter can check the body against them before any SPEC. Preferences that cannot be read fail the read, just
/// as the material engine refuses an assignment it cannot check them for.</summary>
public sealed class SheetMetalProfileReader
{
    private readonly NxSessionContext _context;
    private readonly MaterialGradeMap _gradeMap;
    private readonly SheetMetalPreferenceService _preferences;

    public SheetMetalProfileReader(NxSessionContext context, MaterialGradeMap gradeMap, SheetMetalPreferenceService preferences)
    {
        _context = context;
        _gradeMap = gradeMap;
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

        var preferenceRead = _preferences.ReadFor(body);
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

        // Thickness is still reported without a material: the material picker narrows its list to grades that a
        // SPEC at this thickness allows, and the tree should show a thickness that has, in fact, been read.
        if (string.IsNullOrEmpty(materialName))
            return OperationResult<ProfileReadOutcome>.Success(new ProfileReadOutcome(null, MaterialMissing: true, MaterialName: null, Thickness: thickness, Preference: preference));

        var grade = _gradeMap.GradeFor(materialName!);
        if (grade is null)
        {
            return OperationResult<ProfileReadOutcome>.Fail(
                "MATERIAL_GRADE_UNMAPPED",
                $"NX material '{materialName}' has no entry in the material-grade map — add one before validating a SPEC.");
        }

        var profile = new SheetMetalProfile(new BodyId(body.JournalIdentifier), body.Name ?? body.JournalIdentifier, thickness, grade);
        return OperationResult<ProfileReadOutcome>.Success(new ProfileReadOutcome(profile, MaterialMissing: false, MaterialName: materialName, Thickness: thickness, Preference: preference));
    }
}

/// <param name="Preference">The part's Sheet Metal Preferences, for the presenter to check the body against.</param>
public sealed record ProfileReadOutcome(SheetMetalProfile? Profile, bool MaterialMissing, string? MaterialName, double Thickness, SheetMetalPartPreference Preference);
