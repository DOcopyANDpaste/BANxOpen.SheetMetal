using BANxOpen.SheetMetal.Materials;

namespace BANxOpen.SheetMetal.NxAdapters.Materials;

/// <summary>v1 stand-in for the "linked material project" mentioned in the requirements — a small
/// hard-coded list behind <see cref="IMaterialPicker"/> so the real shared integration can replace this
/// class later without the dialog/presenter changing at all. Names here must have entries in
/// <c>material-grade-map.json</c> (<see cref="MaterialGradeMap"/>) once actually assigned, or the material
/// check will report the grade as unmapped.</summary>
public sealed class MaterialPickerStub : IMaterialPicker
{
    private readonly IReadOnlyList<PickableMaterial> _materials;

    public MaterialPickerStub(IReadOnlyList<PickableMaterial>? materials = null) =>
        _materials = materials ?? new[]
        {
            new PickableMaterial("Aluminum 2024, Temper O", "2024-O"),
            new PickableMaterial("Aluminum 5052, Temper O", "5052-O"),
            new PickableMaterial("Aluminum 7075, Temper O", "7075-O"),
            new PickableMaterial("Aluminum 2024, Temper T3", "2024-T3"),
            new PickableMaterial("Aluminum 7075, Temper T6", "7075-T6"),
        };

    public IReadOnlyList<PickableMaterial> ListAssignableMaterials() => _materials;
}
