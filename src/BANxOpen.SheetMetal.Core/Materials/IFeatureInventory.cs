using BANxOpen.Foundation.Contracts.Common;

namespace BANxOpen.SheetMetal.Materials;

/// <summary>One feature found on a body, identified by the Standard and SPEC it was built to.</summary>
public sealed record FeatureSpecStamp(string StandardId, string SpecId);

/// <summary>What sheet metal features a body carries, as far as the tool can tell.
///
/// <see cref="UnstampedFeatures"/> is kept separate rather than dropped because it is not the same as "no
/// features": it means a feature is present whose SPEC is unknown — typically one built by hand in NX rather
/// than by the tool. A constraint provider that ignored these would fail open on exactly the bodies it
/// cannot reason about.</summary>
public sealed record BodyFeatureInventory(
    IReadOnlyList<FeatureSpecStamp> Stamps,
    IReadOnlyList<string> UnstampedFeatures)
{
    public static readonly BodyFeatureInventory Empty =
        new(Array.Empty<FeatureSpecStamp>(), Array.Empty<string>());

    public bool IsEmpty => Stamps.Count == 0 && UnstampedFeatures.Count == 0;
}

/// <summary>What sheet metal features are already on a body. The NX implementation reads the provenance
/// attributes the tool stamped when it created them; keeping that behind a seam is what lets
/// <see cref="BeadMaterialConstraintProvider"/> stay pure and testable without an NX session.
///
/// Named for the domain rather than for beads: a louver or joggle inventory implements the same shape,
/// and the provider that consumes it does not care which feature kind produced a stamp.</summary>
public interface IFeatureInventory
{
    BodyFeatureInventory Read(BodyId bodyId);
}
