namespace Fluxy.Core.Servers.ReadModels
{
    /// <summary>
    /// The one fact another aggregate needs about a server it is about to point at: that the row
    /// exists, its name, and whether the machine only serves timeshifted content.
    /// </summary>
    /// <remarks>
    /// Deliberately smaller than <see cref="ServerView"/>. A subscription and a stream name a
    /// server by its identifier and, for the stream, judge one flag of it - neither has any use
    /// for the address, the latency or the country list, and reading them would be a query
    /// shaped for a screen nobody is drawing.
    /// </remarks>
    public sealed record ServerReference
    {
        /// <summary>Identifier of the server.</summary>
        public required Guid Id { get; init; }

        /// <summary>Display name, unique across the installation.</summary>
        public required string Name { get; init; }

        /// <summary>Whether the server only serves timeshifted content.</summary>
        public required bool TimeshiftOnly { get; init; }
    }
}
