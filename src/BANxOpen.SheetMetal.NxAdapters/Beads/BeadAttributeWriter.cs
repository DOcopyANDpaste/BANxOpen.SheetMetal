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

    private const string AppName = "NxSheetMetalBead";

    public static void Stamp(Feature feature, string standardId, string specId, bool isNewFeature)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0";
        var nowUtc = DateTime.UtcNow.ToString("O");

        feature.SetUserAttribute(AppNameAttribute, 0, AppName, Update.Option.Now);
        feature.SetUserAttribute(AppVersionAttribute, 0, version, Update.Option.Now);
        feature.SetUserAttribute(StandardIdAttribute, 0, standardId, Update.Option.Now);
        feature.SetUserAttribute(SpecIdAttribute, 0, specId, Update.Option.Now);

        if (isNewFeature)
            feature.SetUserAttribute(CreatedUtcAttribute, 0, nowUtc, Update.Option.Now);
        feature.SetUserAttribute(ModifiedUtcAttribute, 0, nowUtc, Update.Option.Now);
    }
}
