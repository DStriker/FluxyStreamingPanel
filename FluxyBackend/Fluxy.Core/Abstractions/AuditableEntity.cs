namespace Fluxy.Core.Abstractions
{
    /// <summary>
    /// Base class of every persisted entity. It carries the two rules that the project
    /// applies to all database entities: the identifier is a <see cref="Guid"/> generated
    /// on the client, and the row keeps the moment it was created and the moment it was
    /// last modified.
    /// </summary>
    /// <remarks>
    /// The audit properties have no public setter on purpose. Entity Framework writes them
    /// through its own materializer and through the change tracker, so no application code
    /// can change an audit value by accident, and the timestamps are assigned in one place
    /// only - <c>FluxyDbContext.SaveChangesAsync</c> - instead of in every constructor.
    /// </remarks>
    public abstract class AuditableEntity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AuditableEntity"/> class with a
        /// freshly generated <see cref="Id"/>. Generating the key here means an entity that
        /// never goes through Entity Framework - the default admin seeder, a unit test - is
        /// already complete.
        /// </summary>
        protected AuditableEntity()
        {
            Id = Guid.NewGuid();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AuditableEntity"/> class from the
        /// values of an existing row. It exists for mapping a business model back to an
        /// entity, where the stored values must survive instead of being regenerated.
        /// </summary>
        /// <param name="id">Primary key of the stored row.</param>
        /// <param name="createdAt">Creation timestamp of the stored row.</param>
        /// <param name="updatedAt">Last modification timestamp of the stored row.</param>
        protected AuditableEntity(Guid id, DateTimeOffset createdAt, DateTimeOffset updatedAt)
        {
            Id = id;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
        }

        /// <summary>
        /// Primary key. Generated on the client, so no round trip to the database is needed
        /// and the value is already known by the entity that was just created.
        /// </summary>
        public Guid Id { get; private set; }

        /// <summary>
        /// Moment the row was inserted. Equals <see cref="UpdatedAt"/> on a fresh row and is
        /// assigned by the context, not by the entity.
        /// </summary>
        public DateTimeOffset CreatedAt { get; private set; }

        /// <summary>
        /// Moment the row was last modified. It is refreshed for every property EF reports
        /// as changed, so reading it twice for one <c>SaveChanges</c> is safe.
        /// </summary>
        public DateTimeOffset UpdatedAt { get; private set; }
    }
}