namespace Fluxy.API.Contracts
{
    /// <summary>
    /// The display time zone an account should be read in, or nothing to return it to the
    /// browser's own.
    /// </summary>
    /// <remarks>
    /// The JSON spelling is <c>timeZone</c> on both halves of the feature - this body and the
    /// <c>ProfileResponse</c> - because one fact with two spellings is a typo waiting to be
    /// read as a missing field, and <c>timeZone</c> is the camelCase of the same
    /// <c>TimeZone</c> name C# uses, so nothing has to be remembered.
    ///
    /// No password travels with it: this is a rendering preference rather than a fact about
    /// the account, and the session is the proof that the caller may set it.
    /// </remarks>
    public sealed record ChangeTimezoneRequest
    {
        /// <summary>
        /// IANA identifier to store (`Europe/Moscow`), or null / empty to clear the choice
        /// and fall back to whatever the browser reports.
        /// </summary>
        public string? TimeZone { get; init; }
    }
}
