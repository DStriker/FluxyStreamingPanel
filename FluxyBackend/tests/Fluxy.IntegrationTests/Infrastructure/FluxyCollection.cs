namespace Fluxy.IntegrationTests.Infrastructure
{
    /// <summary>
    /// The single collection every integration test belongs to. Test classes in one collection are
    /// run sequentially against the shared database and containers.
    /// </summary>
    [CollectionDefinition(Name)]
    public sealed class FluxyCollection : ICollectionFixture<FluxyFixture>
    {
        public const string Name = "fluxy-integration";
    }
}
