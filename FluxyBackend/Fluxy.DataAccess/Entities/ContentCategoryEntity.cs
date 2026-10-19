using Fluxy.Core.Content.Enums;
using Fluxy.Core.Content.Models;
using Fluxy.Core.Persistence.Base;

namespace Fluxy.DataAccess.Entities
{
    /// <summary>
    /// Database representation of one category of one kind of content. It is mutable, has no
    /// behaviour beyond mapping to <see cref="ContentCategory"/>, and knows nothing about HTTP.
    /// </summary>
    public sealed class ContentCategoryEntity : AuditableEntity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ContentCategoryEntity"/> class for a
        /// category that is about to be inserted. The identifier and both audit timestamps
        /// are generated.
        /// </summary>
        public ContentCategoryEntity()
        {
        }

        /// <summary>
        /// Display name, at most <see cref="Configurations.ContentCategoryConfiguration.NameMaxLength"/>
        /// characters. Unique within its <see cref="Type"/>, compared case sensitively - the
        /// same rule the accounts and the groups are held to.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The kind of content this category sorts. Never changes after the row is written:
        /// moving a category between kinds would strand every content item filed under it,
        /// and a category that has to exist twice is simply written twice.
        /// </summary>
        public ContentType Type { get; set; }

        /// <summary>
        /// Position among the categories of the same <see cref="Type"/>, lowest first. A
        /// plain integer rather than anything self-spacing: re-arranging is one write of
        /// 1..N over the rows the request names, and a deletion leaves a gap that nothing
        /// hurries to close.
        /// </summary>
        public int SortOrder { get; set; }

        /// <summary>Builds the business model out of this row.</summary>
        public ContentCategory ToModel() => new()
        {
            Id = Id,
            Name = Name,
            Type = Type,
            SortOrder = SortOrder
        };

        /// <summary>Copies the changeable fields of the business model onto this row.</summary>
        /// <remarks>
        /// Every field of a category is part of its identity except the name - the type and
        /// the position are facts the service decides, not the model - so this copy exists
        /// for completeness and symmetry with the other entities, and is not expected to be
        /// called with anything but a matching shape.
        /// </remarks>
        public void FromModel(ContentCategory model)
        {
            Name = model.Name;
            Type = model.Type;
            SortOrder = model.SortOrder;
        }
    }
}
