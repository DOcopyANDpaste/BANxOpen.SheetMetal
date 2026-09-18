using System.Globalization;
using BANxOpen.Foundation.Contracts.Bodies;
using BANxOpen.Foundation.Core.Materials.Assignment;
using BANxOpen.Foundation.Core.Materials.Assignment.Choices;
using BANxOpen.SheetMetal.Common;

namespace BANxOpen.SheetMetal.Materials;

/// <summary>Asks which row of the sheet metal material standards file the part's Sheet Metal Preferences should be
/// set to, when the material being assigned is made in more than one of them.
///
/// A physical material is normally several rows — the same material at different thicknesses, in different grades,
/// across Standards — and nothing about the assignment says which one the part is. Phase 1 took the first in file
/// order; this puts the list to the user instead, and <see cref="SyncSheetMetalPreferenceEffectRule"/> sets what
/// comes back.
///
/// The question is asked once per part, not once per body: NX keeps one set of Sheet Metal Preferences per part,
/// so every sheet metal body in the batch shares the answer. That is what the constant
/// <see cref="PartGroupKey"/> expresses. A part with several sheet metal bodies is a case the tool warns about
/// separately (<see cref="SheetMetalPreferenceConstraintProvider.SharedByBodiesCode"/>) and does not refuse.
///
/// Only rows of the Standard the dialog chose (<see cref="SheetMetalStandardSelection"/>) are considered; with no
/// Standard chosen, every Standard's are.
///
/// The user is not asked when there is nothing to decide:
/// <list type="bullet">
/// <item>The preferences already name one of those rows, made of this material and matching the body's thickness →
/// that row is kept. Re-applying a material to a part already set up for it should not re-open a question the user
/// has answered, and keeping the row also keeps the bend data the user may have chosen it for.</item>
/// <item>There is one row and it matches the body's thickness → it is taken, and the information window says
/// which.</item>
/// </list>
/// A thickness NX will not report counts as matching, since there is nothing to disagree with.
///
/// Every row is offered, whatever its thickness. Rows matching the body's own thickness are marked
/// <see cref="AssignmentChoiceOption.IsPreferred"/> so the UI can show those first, and the rest carry a
/// confirmation prompt rather than being hidden: a thickness that disagrees with the body is usually a mistake,
/// but it is the user's to make — <see cref="SheetMetalPreferenceCheck"/> reports the mismatch afterwards either
/// way.</summary>
public sealed class SheetMetalRowChoiceProvider : IAssignmentChoiceProvider
{
    public const string ChoiceIdentifier = "SHEETMETAL.PREFERENCE_ROW";

    /// <summary>One question per part. The provider is only ever asked about bodies of the work part, so a
    /// constant is the whole of "these bodies share a set of preferences".</summary>
    private const string PartGroupKey = "";

    private static readonly IReadOnlyList<AssignmentChoiceColumn> TableColumns = new[]
    {
        new AssignmentChoiceColumn(SheetMetalMaterialColumns.Standard),
        new AssignmentChoiceColumn("Grade"),
        new AssignmentChoiceColumn("Thickness", IsNumeric: true),
        new AssignmentChoiceColumn("Bend radius"),
    };

    private readonly ISheetMetalPreferenceReader _reader;
    private readonly SheetMetalStandardSelection _selection;

    public SheetMetalRowChoiceProvider(ISheetMetalPreferenceReader reader, SheetMetalStandardSelection selection)
    {
        _reader = reader;
        _selection = selection;
    }

    public string ChoiceId => ChoiceIdentifier;

