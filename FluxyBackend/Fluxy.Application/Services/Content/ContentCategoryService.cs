using Fluxy.Core.Abstractions;
using Fluxy.Core.Models.Content;
using Fluxy.DataAccess.Configurations;
using Fluxy.DataAccess.Context;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fluxy.Application.Services.Content
{
    /// <summary>
    /// Reads the categories of one kind of content and changes them on an admin's behalf.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two rules live here rather than in the controller so that a second endpoint reaching
    /// the same action cannot forget them: a name is unique <i>within its type</i> (two kinds
    /// of content may each carry "Sports" and they are unrelated rows), and a reorder names
    /// <b>every</b> category of the type exactly once before anything is written - a partial
    /// order would be an order for some rows and silence for the rest, which is not an order
    /// at all.
    /// </para>
    /// <para>
    /// Deleting a category that is still filed under is refused with a count rather than
    /// allowed to strand the rows that name it. The check lives here rather than in the
    /// controller so that no second endpoint reaching <c>DeleteAsync</c> can forget it, and it
    /// runs before the write because the foreign key would refuse the write anyway - the
    /// difference is that the answer is a code and a number rather than a provider's
    /// exception.
    /// </para>
    /// <para>
    /// Nothing here knows about HTTP. Which <see cref="ContentCategoryAction"/> becomes a 201
    /// and which becomes a 409 is the transport layer's business.
    /// </para>
    /// </remarks>
    public sealed class ContentCategoryService : IContentCategoryService
    {
        /// <summary>The sentence a name that is blank or too long gets.</summary>
        private static readonly string[] NameRule =
        [
            "The name must be between 1 and "
                + ContentCategoryConfiguration.NameMaxLength
                + " characters."
        ];

        /// <summary>The sentence a type that names no member of the enum gets.</summary>
        private static readonly string[] TypeRule =
        [
            "The content type must be one of: videostream, audiostream, videoondemand, series."
        ];

        /// <summary>The sentence a reorder list that does not match the stored rows gets.</summary>
        private static readonly string[] OrderRule =
        [
            "The list must name every category of this content type exactly once, in the new order."
        ];

        private readonly FluxyDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="ContentCategoryService"/> class.
        /// </summary>
        /// <param name="context">Context holding the categories.</param>
        public ContentCategoryService(FluxyDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<ContentCategory>> GetAllAsync(
            ContentType type,
            CancellationToken cancellationToken = default)
        {
            // The type is part of the index (content_type, sort_order), so the whole read is
            // one range scan in the order the list is shown in. The tie-breaks below are for
            // the gap a deletion leaves and the two rows a raced create can share a position
            // with: the database must answer the same order on every read, not one it chose
            // that morning.
            var rows = await _context.ContentCategories
                .AsNoTracking()
                .Where(entry => entry.Type == type)
                .OrderBy(entry => entry.SortOrder)
                .ThenBy(entry => entry.CreatedAt)
                .ThenBy(entry => entry.Id)
                .ToListAsync(cancellationToken);

            return rows.Select(row => row.ToModel()).ToList();
        }

        /// <inheritdoc />
        public async Task<ContentCategoryOutcome> CreateAsync(
            NewContentCategory input,
            CancellationToken cancellationToken = default)
        {
            var name = input.Name.Trim();

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            if (name.Length is < 1 or > ContentCategoryConfiguration.NameMaxLength)
            {
                errors[nameof(NewContentCategory.Name)] = NameRule;
            }

            // A body is parsed before it reaches here, but the enum is a number in the
            // column and a number outside the members would be a kind every reader has to
            // guess about - refused rather than coerced.
            if (!Enum.IsDefined(input.Type))
            {
                errors[nameof(NewContentCategory.Type)] = TypeRule;
            }

            if (errors.Count > 0)
            {
                return Invalid(errors);
            }

            if (await NameTakenAsync(name, input.Type, exceptCategory: null, cancellationToken))
            {
                return AlreadyExists(nameof(NewContentCategory.Name));
            }

            // The end of the list is the highest position this type holds, plus one - read
            // here rather than claimed by the caller, because the caller's screen can be one
            // move behind the database and the end is a fact about the rows, not about what
            // anybody saw. An empty type reads null and starts at 1.
            var highest = await _context.ContentCategories
                .Where(entry => entry.Type == input.Type)
                .MaxAsync(entry => (int?)entry.SortOrder, cancellationToken);

            var entity = new ContentCategoryEntity
            {
                Name = name,
                Type = input.Type,
                SortOrder = (highest ?? 0) + 1
            };

            _context.ContentCategories.Add(entity);

            if (await SaveAsync(cancellationToken) is not null)
            {
                // A name another request took between the check above and this write. The
                // answer is the same one the check would have given.
                return AlreadyExists(nameof(NewContentCategory.Name));
            }

            return new ContentCategoryOutcome
            {
                Action = ContentCategoryAction.Created,
                CategoryId = entity.Id
            };
        }

        /// <inheritdoc />
        public async Task<ContentCategoryOutcome> UpdateAsync(
            Guid id,
            ContentCategoryPatch patch,
            CancellationToken cancellationToken = default)
        {
            var category = await _context.ContentCategories
                .FirstOrDefaultAsync(entry => entry.Id == id, cancellationToken);

            if (category is null)
            {
                return NotFound(id);
            }

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            var name = patch.Name?.Trim();
            if (patch.Name is not null && name!.Length is < 1 or > ContentCategoryConfiguration.NameMaxLength)
            {
                errors[nameof(ContentCategoryPatch.Name)] = NameRule;
            }

            if (errors.Count > 0)
            {
                return Invalid(errors, id);
            }

            if (name is not null
                && await NameTakenAsync(name, category.Type, id, cancellationToken))
            {
                var conflict = AlreadyExists(nameof(ContentCategoryPatch.Name));
                return conflict with { CategoryId = id };
            }

            if (name is not null)
            {
                category.Name = name;
            }

            if (await SaveAsync(cancellationToken) is not null)
            {
                return AlreadyExists(nameof(ContentCategoryPatch.Name)) with { CategoryId = id };
            }

            return new ContentCategoryOutcome
            {
                Action = ContentCategoryAction.Updated,
                CategoryId = id
            };
        }

        /// <inheritdoc />
        public async Task<ContentCategoryOutcome> DeleteAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var category = await _context.ContentCategories
                .FirstOrDefaultAsync(entry => entry.Id == id, cancellationToken);

            if (category is null)
            {
                return NotFound(id);
            }

            // Counted before the write rather than by catching the foreign key's refusal,
            // because the two answers are worth very different things to the operator: one is
            // "this many streams still point at it", the other is a provider's exception
            // wearing a 500. The key below would refuse the row anyway; this is what turns
            // that refusal into a sentence.
            var inUse = await _context.VideoStreams
                .CountAsync(entry => entry.CategoryId == id, cancellationToken);

            if (inUse > 0)
            {
                return new ContentCategoryOutcome
                {
                    Action = ContentCategoryAction.InUse,
                    CategoryId = id,
                    UsedCount = inUse
                };
            }

            _context.ContentCategories.Remove(category);

            // Deletion leaves a gap in the positions, and nothing closes it. Renumbering the
            // survivors here would be a second source for a fact the reorder request owns:
            // the next drag names every row anyway, and until then an order read by position
            // is exactly the same order with or without the gap.
            await _context.SaveChangesCheckedAsync(cancellationToken);

            return new ContentCategoryOutcome
            {
                Action = ContentCategoryAction.Removed,
                CategoryId = id
            };
        }

        /// <inheritdoc />
        public async Task<ContentCategoryOutcome> ReorderAsync(
            ReorderContentCategories input,
            CancellationToken cancellationToken = default)
        {
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            if (!Enum.IsDefined(input.Type))
            {
                errors[nameof(ReorderContentCategories.Type)] = TypeRule;
                return Invalid(errors);
            }

            // Read for the comparison, then written over - the same rows, one load. Tracking
            // rather than AsNoTracking because the rows that survive the check are the ones
            // the write below moves.
            var stored = await _context.ContentCategories
                .Where(entry => entry.Type == input.Type)
                .OrderBy(entry => entry.SortOrder)
                .ToListAsync(cancellationToken);

            // The caller has just dragged a row on a screen that shows the whole list, so it
            // is in a position to name all of it - and naming all of it is the only
            // interpretation a server can apply without guessing. A subset leaves rows
            // without a position, a duplicate makes two claims about one row, and an id the
            // type does not hold means the caller's screen is describing rows that are not
            // here. All three are refused whole rather than partly applied.
            if (input.OrderedIds.Count != stored.Count
                || input.OrderedIds.Distinct().Count() != input.OrderedIds.Count
                || !input.OrderedIds.All(id => stored.Any(entry => entry.Id == id)))
            {
                errors[nameof(ReorderContentCategories.OrderedIds)] = OrderRule;
                return Invalid(errors);
            }

            // Positions 1..N in the order the request named. One save, so a request
            // interrupted halfway cannot leave two categories sharing a position: either
            // every row moved or none did.
            for (var index = 0; index < input.OrderedIds.Count; index++)
            {
                var id = input.OrderedIds[index];
                var category = stored.First(entry => entry.Id == id);
                category.SortOrder = index + 1;
            }

            await _context.SaveChangesCheckedAsync(cancellationToken);

            return new ContentCategoryOutcome { Action = ContentCategoryAction.Reordered };
        }

        /// <summary>
        /// Whether some other category of the same type already answers to that name.
        /// </summary>
        /// <remarks>
        /// Compared exactly, case sensitively, for the same reason username and email are -
        /// and scoped to the type, because "Sports" over video streams and "Sports" over
        /// series are two sections of two different screens. Excluding
        /// <paramref name="exceptCategory"/> is what lets a rename keep its own name.
        /// </remarks>
        private async Task<bool> NameTakenAsync(
            string name,
            ContentType type,
            Guid? exceptCategory,
            CancellationToken cancellationToken)
        {
            return await _context.ContentCategories
                .AsNoTracking()
                .AnyAsync(
                    entry => entry.Name == name
                        && entry.Type == type
                        && (exceptCategory == null || entry.Id != exceptCategory),
                    cancellationToken);
        }

        /// <summary>Writes the staged changes, interpreting the one failure that is expected.</summary>
        /// <returns>Null when it was written, or the exception when the name index refused it.</returns>
        /// <remarks>
        /// Only the index making a name unique within its type is interpreted. A refusal by any
        /// other index is a bug in this service, and answering it as "that name is taken" is how
        /// such a bug hides behind a sentence about something else - so it is left to surface
        /// as the failure it is.
        /// </remarks>
        private async Task<UniqueViolationException?> SaveAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _context.SaveChangesCheckedAsync(cancellationToken);
                return null;
            }
            catch (UniqueViolationException exception) when (exception.IsConstraint(NameIndexName))
            {
                return exception;
            }
        }

        /// <summary>The unique index making a category's name unique within its type.</summary>
        private const string NameIndexName = "IX_content_categories_content_type_name";

        private static ContentCategoryOutcome NotFound(Guid id)
            => new() { Action = ContentCategoryAction.NotFound, CategoryId = id };

        private static ContentCategoryOutcome AlreadyExists(string field)
            => new()
            {
                Action = ContentCategoryAction.AlreadyExists,
                Errors = new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    [field] = ["Another category of this content type already has that name."]
                }
            };

        private static ContentCategoryOutcome Invalid(
            IReadOnlyDictionary<string, string[]> errors,
            Guid id = default)
            => new() { Action = ContentCategoryAction.Invalid, CategoryId = id, Errors = errors };
    }
}
