namespace Scada.Core.Tags;

/// <summary>
/// Public selector applied to an authoritative TAG value or physical source value.
/// Friendly authoring syntax such as Plant.Status.03 is presentation only; persisted
/// identity is the stable TAG/source identity plus this structured selector.
/// </summary>
public enum TagValueSelectorKind
{
    Bit
}

/// <summary>
/// Structured, driver-independent selector. Bit numbering is zero-based and uses
/// the fixed-width binary representation defined by the referenced value type.
/// Driver-specific width/range restrictions are validated at the driver boundary.
/// </summary>
public sealed record TagValueSelector(
    TagValueSelectorKind Kind,
    int Index);

/// <summary>
/// Stable reference to a TAG value. TAG identity is always the canonical Guid;
/// paths/names belong to authoring and diagnostics and must not become identity.
/// </summary>
public sealed record TagValueReference(
    Guid TagId,
    TagValueSelector? Selector = null);
