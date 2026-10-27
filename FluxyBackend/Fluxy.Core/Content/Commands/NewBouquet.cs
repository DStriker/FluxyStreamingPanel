namespace Fluxy.Core.Content.Commands
{
    /// <summary>
    /// A bouquet to create: a name and nothing else.
    /// </summary>
    /// <remarks>
    /// There is deliberately no position in the body, for the reason
    /// <see cref="NewContentCategory"/> gives: the order a bouquet is shown in belongs to the
    /// list that shows it, so a new bouquet joins the end of that list and the client has no
    /// position to name.
    /// </remarks>
    public sealed record NewBouquet
    {
        /// <summary>Name of the new bouquet, unique for the installation without regard to case.</summary>
        public required string Name { get; init; }
    }
}
