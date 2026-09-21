using NXOpen;

namespace BANxOpen.SheetMetal.NxAdapters.Beads;

/// <summary>Builds a curves-only Section over one chain the way the interactive dialog does: part modeling
/// tolerances (chaining = 0.95 × distance, as NX records it) set on the Section, not left at zero — a parameterless
/// <c>Sections.CreateSection()</c> carries zero tolerances and fails the bead builder's validation.
///
/// Used both for a bead builder that hands back no Section of its own, and to put a chain back into the bead
/// dialog's curve block, which only takes Sections, never bare curves.</summary>
public static class CurveSectionFactory
{
    /// <summary>A new Section holding <paramref name="chain"/>.</summary>
    public static Section Create(Part workPart, IReadOnlyList<NXObject> chain)
    {
        var curves = BaseCurvesOf(chain);
        var modeling = workPart.Preferences.Modeling;
        var distanceTolerance = modeling.DistanceToleranceData;

        var section = workPart.Sections.CreateSection(distanceTolerance * 0.95, distanceTolerance, modeling.AngleToleranceData);
        AddChain(workPart, section, chain, curves);
        return section;
    }

    /// <summary>Fills an existing, empty Section with <paramref name="chain"/>, setting the part's tolerances on it.</summary>
    public static void Fill(Part workPart, Section section, IReadOnlyList<NXObject> chain)
    {
        var curves = BaseCurvesOf(chain);
        var distanceTolerance = workPart.Preferences.Modeling.DistanceToleranceData;
        section.DistanceTolerance = distanceTolerance;
        section.ChainingTolerance = distanceTolerance * 0.95;

        AddChain(workPart, section, chain, curves);
    }

    private static void AddChain(Part workPart, Section section, IReadOnlyList<NXObject> chain, IBaseCurve[] curves)
    {
        section.SetAllowedEntityTypes(Section.AllowTypes.OnlyCurves);

        var rule = workPart.ScRuleFactory.CreateRuleBaseCurveDumb(curves);
        section.AddToSection(new SelectionIntentRule[] { rule }, chain[0], null, null, new Point3d(0, 0, 0), Section.Mode.Create);
    }

    private static IBaseCurve[] BaseCurvesOf(IReadOnlyList<NXObject> chain)
    {
        if (chain.Count == 0)
            throw new ArgumentException("A bead needs at least one curve.", nameof(chain));

        var curves = new IBaseCurve[chain.Count];
        for (var i = 0; i < chain.Count; i++)
        {
            if (chain[i] is not IBaseCurve baseCurve)
                throw new ArgumentException($"Selected object is not a curve or edge: {chain[i]?.GetType().Name ?? "null"}", nameof(chain));
            curves[i] = baseCurve;
        }

        return curves;
    }
}
