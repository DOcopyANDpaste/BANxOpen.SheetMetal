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
public sealed record SheetMetalPreferenceRead(bool IsSheetMetalBody, SheetMetalPartPreference? Preference, string? ReadError = null)
{
    public static readonly SheetMetalPreferenceRead NotSheetMetal = new(false, null);

    public static SheetMetalPreferenceRead Of(SheetMetalPartPreference preference) => new(true, preference);

    public static SheetMetalPreferenceRead Unreadable(string error) => new(true, null, error);
}

/// <summary>Reads the Sheet Metal Preferences that govern a body. The NX implementation reads the work part's
/// preferences; keeping that behind a seam is what lets <see cref="SheetMetalPreferenceConstraintProvider"/> stay
/// pure and testable without NX.</summary>
public interface ISheetMetalPreferenceReader
{
    SheetMetalPreferenceRead ReadFor(BodyId bodyId);
}
