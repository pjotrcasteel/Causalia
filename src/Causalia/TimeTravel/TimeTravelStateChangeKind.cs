namespace Causalia.TimeTravel;

/// <summary>
/// Describes how one watched state value changed between two checkpoints.
/// </summary>
public enum TimeTravelStateChangeKind
{
    /// <summary>
    /// The probe did not exist in the earlier checkpoint.
    /// </summary>
    Added = 0,

    /// <summary>
    /// The probe no longer exists in the later checkpoint.
    /// </summary>
    Removed = 1,

    /// <summary>
    /// The probe value or capture error changed.
    /// </summary>
    Changed = 2
}
