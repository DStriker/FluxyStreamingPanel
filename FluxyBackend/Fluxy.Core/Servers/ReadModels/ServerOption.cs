namespace Fluxy.Core.Servers.ReadModels
{
    /// <summary>
    /// One entry of the picker the subscription and stream forms draw: a server's identifier, the
    /// name it is chosen by, and the one flag that filters the transcoding choice.
    /// </summary>
    /// <remarks>
    /// The picker needs nothing else. It is answered by a different endpoint with a different
    /// grant from the full list, because choosing a server while editing a subscription or a
    /// stream is part of editing it: an operator who may run either form gets the names to choose
    /// from and no server table to browse.
    /// </remarks>
    public sealed record ServerOption
    {
        /// <summary>Identifier of the server.</summary>
        public required Guid Id { get; init; }

        /// <summary>Display name.</summary>
        public required string Name { get; init; }

        /// <summary>Whether the server only serves timeshifted content.</summary>
        public required bool TimeshiftOnly { get; init; }
    }
}
