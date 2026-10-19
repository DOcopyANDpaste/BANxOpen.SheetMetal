using BANxOpen.SheetMetal.Beads.Rules;
using BANxOpen.SheetMetal.Materials;

namespace BANxOpen.SheetMetal;

/// <summary>The wording of every message the sheet metal libraries can put in front of a user — bead SPEC validation,
/// the Sheet Metal Preferences check, the material constraints sheet metal features place on a body, requirement notes,
/// and configuration and SPEC-workbook errors — as default text a tool may override (the ASP.NET Identity
/// <c>IdentityErrorDescriber</c> pattern): one virtual member per message, and a <see cref="Default"/> instance. Code
/// that produces one of these messages takes a <c>SheetMetalText?</c> and falls back to <see cref="Default"/>; the
/// codes that go with them are <see cref="SheetMetalCodes"/>. Log lines are not user text and are not here, and
/// neither is the requirement text stored in the part (<c>SheetMetalRequirementMapper</c>), which is data. See the
/// nxopen-csharp-patterns skill, common.md §8.
///
/// Thicknesses arrive already formatted (<see cref="ThicknessMatchRule.Format"/>); parameters that carry an NX error
/// are already formatted (<c>NxDialogText.NxError</c>).</summary>
public class SheetMetalText
{
    public static SheetMetalText Default { get; } = new();

    // ---- Bead SPEC validation (ThicknessMatchRule, MaterialAllowedRule) ----

    public virtual string BeadThicknessMismatch(string spec, string specThickness, string sheetThickness) =>
        $"SPEC '{spec}' is driven for {specThickness} in material, but this sheet metal is {sheetThickness} in.";

    public virtual string BeadMaterialMissing =>
        "This sheet metal has no material assigned; a material must be assigned before a SPEC can be validated.";

    public virtual string BeadGradeNotAColumn(string grade, string standard) =>
        $"Material grade '{grade}' is not available in Standard '{standard}'s workbook — " +
        "Please Contact Admin before assigning this material to the sheet metal.";

    public virtual string BeadSpecNotAllowedOnGrade(string spec, string grade) =>
        $"SPEC '{spec}' is not allowed on material grade '{grade}'.";

    /// <summary>Why an unstamped bead's SPEC is not known, as a clause ("its geometry …"); null when it is known or the
    /// bead is stamped.</summary>
    public virtual string? BeadUnidentifiedReason(BeadOnBody bead) => bead.Status switch
    {
        BeadSpecStatus.GeometryUnreadable => "its geometry could not be read",
        // Named by workbook and SPEC: two workbooks of a Standard could carry the same SpecId.
        BeadSpecStatus.Ambiguous => $"its geometry matches several SPECs ({string.Join(", ", bead.Candidates.Select(c => $"{c.WorkbookName}/{c.SpecId}"))})",
        BeadSpecStatus.Unmatched => "its geometry matches no SPEC in any Standard",
        _ => null,
    };

    // ---- Sheet Metal Preferences check (SheetMetalPreferenceCheck) ----

    public virtual string PreferencesHaveNoMaterial => "Sheet Metal Preferences have no material.";

    public virtual string PreferencesNotMaterialTable =>
        "Sheet Metal Preferences are not set to Material Table entry, so NX is not using a material from the " +
        "sheet metal material standards file.";

    public virtual string PreferenceMaterialNotInTable(string material) =>
        $"Sheet Metal Preferences have material '{material}', and it is not in the BA sheet metal material standards file.";

    public virtual string PreferenceMaterialOutOfSync(string row, string physicalMaterial, string bodyMaterial) =>
        $"Sheet Metal Preferences have material '{row}', made of '{physicalMaterial}', but this body's material is '{bodyMaterial}'.";

    /// <summary>A body with no material, against preferences that name one — out of sync for a tool that assigns it.</summary>
    public virtual string PreferenceBodyHasNoMaterial(string row, string physicalMaterial) =>
        $"Sheet Metal Preferences have material '{row}', made of '{physicalMaterial}', but this body has no material."+
        "Material will be automatically assigned to the body on applied.";

    public virtual string PreferenceThicknessMismatch(string bodyThickness, string preferenceThickness) =>
        $"This sheet metal is {bodyThickness} thick, but Sheet Metal Preferences specify {preferenceThickness}. " +
        "Correct the body's thickness or the preferences before validating a SPEC.";

