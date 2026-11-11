namespace Fluxy.Core.Subscriptions.Enums
{
    /// <summary>
    /// The operation a MAG event asks a device to perform.
    /// </summary>
    /// <remarks>
    /// The numbers are schema: they are already written into rows as <c>smallint</c>, so a member
    /// must never be renumbered or inserted in the middle. The names are the wire vocabulary the
    /// filter accepts and the page answers, matched case-insensitively by
    /// <c>MagEventQueryText</c>.
    /// </remarks>
    public enum MagEventType
    {
        /// <summary>Ask the device to open a channel named by a stream identifier.</summary>
        PlayChannel = 1,

        /// <summary>Ask the device to close its portal.</summary>
        ClosePortal = 2,

        /// <summary>Ask the device to reload its portal.</summary>
        ReloadPortal = 3,

        /// <summary>Ask the device to reboot.</summary>
        RebootDevice = 4,

        /// <summary>Ask the device to show a message.</summary>
        SendMessage = 5,

        /// <summary>Ask the device to reset its STB lock.</summary>
        ResetStbLock = 6
    }
}
