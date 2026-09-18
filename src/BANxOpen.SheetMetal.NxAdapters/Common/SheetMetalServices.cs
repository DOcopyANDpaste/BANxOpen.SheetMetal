using BANxOpen.Foundation.NxAdapters;
using BANxOpen.Foundation.NxAdapters.Materials;
using BANxOpen.SheetMetal.Beads;
using BANxOpen.SheetMetal.Common;
using BANxOpen.SheetMetal.Materials;
using BANxOpen.SheetMetal.NxAdapters.Beads;
using BANxOpen.SheetMetal.NxAdapters.Materials;
using BANxOpen.SheetMetal.SpecData;

namespace BANxOpen.SheetMetal.NxAdapters.Common;

/// <summary>The sheet metal pieces more than one entry point needs, built once from the shared config.
///
/// Both the bead dialog and the Material Assignment dialog enforce bead SPEC restrictions and keep the part's
/// Sheet Metal Preferences in step with the assigned material, so both need the same sheet metal material standards
/// file, spec cache, bead settings and material rule modules. Building them here rather than in each composition
/// root is what guarantees the two dialogs read the same config files and judge a body by the same rules; two
/// hand-wired copies would drift the first time one of them changed.</summary>
public sealed class SheetMetalServices
{
    private SheetMetalServices(
        SheetMetalMaterialTable materialTable,
        BeadSpecCache specCache,
        IBeadSpecLookup specLookup,
        BeadSettings beadSettings,
        BeadTracebackService tracebackService,
        BeadGeometryReader geometryReader,
        SheetMetalPreferenceService preferenceService,
        SheetMetalStandardSelection standardSelection,
        IReadOnlyList<INxMaterialRuleModule> materialModules)
    {
        StandardSelection = standardSelection;
        MaterialTable = materialTable;
        SpecCache = specCache;
        SpecLookup = specLookup;
        BeadSettings = beadSettings;
        TracebackService = tracebackService;
        GeometryReader = geometryReader;
        PreferenceService = preferenceService;
        MaterialModules = materialModules;
    }

    /// <summary>NX's sheet metal material standards file: every material, its physical material, grade and Standard.</summary>
    public SheetMetalMaterialTable MaterialTable { get; }

    public BeadSpecCache SpecCache { get; }

    public IBeadSpecLookup SpecLookup { get; }

    public BeadSettings BeadSettings { get; }

    public BeadTracebackService TracebackService { get; }

    public BeadGeometryReader GeometryReader { get; }

    /// <summary>Reads and syncs the work part's Sheet Metal Preferences.</summary>
    public SheetMetalPreferenceService PreferenceService { get; }

    /// <summary>The Standard a dialog with a Standard picker has chosen; the row choice only offers rows of it.
    /// Left unset, every Standard's rows are offered.</summary>
    public SheetMetalStandardSelection StandardSelection { get; }

    /// <summary>The material rule modules this domain contributes — pass them to <c>MaterialEngine.Create</c>:
    /// bead SPEC restrictions (<see cref="BeadFeatureRuleModule"/>) and Sheet Metal Preferences
    /// (<see cref="SheetMetalPreferenceRuleModule"/>).</summary>
    public IReadOnlyList<INxMaterialRuleModule> MaterialModules { get; }

    /// <summary>Loads the config and builds the services.</summary>
    /// <returns>A failure carrying a user-facing message when the config directory or the sheet metal material
    /// standards file cannot be found, or either is invalid. Callers show it and stop, rather than starting with partial rules.</returns>
    public static BANxOpen.Foundation.Contracts.Common.OperationResult<SheetMetalServices> Create(NxSessionContext context)
    {
        try
        {
            var materialTable = SheetMetalMaterialTable.Load(SheetMetalConfigLocator.MaterialTablePath());
            var specSource = new FileSystemBeadSpecSource(materialTable, new ExcelBeadSpecParser());
            var specCache = new BeadSpecCache(specSource, SheetMetalConfigLocator.CacheDirectory(), context.Log.Warn);
            var specLookup = new BeadSpecLookup(specCache, context.Log.Warn);
            var beadSettings = BeadSettings.Load(SheetMetalConfigLocator.BeadSettingsPath());

            var tracebackService = new BeadTracebackService(context);
            var geometryReader = new BeadGeometryReader(context);
            var inventory = new BeadFeatureInventory(context, tracebackService, geometryReader);
            var preferenceService = new SheetMetalPreferenceService(context, materialTable);
            var standardSelection = new SheetMetalStandardSelection(materialTable);

            var materialModules = new INxMaterialRuleModule[]
            {
                new BeadFeatureRuleModule(new BeadMaterialConstraintProvider(inventory, specLookup, materialTable, beadSettings)),
                new SheetMetalPreferenceRuleModule(
                    new SheetMetalPreferenceConstraintProvider(preferenceService, materialTable),
                    new SheetMetalRowChoiceProvider(preferenceService, standardSelection),
                    new SyncSheetMetalPreferenceEffectRule(materialTable),
                    new SheetMetalPreferenceSyncExecutor(preferenceService, materialTable)),
            };

            return BANxOpen.Foundation.Contracts.Common.OperationResult<SheetMetalServices>.Success(new SheetMetalServices(
                materialTable, specCache, specLookup, beadSettings,
                tracebackService, geometryReader, preferenceService, standardSelection, materialModules));
        }
        catch (Exception ex) when (ex is DirectoryNotFoundException or FileNotFoundException or InvalidDataException
                                       or IOException or UnauthorizedAccessException)
        {
            context.Log.Error($"Sheet metal configuration could not be loaded: {ex.Message}");
            return BANxOpen.Foundation.Contracts.Common.OperationResult<SheetMetalServices>.Fail(
                "SHEETMETAL_CONFIG_UNAVAILABLE", $"Sheet metal configuration could not be loaded: {ex.Message}");
        }
    }
}