    public AssignmentChoice? ChoiceFor(MaterialAssignmentRuleContext context)
    {
        if (context.TargetBody.Kind != BodyKind.SheetMetal)
            return null;

        var materialName = context.RequestedMaterial.Name;
        var rows = _selection.RowsFor(materialName);
        if (rows.Count == 0)
        {
            // Nothing to set the preferences to. SheetMetalPreferenceConstraintProvider blocks a material with no
            // row at all, and a dialog with a Standard picker refuses one with no row under the chosen Standard, so
            // this is only reachable if those gates were removed.
            return null;
        }

        var read = _reader.ReadFor(context.TargetBody.Id);
        if (!read.IsSheetMetalBody || read.Preference is not { } preference)
        {
            // Unreadable preferences block the assignment for the same reason: there is nothing to sync
            // against. Asking the user to pick a row for an assignment that will be refused is worse than
            // silence.
            return null;
        }

        var bodyThickness = read.BodyThickness;
        var options = rows.Select(row => ToOption(row, bodyThickness)).ToList();
        bool MatchesBody(SheetMetalMaterialRow row) =>
            bodyThickness is not { } measured || SheetMetalPreferenceCheck.ThicknessMatches(measured, row.Thickness);

        // The preferences are already set to a row of this material, in this Standard, at this body's thickness —
        // nothing to decide.
        if (preference.Row is { } current
            && string.Equals(current.PhysicalMaterialName, materialName, StringComparison.OrdinalIgnoreCase)
            && options.Any(o => string.Equals(o.OptionId, current.Name, StringComparison.Ordinal))
            && MatchesBody(current))
        {
            return Build(context, options, current.Name, new AssignmentChoiceAutoSelection(
                current.Name,
                $"This part's Sheet Metal Preferences are already set to '{current.Name}' " +
                $"({DescribeRow(current)}), which is made of '{materialName}'. It has been kept."));
        }

        // One row that fits needs no question. One that disagrees with the body's thickness is still put to the
        // user, so the option's thickness confirmation is seen rather than skipped.
        if (rows.Count == 1 && MatchesBody(rows[0]))
        {
            return Build(context, options, rows[0].Name, new AssignmentChoiceAutoSelection(
                rows[0].Name,
                $"'{materialName}' has one row {Scope()}. This part's Sheet Metal " +
                $"Preferences have been set to '{rows[0].Name}' ({DescribeRow(rows[0])})."));
        }

        var thicknessNote = bodyThickness is { } bodyValue
            ? $" This body is {Format(bodyValue)} thick."
            : " NX would not report this body's thickness, so no row is marked as matching it.";

        return Build(
            context,
            options,
            preselectedOptionId: null,
            auto: null,
            prompt:
                $"'{materialName}' has {rows.Count} row(s) {Scope()}. " +
                $"Choose the one this part's Sheet Metal Preferences should be set to.{thicknessNote}");
    }

    private string Scope() => _selection.Selected is { } standard
        ? $"under Standard '{standard}' in the sheet metal material standards file"
        : "in the sheet metal material standards file";

    private AssignmentChoice Build(
        MaterialAssignmentRuleContext context,
        IReadOnlyList<AssignmentChoiceOption> options,
        string? preselectedOptionId,
        AssignmentChoiceAutoSelection? auto,
        string? prompt = null) =>
        new(
            ChoiceIdentifier,
            context.TargetBody.Id,
            PartGroupKey,
            "Sheet Metal Material",
            prompt ?? string.Empty,
            TableColumns,
            options,
            preselectedOptionId,
            // Only offered when the filter can actually hide something: a list where every row matches the body
            // would present a checkbox that does nothing.
            options.Any(o => !o.IsPreferred) ? "Only rows matching this body's thickness" : null,
            auto);

    private static AssignmentChoiceOption ToOption(SheetMetalMaterialRow row, double? bodyThickness)
    {
        var matchesThickness = bodyThickness is { } measured
                               && SheetMetalPreferenceCheck.ThicknessMatches(measured, row.Thickness);

        // Only worth confirming when there is a body thickness to disagree with. A thickness NX would not report
        // marks no row as preferred, and prompting on every row in that case would be noise.
        string? confirmation = null;
        if (!matchesThickness && bodyThickness is { } bodyValue)
        {
            confirmation =
                $"'{row.Name}' is {Format(row.Thickness)} thick, but this body is {Format(bodyValue)} thick." +
                $"{Environment.NewLine}{Environment.NewLine}" +
                "Setting the Sheet Metal Preferences to it leaves the part's material and its geometry " +
                "disagreeing about thickness, which will be reported whenever this body is checked." +
                $"{Environment.NewLine}{Environment.NewLine}" +
                $"Use '{row.Name}' anyway?";
        }

        return new AssignmentChoiceOption(
            row.Name,
            new[] { row.Standard, row.Grade, Format(row.Thickness), row.BendRadius },
            matchesThickness,
            confirmation);
    }

    private static string DescribeRow(SheetMetalMaterialRow row) =>
        $"{row.Standard}, grade {row.Grade}, {Format(row.Thickness)} thick";

    /// <summary>Four decimals, matching how <see cref="SheetMetalPreferenceCheck"/> writes a thickness into its
    /// own messages, so the same number does not appear two ways in one session.</summary>
    private static string Format(double thickness) => thickness.ToString("0.####", CultureInfo.CurrentCulture);
}
