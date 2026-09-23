using NXOpen;
using NXOpen.Features;

namespace BANxOpen.SheetMetal.NxAdapters.Beads;

/// <summary>Names a Bead feature after the SPEC it is built to, so the Part Navigator tells two beads apart —
/// "B1005010-2_BEAD(12)".
///
/// NX's own name keeps its counter, and that is the whole uniqueness story: the name a feature already has is unique in
/// the part, so prefixing it keeps it unique and no de-duplication pass is needed. A prefix this class wrote for a
/// previous SPEC is stripped first, so re-applying a bead to a different SPEC replaces the prefix instead of stacking
/// another one. A name the user chose themselves is prefixed, not replaced.
///
/// The name is cosmetic: <see cref="BeadFeatureIdentity"/> recognises a bead by <c>Feature.FeatureType</c>, never by its
/// name, so nothing in the traceback or the selection expansion depends on what this writes.</summary>
public static class BeadFeatureNamer
{
    /// <summary>Puts <paramref name="specId"/> in front of the feature's own name. Call it BEFORE
    /// <see cref="BeadAttributeWriter.Stamp"/>, which overwrites the previous SPEC id this reads to strip the old
    /// prefix.</summary>
    /// <returns>True when the feature now carries the name, false when NX refused it (logged through
    /// <paramref name="logWarning"/>). A refused rename is never a reason to fail the bead: the feature itself is
    /// built and stamped correctly.</returns>
    public static bool TryApplySpecName(Feature feature, string specId, Action<string> logWarning)
    {
        string currentName;
        try
        {
            currentName = feature.GetFeatureName();
        }
        catch (NXException ex)
        {
            logWarning($"Could not read a bead's feature name to put SPEC {specId} in front of it: NX {ex.ErrorCode}: {ex.Message}");
            return false;
        }

        var name = $"{specId}_{WithoutSpecPrefix(feature, currentName)}";
        if (string.Equals(name, currentName, StringComparison.Ordinal))
            return true;

        try
        {
            feature.SetName(name);
            return true;
        }
        catch (NXException ex)
        {
            logWarning($"Could not rename bead '{currentName}' to '{name}': NX {ex.ErrorCode}: {ex.Message}. The bead " +
                       $"itself is built and stamped with SPEC {specId}; only its name in the Part Navigator is NX's own.");
            return false;
        }
    }

    /// <summary>The name without the "&lt;previous SPEC id&gt;_" this class put in front of it. The previous id comes from
    /// the feature's own stamp rather than from guessing at the shape of the name, so a user's name that happens to
    /// contain an underscore is left whole.</summary>
    private static string WithoutSpecPrefix(Feature feature, string currentName)
    {
        string? previousSpecId;
        try
        {
            previousSpecId = BeadAttributeWriter.Read(feature, BeadAttributeWriter.SpecIdAttribute);
        }
        catch (NXException)
        {
            // An unreadable stamp only means no prefix is stripped, which at worst leaves a longer name.
            return currentName;
        }

        if (string.IsNullOrEmpty(previousSpecId))
            return currentName;

        var prefix = $"{previousSpecId}_";
        return currentName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? currentName.Substring(prefix.Length)
            : currentName;
    }
}
