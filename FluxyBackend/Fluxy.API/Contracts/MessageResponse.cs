namespace Fluxy.API.Contracts
{
    /// <summary>
    /// The single body every endpoint of this API answers with, whether it succeeded or not.
    /// </summary>
    /// <remarks>
    /// Two fields travel together on purpose. <see cref="Code"/> is the stable part and is what
    /// a client should branch on; <see cref="Message"/> is an English fallback for the cases
    /// where the client has no translation for the code yet. Keeping the text out of the code
    /// is what lets the wording change without breaking anyone.
    /// </remarks>
    public sealed record MessageResponse
    {
        /// <summary>
        /// Machine readable outcome, written in snake_case to match the naming the rest of the
        /// protocol already uses for actions.
        /// </summary>
        public required string Code { get; init; }

        /// <summary>Human readable fallback, in English.</summary>
        public required string Message { get; init; }

        /// <summary>
        /// Rejected fields and why they were rejected, keyed by the camelCase name of the
        /// property. Null whenever nothing was rejected, so a client can test it as a boolean.
        /// </summary>
        public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
    }
}