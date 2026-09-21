using System.Reflection;
using NXOpen;
using NXOpen.Features;

namespace BANxOpen.SheetMetal.NxAdapters.Beads;

/// <summary>Stamps every created/updated Bead feature with which tool/version/SPEC drove it — the app name
/// and assembly version identify the tool, the Standard/SPEC ids are what makes <c>BeadTracebackService</c>
/// possible later, and the date is a plain audit trail.</summary>
public static class BeadAttributeWriter
{
    public const string AppNameAttribute = "BEAD_APP_NAME";
    public const string AppVersionAttribute = "BEAD_APP_VERSION";
    public const string CreatedUtcAttribute = "BEAD_CREATED_UTC";
    public const string ModifiedUtcAttribute = "BEAD_MODIFIED_UTC";
    public const string StandardIdAttribute = "BEAD_STANDARD_ID";
    public const string SpecIdAttribute = "BEAD_SPEC_ID";

    /// <summary>Which of the Standard's bead SPECs the row came from — the name the dialog offers, not the workbook's
    /// file name, so a file rename or a revision suffix does not orphan a stamped bead.
    ///
    /// Added after the first release: beads stamped before it have the Standard and SPEC attributes but not this one,
    /// so <c>BeadTracebackService</c> reads it as optional and the dialog falls back to looking the SPEC id up.</summary>
    public const string BeadSpecAttribute = "BEAD_SPEC_NAME";

    private const string AppName = "NxSheetMetalBead";

    /// <summary>NX's index for a plain, non-array attribute. Index 0 is the first element of an ARRAY attribute.</summary>
    internal const int NotAnArray = -1;

    /// <summary>Beads stamped before the index fix carry their attributes as one-element arrays at this index.</summary>
    internal const int LegacyArrayIndex = 0;

    /// <remarks>Update.Option.Later: an attribute does not change geometry, and Now made NX run a full model update
    /// for every one of the seven attributes, on a feature just committed inside the dialog's undo mark.</remarks>
    public static void Stamp(Feature feature, string standardId, string beadSpec, string specId, bool isNewFeature)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0";
        var nowUtc = DateTime.UtcNow.ToString("O");

        Set(feature, AppNameAttribute, AppName);
        Set(feature, AppVersionAttribute, version);
        Set(feature, StandardIdAttribute, standardId);
        Set(feature, BeadSpecAttribute, beadSpec);
        Set(feature, SpecIdAttribute, specId);

        if (isNewFeature)
            Set(feature, CreatedUtcAttribute, nowUtc);
        Set(feature, ModifiedUtcAttribute, nowUtc);
    }

    /// <summary>The attribute's value, whether stamped as a plain attribute or, by an older version, as a
    /// one-element array. Null when absent.</summary>
    internal static string? Read(Feature feature, string title)
    {
        foreach (var index in new[] { NotAnArray, LegacyArrayIndex })
        {
            if (feature.HasUserAttribute(title, NXObject.AttributeType.String, index))
                return feature.GetStringUserAttribute(title, index);
        }

        return null;
    }

    private static void Set(Feature feature, string title, string value)
    {
        // A bead re-stamped by this version drops the old array form, so it never carries both.
        if (feature.HasUserAttribute(title, NXObject.AttributeType.String, LegacyArrayIndex))
            feature.DeleteUserAttribute(NXObject.AttributeType.String, title, true, Update.Option.Later);

        feature.SetUserAttribute(title, NotAnArray, value, Update.Option.Later);
    }
}
