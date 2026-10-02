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

        /// <summary>
        /// Path the browser should show the visitor next, without a leading origin. Null when
        /// there is nowhere in particular to go.
        /// </summary>
        /// <remarks>
        /// The server names the path rather than sending a redirect. This API is JSON, and a
        /// <c>fetch</c> that follows a 302 ends up holding an HTML page it cannot parse - so a
        /// redirect here would arrive at the frontend as a parse failure with no status and no
        /// body to explain it. Naming the destination instead keeps the decision in the
        /// application that owns the routes, and the leading slash is added here so the frontend
        /// can hand the value straight to its router.
        ///
        /// It is a path and not a full address on purpose. The server does not know which host
        /// serves the frontend, and building an absolute URL from a guess is how an open redirect
        /// gets introduced.
        /// </remarks>
        public string? Redirect { get; init; }
    }
}