    // Short forms of the same states, for a sentence that says why the preferences cannot be used.

    public virtual string PreferencesUnreadableReason => "Preferences could not be read";
    public virtual string PreferencesNotMaterialTableReason => "Preference Entry is not in Material Table";
    public virtual string PreferencesNoMaterialReason => "Preference has no material";
    public virtual string PreferencesMaterialNotInTableReason(string material) =>
        $"material '{material}' is not in the sheet metal material standards file";
    public virtual string PreferencesStandardNotListedReason(string standard) =>
        $"Standard '{standard}' is not one the standards file lists";
    public virtual string PreferencesNoRowReason => "Preference has no row in the standards file";

    // Labels for the state the preferences are in, for a summary line.

    public virtual string PreferenceStateNotMaterialTable => "Preference Entry is not in Material Table";
    public virtual string PreferenceStateNoMaterial => "Preference has no material";
    public virtual string PreferenceStateNotInTable(string material) => $"'{material}' (not in the standards file)";
    public virtual string PreferenceStateMaterial(string material) => $"'{material}'";
    public virtual string PreferenceStateUnreadable => "Preferences could not be read";

    /// <summary>The detail of an unreadable read when the body is gone.</summary>
    public virtual string PreferencesBodyGone(string bodyId) => $"body '{bodyId}' is no longer in the work part";

    public virtual string PreferencesNoThickness => "Sheet Metal Preferences have no material thickness";

    public virtual string NoPreferencesReturned => "no preferences were returned";

    // ---- Constraints on a body's material (the constraint providers; attributed to a source by the gate) ----

    public virtual string SourceBodyFeatures => "this body's features";
    public virtual string SourceStampedBead(string spec, string standard) => $"Bead SPEC {spec} (Standard {standard})";
    public virtual string SourceMatchedBead(string name, string spec, string standard) =>
        $"bead feature '{name}' (unstamped; geometry matches SPEC {spec}, Standard {standard})";
    public virtual string SourceUnidentifiedBead(string name) => $"bead feature '{name}'";
    public virtual string SourceBeadSpecCount(int count) => $"{count} bead SPECs on this body";
    public virtual string SourcePreferences => "Sheet Metal Preferences";
    public virtual string SourcePartRequirements => "this part's sheet metal requirements";

    public virtual string BeadFeaturesUnreadable(string error) =>
        $"The features on this body could not be read, so its bead SPEC restrictions cannot be checked: {error}";

    public virtual string BeadGradeUnrecognized(string material) =>
        $"Material '{material}' has no row in the sheet metal material standards file, so it cannot be " +
        "checked against the bead SPECs on this body. Add a row for it before assigning.";

    public virtual string BeadGeometryUnreadable(string name) =>
        $"Bead feature '{name}' was not created by the bead tool and its geometry could not be " +
        "read, so its SPEC material restriction is not enforced. Re-apply it with the bead dialog to " +
        "record its SPEC.";

    public virtual string BeadAmbiguous(string name, string candidates) =>
        $"Bead feature '{name}' was not created by the bead tool and its geometry matches more " +
        $"than one SPEC ({candidates}), so no SPEC material " +
        "restriction is enforced for it. Re-apply it with the bead dialog to record which SPEC it is.";

    public virtual string BeadUnmatched(string name) =>
        $"Bead feature '{name}' was not created by the bead tool and its geometry matches no SPEC " +
        "in any Standard, so no SPEC material restriction is enforced for it. Re-apply it with the bead " +
        "dialog using a valid SPEC.";

    public virtual string BeadSpecNotFound(string spec, string standard) =>
        $"SPEC '{spec}' is no longer in Standard '{standard}', so no material can be " +
        "verified against the bead built to it. Check the Standard's workbook, or re-apply the bead " +
        "with a current SPEC.";

    public virtual string BeadMaterialNotAllowed(string material, string? grade, string spec) =>
        $"Material '{material}' (grade '{grade}') is not allowed by SPEC '{spec}' at any thickness it is driven for. " +
        "Re-apply the bead with a SPEC that allows it.";

    public virtual string RequirementPairsChanged(string requirement, string workbookPairs, string recordedPairs) =>
        $"{requirement}: its workbook now allows {workbookPairs}, but the part's requirement records {recordedPairs}. " +
        "The requirement still applies; re-apply the beads with the Bead tool to bring it up to date.";

