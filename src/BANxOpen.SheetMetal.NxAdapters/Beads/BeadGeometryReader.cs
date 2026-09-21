using NXOpen;
using NXOpen.Features;
using NXOpen.Features.SheetMetal;
using BANxOpen.Foundation.NxAdapters;
using BANxOpen.SheetMetal.Beads;

namespace BANxOpen.SheetMetal.NxAdapters.Beads;

/// <summary>Measures an existing bead so an unstamped one can be identified against the SPEC workbooks.
///
/// Reads back exactly what <see cref="BeadFeatureService"/> writes — the builder's Height, Radius and
/// DieRadius — plus the thickness of the body the bead is on. The raw expression values are used, in the same
/// units the tool writes them in, so a bead built by the tool round-trips to its SPEC exactly.</summary>
public sealed class BeadGeometryReader
{
    private readonly NxSessionContext _context;

    public BeadGeometryReader(NxSessionContext context) => _context = context;

    /// <summary>Null when the feature cannot be opened as a bead. Callers treat that as "cannot be identified", which
    /// warns rather than guessing.</summary>
    /// <param name="thickness">The thickness of the body the bead is on, which the caller has already read.</param>
    public BeadGeometry? Read(Feature beadFeature, double thickness)
    {
        BeadBuilder builder;
        try
        {
            builder = _context.WorkPart.Features.SheetmetalManager.CreateBeadFeatureBuilder(beadFeature);
        }
        catch (NXException ex)
        {
            _context.Log.Warn($"Could not open bead '{beadFeature.Name}' to read its geometry: NX {ex.ErrorCode}: {ex.Message}");
            return null;
        }

        try
        {
            return new BeadGeometry(
                Thickness: thickness,
                Height: builder.Height.Value,
                Radius: builder.Radius.Value,
                DieRadius: builder.DieRadius.Value);
        }
        catch (NXException ex)
        {
            _context.Log.Warn($"Could not read the parameters of bead '{beadFeature.Name}': NX {ex.ErrorCode}: {ex.Message}");
            return null;
        }
        finally
        {
            // Opened only to read; destroying without committing leaves the feature untouched.
            builder.Destroy();
        }
    }
}
