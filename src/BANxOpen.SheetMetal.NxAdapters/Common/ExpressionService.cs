using System.Globalization;
using NXOpen;
using BANxOpen.Foundation.NxAdapters;
using BANxOpen.Foundation.NxAdapters.Requirements;

namespace BANxOpen.SheetMetal.NxAdapters.Common;

public sealed record BeadExpressionSet(Expression Depth, Expression Radius, Expression DieRadius);

/// <summary>Creates/reuses one named NX Expression per SPEC-driven value (Depth/Radius/DieRadius) and
/// points a Bead builder's own (read-only, builder-owned) Height/Radius/DieRadius Expression handle at it
/// via <c>Expression.SetFormula</c> — the feature ends up driven by a named expression carrying the SPEC's
/// value, not a literal double, per the requirement. One expression set is shared across every curve's
/// Bead feature created from the same SPEC in the same Apply, since the values are identical.</summary>
public sealed class ExpressionService
{
    private readonly NxSessionContext _context;

    public ExpressionService(NxSessionContext context) => _context = context;

    /// <summary>Named <c>Bead_&lt;SPEC&gt;_Depth/_Radius/_DieRadius</c>, e.g. <c>Bead_B1005010_3_Depth</c>. Expressions an
    /// earlier version named <c>Bead_&lt;Standard&gt;_&lt;SPEC&gt;_*</c> are renamed in place, so the beads bound to them
    /// keep their link.</summary>
    public BeadExpressionSet EnsureSpecExpressions(string standardId, string specId, double depth, double radius, double dieRadius, Unit lengthUnit)
    {
        var prefix = $"Bead_{Token(specId)}";
        var legacyPrefix = $"Bead_{Sanitize(standardId)}_{Sanitize(specId)}";
        return new BeadExpressionSet(
            EnsureNamedExpression($"{prefix}_Depth", $"{legacyPrefix}_Depth", depth, lengthUnit),
            EnsureNamedExpression($"{prefix}_Radius", $"{legacyPrefix}_Radius", radius, lengthUnit),
            EnsureNamedExpression($"{prefix}_DieRadius", $"{legacyPrefix}_DieRadius", dieRadius, lengthUnit));
    }

    /// <summary>Points a Bead builder's Height/Radius/DieRadius Expression handle (builder-owned, read-only
    /// as an object reference) at one of our named expressions by formula — this is what makes the feature
    /// associative to the SPEC's expression rather than holding a copied-in literal.</summary>
    public static void BindToExpression(Expression builderOwnedExpression, Expression namedExpression) =>
        builderOwnedExpression.SetFormula(namedExpression.Name);

    /// <summary>A user expression, not a system one: NX expects a system expression to be associated with an object
    /// and deletes it with that object, so a renamed system expression shared by several beads can be deleted along
    /// with one of them while the others still reference it.
    ///
    /// Locked against hand edits (<see cref="Expression.IsNoEdit"/>): a SPEC value typed over in the Expressions dialog
    /// would silently put every bead on the SPEC off-spec. Unlocked only for this tool's own edit.</summary>
    private Expression EnsureNamedExpression(string name, string legacyName, double value, Unit unit)
    {
        var expressions = _context.WorkPart.Expressions;
        var formula = value.ToString(CultureInfo.InvariantCulture);

        // Looked up by name rather than FindObject-and-catch: that catch also swallowed a failed edit, and then
        // tried to create a second expression with the same name.
        var all = expressions.Cast<Expression>().ToList();
        var existing = all.FirstOrDefault(e => e.Name == name);
        if (existing is null && all.FirstOrDefault(e => e.Name == legacyName) is { } legacy)
        {
            // NX carries the rename into every formula that references it, so the beads stay bound.
            using (NxExpressionLock.Unlocked(legacy))
                expressions.Rename(legacy, name);
            _context.Log.Debug($"Expression '{legacyName}' renamed to '{name}'.");
            existing = legacy;
        }

        if (existing is not null)
        {
            using (NxExpressionLock.Unlocked(existing))
                expressions.EditExpressionWithUnits(existing, unit, formula);

            // Also locks one created before locking was introduced.
            NxExpressionLock.Lock(existing);
            return existing;
        }

        var created = expressions.CreateNumberExpression($"{name}={formula}", unit);
        NxExpressionLock.Lock(created);
        return created;
    }

    /// <summary>The naming before the Standard was dropped: punctuation removed.</summary>
    private static string Sanitize(string value) => new(value.Where(char.IsLetterOrDigit).ToArray());

    /// <summary>Punctuation becomes '_', so <c>B1005010-3</c> and <c>B10050103</c> stay distinct.</summary>
    private static string Token(string value) => new(value.Select(c => char.IsLetterOrDigit(c) && c < 128 ? c : '_').ToArray());
}
