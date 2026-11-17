using Fluxy.Application.Transcoding.Utils;
using Fluxy.Core.Persistence.Exceptions;
using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Persistence.Utils;
using Fluxy.Core.Transcoding.Commands;
using Fluxy.Core.Transcoding.Enums;
using Fluxy.Core.Transcoding.Interfaces;
using Fluxy.Core.Transcoding.Models;
using Fluxy.Core.Transcoding.Outcomes;
using Fluxy.Core.Transcoding.ReadModels;
using Fluxy.Core.Transcoding.Repositories;
using Fluxy.Core.Transcoding.Utils;

namespace Fluxy.Application.Transcoding.Services
{
    /// <summary>
    /// Reads the transcoding profiles and changes them on an administrator's behalf.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A profile is a name plus a set of ffmpeg options, every one of them optional. What the
    /// service decides is what an operator may write: the name's length, every numeric bound the
    /// model states, and the all-or-none redaction rectangle. What it does not decide is how an
    /// option is spelled on a command line - that belongs to the encoder.
    /// </para>
    /// <para>
    /// <b>Uniqueness is checked twice on purpose.</b> The pre-check answers the friendly message
    /// for the ordinary case; the named index is what actually guarantees it, because two
    /// requests can both pass the check and only one can win the insert. Both paths answer
    /// <see cref="TranscodingProfileAction.AlreadyExists"/>, so a caller cannot tell which one it
    /// met - and a violation from any other index is rethrown rather than dressed up as a taken
    /// name.
    /// </para>
    /// <para>
    /// <b>Deleting a referenced profile is refused.</b> The four foreign keys are
    /// <c>Restrict</c>, so the database would refuse it anyway; counting first answers with a
    /// code and a number instead of a database failure.
    /// </para>
    /// <para>
    /// Nothing here knows about HTTP. Which <see cref="TranscodingProfileAction"/> becomes a 201
    /// and which a 409 is the transport layer's business.
    /// </para>
    /// </remarks>
    public sealed class TranscodingProfileService : ITranscodingProfileService
    {
        private readonly ITranscodingProfileRepository _profiles;
        private readonly IUnitOfWork _unitOfWork;

        /// <summary>
        /// Initializes a new instance of the <see cref="TranscodingProfileService"/> class.
        /// </summary>
        /// <param name="profiles">Repository holding the profiles.</param>
        /// <param name="unitOfWork">Commits the changes one operation stages.</param>
        public TranscodingProfileService(
            ITranscodingProfileRepository profiles,
            IUnitOfWork unitOfWork)
        {
            _profiles = profiles;
            _unitOfWork = unitOfWork;
        }

        /// <inheritdoc />
        public Task<TranscodingProfilePage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            TranscodingProfileSortField sortBy,
            TranscodingProfileSortOrder sortOrder,
            CancellationToken cancellationToken = default)
            => _profiles.GetPageAsync(page, pageSize, search, sortBy, sortOrder, cancellationToken);

        /// <inheritdoc />
        public async Task<TranscodingProfileView?> GetAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var profile = await _profiles.GetAsync(id, cancellationToken);

            return profile is null ? null : ToView(profile);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<TranscodingProfileOption>> GetOptionsAsync(
            CancellationToken cancellationToken = default)
            => _profiles.GetOptionsAsync(cancellationToken);

