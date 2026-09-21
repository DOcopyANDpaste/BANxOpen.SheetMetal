using System.Globalization;
using NXOpen;
using BANxOpen.Foundation.NxAdapters;

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

    public BeadExpressionSet EnsureSpecExpressions(string standardId, string specId, double depth, double radius, double dieRadius, Unit lengthUnit)
    {
        var prefix = $"Bead_{Sanitize(standardId)}_{Sanitize(specId)}";
        return new BeadExpressionSet(
            EnsureNamedExpression($"{prefix}_Depth", depth, lengthUnit),
            EnsureNamedExpression($"{prefix}_Radius", radius, lengthUnit),
            EnsureNamedExpression($"{prefix}_DieRadius", dieRadius, lengthUnit));
    }

    /// <summary>Points a Bead builder's Height/Radius/DieRadius Expression handle (builder-owned, read-only
    /// as an object reference) at one of our named expressions by formula — this is what makes the feature
    /// associative to the SPEC's expression rather than holding a copied-in literal.</summary>
    public static void BindToExpression(Expression builderOwnedExpression, Expression namedExpression) =>
        builderOwnedExpression.SetFormula(namedExpression.Name);

    /// <summary>A user expression, not a system one: NX expects a system expression to be associated with an object
    /// and deletes it with that object, so a renamed system expression shared by several beads can be deleted along
    /// with one of them while the others still reference it.</summary>
    private Expression EnsureNamedExpression(string name, double value, Unit unit)
    {
        var expressions = _context.WorkPart.Expressions;
        var formula = value.ToString(CultureInfo.InvariantCulture);

        // Looked up by name rather than FindObject-and-catch: that catch also swallowed a failed edit, and then
        // tried to create a second expression with the same name.
        var existing = expressions.Cast<Expression>().FirstOrDefault(e => e.Name == name);
        if (existing is not null)
        {
            expressions.EditExpressionWithUnits(existing, unit, formula);
            return existing;
        }

        return expressions.CreateNumberExpression($"{name}={formula}", unit);
    }

    private static string Sanitize(string value) => new(value.Where(char.IsLetterOrDigit).ToArray());
}
