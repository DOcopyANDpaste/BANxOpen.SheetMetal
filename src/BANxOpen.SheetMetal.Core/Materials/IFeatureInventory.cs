using BANxOpen.Foundation.Contracts.Common;
using BANxOpen.SheetMetal.Beads;

namespace BANxOpen.SheetMetal.Materials;

/// <summary>One feature found on a body, identified by the Standard and SPEC it was built to.</summary>
/// <param name="FeatureKey">The feature's identity (its NX tag), so a caller can tell which bead this is.</param>
/// <param name="Name">Feature name, for messages.</param>
public sealed record FeatureSpecStamp(string StandardId, string SpecId, string FeatureKey = "", string Name = "");

/// <summary>A bead on the body that carries no SPEC stamp — usually one built by hand in NX.</summary>
/// <param name="Name">Feature name, for messages.</param>
/// <param name="Geometry">Its measured shape, used to identify which SPEC it matches. Null when it could
/// not be read.</param>
/// <param name="FeatureKey">As <see cref="FeatureSpecStamp.FeatureKey"/>.</param>
public sealed record UnstampedFeature(string Name, BeadGeometry? Geometry, string FeatureKey = "");

/// <summary>What sheet metal features a body carries, as far as the tool can tell.
///
/// <see cref="UnstampedFeatures"/> is kept separate rather than dropped because it is not the same as "no
/// features": a bead is present whose SPEC was never recorded. The provider identifies it from its geometry
/// where it can, and warns where it cannot.</summary>
/// <param name="ReadError">Set when the body's features could not be read at all. Distinct from an empty
/// inventory: "could not look" must not be mistaken for "nothing there", or every restriction on the body
/// silently lifts.</param>
public sealed record BodyFeatureInventory(
    IReadOnlyList<FeatureSpecStamp> Stamps,
    IReadOnlyList<UnstampedFeature> UnstampedFeatures,
    string? ReadError = null)
{
    public static readonly BodyFeatureInventory Empty =
        new(Array.Empty<FeatureSpecStamp>(), Array.Empty<UnstampedFeature>());

    public static BodyFeatureInventory Unreadable(string error) =>
        new(Array.Empty<FeatureSpecStamp>(), Array.Empty<UnstampedFeature>(), error);

    public bool IsEmpty => ReadError is null && Stamps.Count == 0 && UnstampedFeatures.Count == 0;
}

/// <summary>What sheet metal features are already on a body. The NX implementation reads the provenance
/// attributes the tool stamped when it created them, and measures beads that have none; keeping that behind
/// a seam is what lets <see cref="BeadMaterialConstraintProvider"/> stay pure and testable without NX.
///
/// Named for the domain rather than for beads: a louver or joggle inventory implements the same shape.</summary>
public interface IFeatureInventory
{
    BodyFeatureInventory Read(BodyId bodyId);
}
