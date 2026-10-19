namespace Fluxy.Core.Users.Enums
{
    /// <summary>
    /// What a one-time code turned out to be.
    /// </summary>
    /// <remarks>
    /// The verdict of a code is the same question wherever it is asked - a profile change
    /// waiting for its confirmation, a password reset waiting for its own - so it is one
    /// enumeration and not one per flow. The flows answer it differently, though: a wrong code
    /// and a code whose window has closed are two different refusals to the person who asked,
    /// and a flow that cannot tell them apart has to pick one and lose the other.
    /// </remarks>
    public enum PendingCodeStatus
    {
        /// <summary>The code does not match the one that was stored.</summary>
        Invalid = 0,

        /// <summary>The code matches, but the window it was valid in has closed.</summary>
        Expired = 1,

        /// <summary>The code matches and is still inside its window.</summary>
        Valid = 2
    }
}
