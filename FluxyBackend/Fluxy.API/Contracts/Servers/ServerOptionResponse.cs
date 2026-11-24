namespace Fluxy.API.Contracts.Servers
{
    /// <summary>
    /// One entry of the server picker: the identifier, the name it is chosen by, and the one flag
    /// the stream form filters on.
    /// </summary>
    /// <remarks>
    /// Deliberately the whole shape of the picker and nothing else. The endpoint that answers
    /// this list is gated by an "any of" policy over the subscription and stream read grants
    /// rather than by the server pair, because choosing a server while editing either form is
    /// part of editing it.
    /// </remarks>
    public sealed record ServerOptionResponse
    {
        /// <summary>Identifier of the server, as text.</summary>
        public required string Id { get; init; }

        /// <summary>Display name.</summary>
        public required string Name { get; init; }

        /// <summary>Whether the server only serves timeshifted content.</summary>
        public required bool TimeshiftOnly { get; init; }
    }
}
