namespace BANxOpen.SheetMetal.Materials;

public sealed record PickableMaterial(string Name, string GradeLabel);

/// <summary>Seam for "what materials can the user assign when a sheet metal has none." v1's only
/// implementation (<c>NxAdapters.Materials.MaterialPickerStub</c>) is a small config-driven list, not the
/// shared material-library project mentioned in the requirements — swapping in the real integration later
/// means a new implementation of this interface, not a dialog change.</summary>
public interface IMaterialPicker
{
    IReadOnlyList<PickableMaterial> ListAssignableMaterials();
}