    public virtual string PreferencesUnreadableForAssignment(string error) =>
        $"This part's Sheet Metal Preferences could not be read, so they cannot be updated to match the assigned material: {error}";

    public virtual string PreferenceMaterialHasNoRow(string material, string column) =>
        $"Material '{material}' has no row in the sheet metal material standards file " +
        $"({column} = '{material}'), so Sheet Metal Preferences " +
        "cannot be updated to match it. Add a row for it before assigning.";

    public virtual string PreferencesSharedByBodies(int count) =>
        $"This part has {count} sheet metal bodies. Sheet Metal Preferences " +
        "belong to the part, so this assignment sets the preferences' material for all of them.";

    public virtual string RequirementsUnreadableForAssignment(string errors) =>
        $"This part's sheet metal requirements could not be read, so the material was not checked against them: {errors} " +
        "The assignment goes ahead; re-open the Bead tool to rewrite them.";

    public virtual string RequirementMaterialNotAllowed(string material, string requirement, string allowedPairs) =>
        $"Material '{material}' has no row {requirement} allows (allowed: {allowedPairs}). " +
        $"{requirement} will need to be re-applied; its NX requirement check reports it until then.";

    // ---- The picked Sheet Metal Preferences row (SheetMetalRowGateRule) ----

    /// <param name="standard">The chosen Standard, or null when every Standard's rows are offered.</param>
    public virtual string RowNotOffered(string row, string material, string? standard) =>
        $"'{row}' is not one of the Sheet Metal Preferences materials that can be set for '{material}' " +
        (standard is null ? "in the sheet metal material standards file" : $"under Standard '{standard}'") +
        ". Pick another row.";

    public virtual string RowRefusedRequirementsUnreadable =>
        "This part's sheet metal requirements could not be read, so this row was not checked against them. " +
        "The assignment goes ahead; re-open the Bead tool to rewrite them.";

    public virtual string RowPairNotAllowed(string row, string requirement, string grade, string rowThickness, string allowedPairs) =>
        $"'{row}' ({grade} at {rowThickness} in) is not allowed by {requirement} (allowed: {allowedPairs}). " +
        $"{requirement} will need to be re-applied; its NX requirement check reports it until then.";

    public virtual string RowNeedsReapply(string row, string requirement, string builtThickness, string rowThickness) =>
        $"'{row}' is allowed by {requirement}, but its features were built at {builtThickness} in. " +
        $"{requirement} will need to be re-applied at {rowThickness} in to rebuild their geometry.";

    public virtual string RowThicknessMismatch(string row, string rowThickness, string bodyThickness) =>
        $"'{row}' is {rowThickness} thick but this body is {bodyThickness} thick; the part's " +
        "material and geometry will disagree about thickness.";

    // ---- Requirements held in the part ----

    public virtual string RequirementKeyMalformed(string key) => $"Requirement key '{key}' is not a Standard|SPEC pair.";

    public virtual string RequirementIncomplete(string featureNoun, string spec, string standard) =>
        $"The requirement for {featureNoun} {spec} (Standard {standard}) is incomplete or was " +
        "edited by hand; re-apply its features to rewrite it.";

    /// <param name="error">Why it could not be read (<see cref="RequirementIncomplete"/>).</param>
    public virtual string RequirementRewritten(string error) =>
        $"{error} It was rewritten from its SPEC workbook.";

    public virtual string RequirementRecordedAtCurrentThickness(string featureNoun, string spec, string standard, string thickness) =>
        $"{featureNoun} {spec} (Standard {standard}) had no requirement; it was recorded at the sheet's current {thickness} in.";

    public virtual string RequirementNotRecordedNotDriven(string featureNoun, string spec, string standard, string thickness) =>
        $"{featureNoun} {spec} (Standard {standard}) has no requirement, and none could be " +
        $"recorded: the SPEC is not driven for the sheet's {thickness} in. Re-apply its beads at a thickness it covers.";

    public virtual string RequirementNotRecordedThicknessUnknown(string featureNoun, string spec, string standard) =>
        $"{featureNoun} {spec} (Standard {standard}) has no requirement, and none could be " +
        "recorded: the sheet's thickness is unknown.";

