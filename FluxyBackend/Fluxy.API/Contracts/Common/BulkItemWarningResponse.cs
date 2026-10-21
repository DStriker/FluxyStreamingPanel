namespace Fluxy.API.Contracts.Common
{
    /// <summary>
    /// One thing a successful bulk row should tell the operator but did not refuse.
    /// </summary>
    /// <remarks>
    /// A warning belongs to a row that was written. It carries a code for the client to
    /// translate and an English sentence for the cases where the client has no translation
    /// yet, the same pair the rest of this protocol uses. It is separate from
    /// <c>Errors</c>, because an error means the row was not changed and a warning means it
    /// was - a client that folded the two together would redraw a successful row as a refusal.
    /// </remarks>
    public sealed record BulkItemWarningResponse
    {
        /// <summary>Machine readable name of what happened.</summary>
        public required string Code { get; init; }

        /// <summary>Human readable fallback, in English.</summary>
        public required string Message { get; init; }
    }
}
