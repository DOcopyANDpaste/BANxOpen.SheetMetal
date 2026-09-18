using BANxOpen.Foundation.Contracts.Common;
using BANxOpen.SheetMetal.Common;

namespace BANxOpen.SheetMetal.Materials;

/// <summary>What could be learned about the Sheet Metal Preferences governing one body.</summary>
/// <param name="IsSheetMetalBody">False for any body the preferences do not apply to.</param>
/// <param name="Preference">The part's preferences. Null when the body is not sheet metal, or when they could not
/// be read.</param>
/// <param name="ReadError">Set when the body is sheet metal but its preferences could not be read. Distinct from
/// "not sheet metal": "could not look" must not be mistaken for "nothing to enforce", or the restriction silently
/// lifts.</param>
/// <param name="BodyThickness">The body's own measured thickness in part units, from
/// <c>SheetmetalManager.GetBodyThickness</c>. Null when the body is not sheet metal or NX would not report it.
///
/// Distinct from <see cref="SheetMetalPartPreference.Thickness"/>, which is what the preferences <em>say</em> the
/// material is: the two disagreeing is exactly the mismatch <see cref="SheetMetalPreferenceCheck"/> reports. The
/// body's own value travels with the read because <c>SheetMetalRowChoiceProvider</c> marks the rows that match
/// it, and asking NX for it a second time from another layer could get a different answer.</param>
public sealed record SheetMetalPreferenceRead(
    bool IsSheetMetalBody,
    SheetMetalPartPreference? Preference,
    string? ReadError = null,
    double? BodyThickness = null)
{
    public static readonly SheetMetalPreferenceRead NotSheetMetal = new(false, null);

    public static SheetMetalPreferenceRead Of(SheetMetalPartPreference preference, double? bodyThickness = null) =>
        new(true, preference, null, bodyThickness);

    public static SheetMetalPreferenceRead Unreadable(string error) => new(true, null, error);
}

/// <summary>Reads the Sheet Metal Preferences that govern a body. The NX implementation reads the work part's
/// preferences; keeping that behind a seam is what lets <see cref="SheetMetalPreferenceConstraintProvider"/> stay
/// pure and testable without NX.</summary>
public interface ISheetMetalPreferenceReader
{
    SheetMetalPreferenceRead ReadFor(BodyId bodyId);
}
