namespace Fluxy.API.Contracts.Content
{
    /// <summary>
    /// How one playback domain ended a source replacement, as the wire reports it.
    /// </summary>
    /// <remarks>
    /// <see cref="Domain"/> is the stable wire name rather than the enum member name, because
    /// the four values are a client-facing contract and renaming a C# member must not rename
    /// the field a screen keys its labels by.
    /// </remarks>
    public sealed record SourceReplacementDomainCount
    {
        /// <summary>Wire name of the domain, one of the four the endpoint always reports.</summary>
        public required string Domain { get; init; }

        /// <summary>Entities whose values were changed.</summary>
        public required int Updated { get; init; }

        /// <summary>Candidate addresses the replacement left unchanged.</summary>
        public required int Skipped { get; init; }
    }
}
