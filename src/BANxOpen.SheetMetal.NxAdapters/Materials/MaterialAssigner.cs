using NXOpen;
using BANxOpen.Foundation.Contracts.Common;
using BANxOpen.Foundation.NxAdapters;

namespace BANxOpen.SheetMetal.NxAdapters.Materials;

/// <summary>Writes one of <see cref="MaterialPickerStub"/>'s materials onto a body: reuse if it's already
/// in the part, otherwise load it from NX's own material library and assign. Same resolution shape as
/// <c>NXOPEN Projects\NxAdapters\Materials\NxPhysicalMaterialSource</c> (part first, library load only
/// after asking, since it's slow) — kept as its own small class here rather than shared, since that class
/// lives in a sibling tool's project, not in BANxOpen.Foundation.
///
/// <see cref="DefaultLibraryName"/> stands in for "the linked material project" mentioned in the
/// requirements — the real integration is future work; this is the v1 default per the plan.</summary>
public sealed class MaterialAssigner
{
    public const string DefaultLibraryName = "Sheet Metal Materials";

    private readonly NxSessionContext _context;
    private readonly Func<string, bool> _confirmLoad;

    public MaterialAssigner(NxSessionContext context, Func<string, bool>? confirmLoad = null)
    {
        _context = context;
        _confirmLoad = confirmLoad ?? NxMessageBoxHelper.Confirm;
    }

    public OperationResult<PhysicalMaterial> Assign(Body body, string materialName, string libraryName = DefaultLibraryName)
    {
        string? failureReason = null;
        var material = FindInPart(materialName) ?? LoadFromLibrary(libraryName, materialName, out failureReason);
        if (material is null)
            return OperationResult<PhysicalMaterial>.Fail("MATERIAL_LOAD_FAILED", failureReason ?? $"Could not resolve material '{materialName}'.");

        try
        {
            material.AssignObjects(new NXObject[] { body });
            return OperationResult<PhysicalMaterial>.Success(material);
        }
        catch (NXException ex)
        {
            _context.Log.Error($"Failed to assign material '{materialName}' to body '{body.Name}': NX {ex.ErrorCode}: {ex.Message}");
            return OperationResult<PhysicalMaterial>.Fail("MATERIAL_ASSIGN_FAILED", ex.Message);
        }
    }

    private PhysicalMaterial? FindInPart(string materialName)
    {
        try
        {
            return _context.WorkPart.MaterialManager.PhysicalMaterials
                .ToArray()
                .OfType<PhysicalMaterial>()
                .FirstOrDefault(m => string.Equals(m.Name, materialName, StringComparison.OrdinalIgnoreCase));
        }
        catch (NXException ex)
        {
            _context.Log.Warn($"Could not list physical materials already in the part: {ex.Message}");
            return null;
        }
    }

    private PhysicalMaterial? LoadFromLibrary(string libraryName, string materialName, out string? failureReason)
    {
        failureReason = null;

        if (!_confirmLoad(
                $"'{materialName}' is not in this part yet.{Environment.NewLine}" +
                $"Load it from the NX material library '{libraryName}'?"))
        {
            failureReason = $"Loading '{materialName}' from the NX library was declined.";
            return null;
        }

        try
        {
            _context.Log.Info($"Loading physical material '{materialName}' from NX library '{libraryName}'...");
            return _context.WorkPart.MaterialManager.PhysicalMaterials.LoadFromLibrary(libraryName, materialName);
        }
        catch (NXException ex)
        {
            failureReason = $"'{materialName}' was not found in NX library '{libraryName}' (NX {ex.ErrorCode}: {ex.Message}).";
            return null;
        }
    }
}