    public virtual string ThicknessExpressionMissing(string expression) =>
        $"This part has no '{expression}' expression, so a bead requirement " +
        "has nothing to check the thickness against. Set the Sheet Metal Preferences to a Material Table entry first.";

    public virtual string BeadsUnreadableForRequirements(string error) =>
        $"The beads on this body could not be read, so their requirements were not updated: {error}";

    public virtual string RequirementsUnreadableForRefresh(string errors) =>
        $"This part's requirements could not be read, so they were not updated: {errors}";

    public virtual string GradeExpressionFailed(string expression, string nxDetail) =>
        $"The '{expression}' expression could not be written: {nxDetail}";

    // ---- Part profile and preference service ----

    public virtual string ProfileNotSheetMetal(string body) => $"'{body}' is not a sheet metal body.";
    public virtual string ProfileNotCheckable(string body) => $"'{body}' could not be checked as sheet metal.";
    public virtual string ThicknessUnreadable => "Could not read this sheet metal's thickness.";
    public virtual string ProfilePreferencesUnreadable(string error) => $"Could not read this part's Sheet Metal Preferences: {error}.";

    public virtual string PreferencesSetFailed(string material, string detail) =>
        $"Sheet Metal Preferences could not be set to '{material}': {detail}";

    public virtual string PreferencesNotApplied(string material) =>
        $"Sheet Metal Preferences were updated, but NX does not show material '{material}' with Material Table entry afterwards.";

    public virtual string PreferencesReadBackFailed(string detail) => $"Sheet Metal Preferences could not be read back: {detail}";

    public virtual string PreferenceRowNotInTable(string material) =>
        $"Material '{material}' is not in the sheet metal material standards file, so Sheet Metal Preferences cannot be set to it.";

    // ---- Configuration (shown at start-up) ----

    public virtual string ConfigUnavailable(string detail) => $"Sheet metal configuration could not be loaded: {detail}";

    public virtual string ConfigDirectoryNotFound(string markerFile, IEnumerable<string> attempted, string environmentVariable) =>
        $"Could not locate the sheet metal config directory (a folder containing {markerFile}). " +
        $"Tried: {string.Join("; ", attempted)}. Set {environmentVariable} to that folder.";

    public virtual string MaterialTablePathNotSet(string settingsPath) =>
        $"materialTablePath is not set in {settingsPath}. Set it to the sheet metal material standards file.";

    public virtual string MaterialTableFileMissing(string settingsPath, string path) =>
        $"The sheet metal material standards file set in {settingsPath} (materialTablePath) does not exist: {path}";

    public virtual string SettingsInvalidJson(string path, string detail) => $"Sheet metal settings at '{path}' are not valid JSON: {detail}";
    public virtual string SettingsEmpty(string path) => $"Sheet metal settings at '{path}' are empty.";

    public virtual string BeadSettingsInvalidJson(string path, string detail) => $"Bead settings at '{path}' are not valid JSON: {detail}";
    public virtual string BeadSettingsEmpty(string path) => $"Bead settings at '{path}' are empty.";
    public virtual string BeadSettingsInvalid(string path, string detail) => $"Bead settings at '{path}': {detail}";
    public virtual string BeadToleranceNotPositive => "geometryMatchTolerance must be greater than zero.";

    public virtual string MappingFeatureUnknown(string? feature, IEnumerable<string> known) =>
        $"parameterMapping feature '{feature}' is not a bead parameter. Known: {string.Join(", ", known)}.";

    public virtual string MappingColumnUnknown(string column, string feature, IEnumerable<string> known) =>
        $"parameterMapping column '{column}' for {feature} is not a SPEC workbook column. Known: {string.Join(", ", known)}.";

    public virtual string MappingDuplicated(IEnumerable<string> features) =>
        $"parameterMapping lists {string.Join(", ", features)} more than once.";

    public virtual string MappingMissing(IEnumerable<string> parameters) =>
        $"parameterMapping must map {string.Join(", ", parameters)}: the tool writes it into every bead.";

    public virtual string MaterialTableNotFound(string path) => $"Sheet metal material standards file not found: {path}";
    public virtual string MaterialTableInvalid(string path, string detail) => $"Sheet metal material standards file '{path}': {detail}";

    // The standards file's own format errors (the line is named; the file is named by MaterialTableInvalid).