        /// <inheritdoc />
        public async Task<TranscodingProfileOutcome> CreateAsync(
            NewTranscodingProfile input,
            CancellationToken cancellationToken = default)
        {
            var write = new TranscodingProfileWrite
            {
                Name = input.Name.Trim(),
                VideoCodec = input.VideoCodec ?? TranscodingProfileRules.DefaultVideoCodec,
                AudioCodec = input.AudioCodec ?? TranscodingProfileRules.DefaultAudioCodec,
                Preset = input.Preset ?? TranscodingProfileRules.DefaultPreset,
                VideoProfile = input.VideoProfile,
                VideoBitrateAvg = input.VideoBitrateAvg,
                AudioBitrateAvg = input.AudioBitrateAvg,
                MinBitrateTolerance = input.MinBitrateTolerance,
                MaxBitrateTolerance = input.MaxBitrateTolerance,
                BufferSize = input.BufferSize,
                Crf = input.Crf,
                Width = input.Width,
                Height = input.Height,
                AspectRatio = NormalizeText(input.AspectRatio),
                Framerate = input.Framerate,
                AudioSampleRate = input.AudioSampleRate,
                AudioChannels = input.AudioChannels,
                Threads = input.Threads,
                LogoUrl = NormalizeText(input.LogoUrl),
                RedactionX = input.RedactionX,
                RedactionY = input.RedactionY,
                RedactionWidth = input.RedactionWidth,
                RedactionHeight = input.RedactionHeight
            };

            var errors = TranscodingProfileRules.Validate(write);

            if (errors.Count > 0)
            {
                return Invalid(errors);
            }

            if (await _profiles.IsNameTakenAsync(write.Name, exceptProfile: null, cancellationToken))
            {
                return AlreadyExists(nameof(NewTranscodingProfile.Name));
            }

            var profileId = await _profiles.AddAsync(write, cancellationToken);

            if (await _unitOfWork.TrySaveAsync(cancellationToken) is { } createFailure)
            {
                return createFailure.IsConstraint(NameIndexName)
                    ? AlreadyExists(nameof(NewTranscodingProfile.Name))
                    : throw createFailure;
            }

            return new TranscodingProfileOutcome
            {
                Action = TranscodingProfileAction.Created,
                ProfileId = profileId
            };
        }

        /// <inheritdoc />
        public async Task<TranscodingProfileOutcome> UpdateAsync(
            Guid id,
            TranscodingProfilePatch patch,
            CancellationToken cancellationToken = default)
        {
            var profile = await _profiles.GetAsync(id, cancellationToken);

            if (profile is null)
            {
                return NotFound(id);
            }

            // Merged first and judged afterwards, so the rules below see the row as it is about
            // to become rather than as it was. The four non-nullable columns keep what the row
            // holds when the patch says nothing; every nullable option becomes exactly what the
            // patch names, because null there is the state "no flag".
            var write = new TranscodingProfileWrite
            {
                Name = patch.Name is null ? profile.Name : patch.Name.Trim(),
                VideoCodec = patch.VideoCodec ?? profile.VideoCodec,
                AudioCodec = patch.AudioCodec ?? profile.AudioCodec,
                Preset = patch.Preset ?? profile.Preset,
                VideoProfile = patch.VideoProfile,
                VideoBitrateAvg = patch.VideoBitrateAvg,
                AudioBitrateAvg = patch.AudioBitrateAvg,
                MinBitrateTolerance = patch.MinBitrateTolerance,
                MaxBitrateTolerance = patch.MaxBitrateTolerance,
                BufferSize = patch.BufferSize,
                Crf = patch.Crf,
                Width = patch.Width,
                Height = patch.Height,
                AspectRatio = NormalizeText(patch.AspectRatio),
                Framerate = patch.Framerate,
                AudioSampleRate = patch.AudioSampleRate,
                AudioChannels = patch.AudioChannels,
                Threads = patch.Threads,
                LogoUrl = NormalizeText(patch.LogoUrl),
                RedactionX = patch.RedactionX,
                RedactionY = patch.RedactionY,
                RedactionWidth = patch.RedactionWidth,
                RedactionHeight = patch.RedactionHeight
            };

            var errors = TranscodingProfileRules.Validate(write);

            if (errors.Count > 0)
            {
                return Invalid(errors, id);
            }

            if (await _profiles.IsNameTakenAsync(write.Name, id, cancellationToken))
            {
                return AlreadyExists(nameof(TranscodingProfilePatch.Name), id);
            }

            // The write reports false for a row that no longer exists: a profile deleted between
            // the read above and this call would otherwise commit an empty save and answer 200
            // for a record the same id's GET answers 404 for.
            if (await _profiles.UpdateAsync(id, write, cancellationToken) is false)
            {
                return NotFound(id);
            }

            if (await _unitOfWork.TrySaveAsync(cancellationToken) is { } updateFailure)
            {
                return updateFailure.IsConstraint(NameIndexName)
                    ? AlreadyExists(nameof(TranscodingProfilePatch.Name), id)
                    : throw updateFailure;
            }

            return new TranscodingProfileOutcome
            {
                Action = TranscodingProfileAction.Updated,
                ProfileId = id
            };
        }

