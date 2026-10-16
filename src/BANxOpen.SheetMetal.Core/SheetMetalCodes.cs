namespace BANxOpen.SheetMetal;

/// <summary>The stable codes the sheet metal libraries put on their rule outcomes (<c>RuleOutcome.ReasonCode</c>) and
/// failed operations (<c>OperationResult.ErrorCode</c>). Codes, not wording, are the contract: tools branch on these and
/// show the message as-is. The wording is <see cref="SheetMetalText"/>'s. A part's unreadable requirements use
/// Foundation's <c>FoundationCodes.RequirementsUnreadable</c>, and a failed write <c>FoundationCodes.RequirementWriteFailed</c>.</summary>
public static class SheetMetalCodes
{
    // ---- Bead SPEC validation ----

    /// <summary>Args: spec, specThickness, sheetThickness.</summary>
    public const string ThicknessMismatch = "THICKNESS_MISMATCH";

    public const string MaterialMissing = "MATERIAL_MISSING";

    /// <summary>A material grade the SPEC workbooks do not know (bead validation: args grade, standard), or a material
    /// with no row in the standards file to give it a grade (the bead constraint: args material, source).</summary>
    public const string MaterialGradeUnrecognized = "MATERIAL_GRADE_UNRECOGNIZED";

    /// <summary>Args: spec, grade.</summary>
    public const string MaterialNotAllowed = "MATERIAL_NOT_ALLOWED";

    // ---- Constraints on a body's material ----

    /// <summary>Args: spec, grade, material, source.</summary>
    public const string MaterialNotAllowedByBead = "MATERIAL_NOT_ALLOWED_BY_BEAD";

    /// <summary>Args: spec, standard, material, source.</summary>
    public const string BeadSpecNotFound = "BEAD_SPEC_NOT_FOUND";

    /// <summary>Args: feature, status (geometryUnreadable | ambiguous | noMatch), candidates, material, source.</summary>
    public const string BeadSpecUnmatched = "BEAD_SPEC_UNMATCHED";

    /// <summary>The beads on a body could not be read — by the bead constraint (args error, material, source) or when
    /// refreshing requirements.</summary>
    public const string BeadFeaturesUnreadable = "BEAD_FEATURES_UNREADABLE";

    /// <summary>Args: requirement, builtThickness, workbookPairs, recordedPairs, material, source.</summary>
    public const string RequirementOutOfDate = "REQUIREMENT_OUT_OF_DATE";

    /// <summary>Args: error, material, source.</summary>
    public const string PreferencesUnreadable = "SHEETMETAL_PREFERENCES_UNREADABLE";

    /// <summary>A material with no row in the standards file, so the preferences cannot be set to it.</summary>
    public const string PreferenceMaterialNotInTable = "PREFERENCE_MATERIAL_NOT_IN_TABLE";

    /// <summary>Args: count, material, source.</summary>
    public const string PreferenceSharedByBodies = "PREFERENCE_SHARED_BY_BODIES";

    /// <summary>A Warn. Args: reason (thicknessNotAllowed | gradeNotAllowed), builtThickness, grades, allowedPairs, material, source.</summary>
    public const string MaterialNotAllowedByRequirement = "MATERIAL_NOT_ALLOWED_BY_REQUIREMENT";

    // ---- The picked Sheet Metal Preferences row ----

    /// <summary>Args: row, material, standard (empty when no Standard is chosen).</summary>
    public const string RowNotOffered = "SHEETMETAL_ROW_NOT_OFFERED";

    /// <summary>Args: row, rowThickness, refusal (requirementsUnreadable — a Block | pairNotAllowed — a Warn), and for
    /// pairNotAllowed requirement, grade, builtThickness, allowedPairs.</summary>
    public const string RowRefusedByRequirement = "SHEETMETAL_ROW_REFUSED_BY_REQUIREMENT";

    /// <summary>A Warn: the row is allowed, but the features were built at another thickness. Args: row, rowThickness,
    /// requirement, builtThickness.</summary>
    public const string RowNeedsReapply = "SHEETMETAL_ROW_NEEDS_REAPPLY";

    /// <summary>Args: row, rowThickness, bodyThickness.</summary>
    public const string RowThicknessMismatch = "SHEETMETAL_ROW_THICKNESS_MISMATCH";

    // ---- Services ----

    public const string ConfigUnavailable = "SHEETMETAL_CONFIG_UNAVAILABLE";
    public const string NotSheetMetal = "NOT_SHEETMETAL";
    public const string ThicknessReadFailed = "THICKNESS_READ_FAILED";
    public const string PreferenceSyncNotApplied = "PREFERENCE_SYNC_NOT_APPLIED";
    public const string ThicknessExpressionMissing = "THICKNESS_EXPRESSION_MISSING";
    public const string GradeExpressionFailed = "GRADE_EXPRESSION_FAILED";
    public const string GradeAttributeFailed = "GRADE_ATTRIBUTE_FAILED";
    public const string RequirementRefreshFailed = "REQUIREMENT_REFRESH_FAILED";
    public const string BeadExpressionsFailed = "BEAD_EXPRESSIONS_FAILED";
    public const string BeadValidationFailed = "BEAD_VALIDATION_FAILED";
    public const string BeadCreateFailed = "BEAD_CREATE_FAILED";
    public const string BeadBuilderCreateFailed = "BEAD_BUILDER_CREATE_FAILED";
    public const string BeadPreviewFailed = "BEAD_PREVIEW_FAILED";
}