    public virtual string TableNoBlock(string tableStart) => $"no {tableStart} block was found.";
    public virtual string TableNoUnits(string unitsKeyword, string tableStart, int line, string supportedUnits) =>
        $"no {unitsKeyword} line was found before {tableStart} (line {line}). Add '{unitsKeyword},{supportedUnits}'.";
    public virtual string TableUnitsUnsupported(string unitsKeyword, string units, string supportedUnits) =>
        $"{unitsKeyword} is '{units}', but only {supportedUnits} is supported: bead SPECs and thickness checks are in inches.";
    public virtual string TableRowWidth(int line, int values, int headerLine, int columns) =>
        $"line {line} has {values} values but the header row (line {headerLine}) has {columns} columns.";
    public virtual string TableDuplicateName(int line, string name, int firstLine, string uniqueColumn) =>
        $"line {line}: '{name}' is already used on line {firstLine}. Values in the unique column '{uniqueColumn}' must not repeat.";
    public virtual string TableNoEnd(string tableStart, string tableEnd) => $"the {tableStart} block has no {tableEnd} line.";
    public virtual string TableHeaderEmpty(int line) => $"line {line}: the header row is empty.";
    public virtual string TableColumnUnnamed(int line, int column) => $"line {line}: column {column} has no name.";
    public virtual string TableColumnRepeated(int line, string column) => $"line {line}: column '{column}' appears more than once.";
    public virtual string TablePropertyRowWidth(int line, int values, int columns) =>
        $"line {line}: the property row has {values} values but the header row has {columns} columns.";
    public virtual string TablePropertyUnknown(int line, string column, string property, IEnumerable<string> known) =>
        $"line {line}: column '{column}' has property '{property}'. Expected one of: {string.Join(", ", known)}.";
    public virtual string TableUniqueCount(int line, string uniqueProperty, int count) =>
        $"line {line}: exactly one column must be '{uniqueProperty}', but {count} are.";
    public virtual string TableColumnsMissing(int line, IEnumerable<string> missing) =>
        $"line {line}: required column(s) missing: {string.Join(", ", missing)}.";
    public virtual string TableUniqueEmpty(int line, string uniqueColumn) => $"line {line}: the unique column '{uniqueColumn}' is empty.";
    public virtual string TableValueEmpty(int line, string name, string column) => $"line {line} ('{name}'): {column} is empty.";
    public virtual string TableThicknessInvalid(int line, string name, string column, string text) =>
        $"line {line} ('{name}'): {column} '{text}' is not a positive number.";
    public virtual string TableEndsBefore(string tableStart, string expected) => $"the {tableStart} block ends before {expected}.";
    public virtual string TableExpectedHeaderRow => "the header row";
    public virtual string TableExpectedPropertyRow => "the column property row (unique/show/hide/attribute)";

    // ---- SPEC workbooks and Standard folders ----

    public virtual string StandardsNotFromFile =>
        "The sheet metal material table was not read from a file, so its Standard folders cannot be found.";

    public virtual string StandardNotAFolderName(string standard) => $"Standard '{standard}' cannot be used as a folder name.";

    public virtual string BeadSpecMatchesSeveralWorkbooks(string spec, int count, string folder, IEnumerable<string> files) =>
        $"Bead SPEC '{spec}' matches {count} workbooks in '{folder}' " +
        $"({string.Join(", ", files)}). Exactly one file name may contain that SPEC name.";

    public virtual string WorkbookHeaderRowEmpty => "SPEC workbook header row has no columns.";
    public virtual string WorkbookUnreadable(string detail) => $"Not a readable .xlsx workbook: {detail}";
    public virtual string WorkbookNoWorkbookPart => "The workbook has no workbook part.";
    public virtual string WorkbookNoWorksheets => "The workbook has no worksheets.";
    public virtual string WorkbookCellNotANumber(string cell, string text) => $"SPEC workbook cell {cell} should be a number but reads '{text}'.";
    public virtual string WorkbookNoHeaderRow => "Could not find the header row (a cell reading 'SPEC') in the workbook.";
    public virtual string WorkbookColumnMissing(string column, IEnumerable<string> aliases) =>
        $"SPEC workbook is missing the '{column}' column (looked for header text matching: {string.Join(", ", aliases)}).";
}