        /// <inheritdoc />
        public async Task<TranscodingProfileOutcome> DeleteAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var references = await _profiles.CountReferencesAsync(id, cancellationToken);

            if (references > 0)
            {
                return new TranscodingProfileOutcome
                {
                    Action = TranscodingProfileAction.InUse,
                    ProfileId = id,
                    ReferenceCount = references
                };
            }

            if (await _profiles.RemoveAsync(id, cancellationToken) is false)
            {
                return NotFound(id);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new TranscodingProfileOutcome
            {
                Action = TranscodingProfileAction.Removed,
                ProfileId = id
            };
        }

        /// <summary>
        /// Trims a text option and turns a blank one into null, so "no value" has one spelling.
        /// </summary>
        /// <remarks>
        /// An option is not a note with a "clear" state of its own: the patch carries the whole
        /// set, so a blank string and an absent field mean the same thing here - send no flag.
        /// </remarks>
        private static string? NormalizeText(string? value)
        {
            if (value is null)
            {
                return null;
            }

            var trimmed = value.Trim();

            return trimmed.Length == 0 ? null : trimmed;
        }

        /// <summary>Turns the stored model into the shape a response is built from.</summary>
        private static TranscodingProfileView ToView(TranscodingProfile profile) => new()
        {
            Id = profile.Id,
            Name = profile.Name,
            VideoCodec = profile.VideoCodec,
            AudioCodec = profile.AudioCodec,
            Preset = profile.Preset,
            VideoProfile = profile.VideoProfile,
            VideoBitrateAvg = profile.VideoBitrateAvg,
            AudioBitrateAvg = profile.AudioBitrateAvg,
            MinBitrateTolerance = profile.MinBitrateTolerance,
            MaxBitrateTolerance = profile.MaxBitrateTolerance,
            BufferSize = profile.BufferSize,
            Crf = profile.Crf,
            Width = profile.Width,
            Height = profile.Height,
            AspectRatio = profile.AspectRatio,
            Framerate = profile.Framerate,
            AudioSampleRate = profile.AudioSampleRate,
            AudioChannels = profile.AudioChannels,
            Threads = profile.Threads,
            LogoUrl = profile.LogoUrl,
            RedactionX = profile.RedactionX,
            RedactionY = profile.RedactionY,
            RedactionWidth = profile.RedactionWidth,
            RedactionHeight = profile.RedactionHeight
        };

        /// <summary>
        /// The name of the unique index, read from the Core limits rather than written out here.
        /// The configuration and the service must agree on one string; the constant is that one
        /// string.
        /// </summary>
        private static string NameIndexName => TranscodingProfileLimits.NameIndexName;

        private static TranscodingProfileOutcome NotFound(Guid id)
            => new() { Action = TranscodingProfileAction.NotFound, ProfileId = id };

        private static TranscodingProfileOutcome AlreadyExists(string field, Guid id = default)
            => new()
            {
                Action = TranscodingProfileAction.AlreadyExists,
                ProfileId = id,
                Errors = Field(field, ["Another profile already has that name."])
            };

        private static TranscodingProfileOutcome Invalid(
            IReadOnlyDictionary<string, string[]> errors,
            Guid id = default)
            => new() { Action = TranscodingProfileAction.Invalid, ProfileId = id, Errors = errors };

        private static Dictionary<string, string[]> Field(string name, string[] sentences)
            => new(StringComparer.Ordinal) { [name] = sentences };
    }
}
