namespace Fluxy.Core.Content.Commands
{
    /// <summary>
    /// What to change about an existing bouquet. The one field a body can name is the name.
    /// </summary>
    /// <remarks>
    /// A null means "leave the current name alone", which is how every patch in this API
    /// reads: a property the caller did not send is a property the caller did not decide.
    /// </remarks>
    public sealed record BouquetPatch
    {
        /// <summary>New name, or null to keep the one on the row.</summary>
        public string? Name { get; init; }
    }
}
