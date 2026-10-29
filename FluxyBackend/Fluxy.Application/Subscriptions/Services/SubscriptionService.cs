using System.Security.Cryptography;
using Fluxy.Application.Subscriptions.Utils;
using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Persistence.Utils;
using Fluxy.Core.Subscriptions.Commands;
using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Subscriptions.Interfaces;
using Fluxy.Core.Subscriptions.Models;
using Fluxy.Core.Subscriptions.Outcomes;
using Fluxy.Core.Subscriptions.ReadModels;
using Fluxy.Core.Subscriptions.Repositories;
using Fluxy.Core.Subscriptions.Rows;
using Fluxy.Core.Subscriptions.Utils;
using Fluxy.Core.Users.Enums;

namespace Fluxy.Application.Subscriptions.Services
{
    /// <summary>
    /// Reads the subscriptions and changes them on an administrator's behalf.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rules that can be judged without the database are <see cref="SubscriptionRules"/>'s;
    /// what is left here is the merging of a patch into the row, the probes (owner, plan, bouquets,
    /// the three uniqueness rules) and the one derivation that makes a plan the source of truth.
    /// </para>
    /// <para>
    /// <b>The plan owns what it owns, and a save writes only what it owns.</b> With a plan, the
    /// dependent fields are taken from the plan and the request's values are dropped rather than
    /// refused - an operator changing a disabled input has not asked for anything. Without a plan,
    /// the request's values are stored and <c>tariff_plan_id</c> is nulled.
    /// </para>
    /// <para>
    /// Nothing here knows about HTTP. Which <see cref="SubscriptionAction"/> becomes a 201 and
    /// which a 409 is the transport layer's business.
    /// </para>
    /// </remarks>
    public sealed class SubscriptionService : ISubscriptionService
    {
        /// <summary>How many times the generator tries before giving up on the random source.</summary>
        /// <remarks>
        /// Two collisions in a row are already a 1-in-2^128 event; a loop without a bound would be
        /// the one place a broken random source could hang a request instead of failing it.
        /// </remarks>
        private const int PlaylistIdAttempts = 10;

        private readonly ISubscriptionRepository _subscriptions;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _time;

        /// <summary>
        /// Initializes a new instance of the <see cref="SubscriptionService"/> class.
        /// </summary>
        /// <param name="subscriptions">Repository holding the subscriptions.</param>
        /// <param name="unitOfWork">Commits the changes one operation stages.</param>
        /// <param name="time">Clock the expiry arithmetic is measured from.</param>
        public SubscriptionService(
            ISubscriptionRepository subscriptions,
            IUnitOfWork unitOfWork,
            TimeProvider time)
        {
            _subscriptions = subscriptions;
            _unitOfWork = unitOfWork;
            _time = time;
        }

        /// <inheritdoc />
        public Task<SubscriptionPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            Guid? ownerId,
            Guid? creatorId,
            SubscriptionStatus? status,
            SubscriptionSortField sortBy,
            SubscriptionSortOrder sortOrder,
            CancellationToken cancellationToken = default)
            => _subscriptions.GetPageAsync(
                page,
                pageSize,
                search,
                ownerId,
                creatorId,
                status,
                sortBy,
                sortOrder,
                cancellationToken);

        /// <inheritdoc />
        public Task<SubscriptionDetail?> GetAsync(
            Guid id,
            CancellationToken cancellationToken = default)
            => _subscriptions.GetAsync(id, cancellationToken);

        /// <inheritdoc />
        public async Task<SubscriptionOutcome> CreateAsync(
            NewSubscription input,
            CancellationToken cancellationToken = default)
        {
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            var owner = await _subscriptions.GetOwnerContextAsync(input.UserId, cancellationToken);

            if (owner is null)
            {
                errors[nameof(NewSubscription.UserId)] = SubscriptionRules.OwnerNotFoundRule;
                return Invalid(errors);
            }

            var creator = await _subscriptions.GetOwnerContextAsync(
                input.ResellerId,
                cancellationToken);

            if (creator is null)
            {
                errors[nameof(NewSubscription.ResellerId)] = SubscriptionRules.CreatorNotFoundRule;
                return Invalid(errors);
            }

            if (creator.Role is not (UserRole.Admin or UserRole.Reseller))
            {
                errors[nameof(NewSubscription.ResellerId)] = SubscriptionRules.CreatorRoleRule;
                return Invalid(errors);
            }

            var plan = await ResolvePlanAsync(input.TariffPlanId, owner, errors, cancellationToken);

            var playlistId = NormalizePlaylistId(input.PlaylistId, errors);
            var magMac = NormalizeMac(input.MagMac, errors, nameof(NewSubscription.MagMac));
            var enigma2Mac = NormalizeMac(
                input.Enigma2Mac,
                errors,
                nameof(NewSubscription.Enigma2Mac));

            var (ipWhitelist, ipErrors) = SubscriptionRules.NormalizeIpWhitelist(input.IpWhitelist);
            var (userAgents, userAgentErrors) =
                SubscriptionRules.NormalizeUserAgentWhitelist(input.UserAgentWhitelist);

            Merge(ipErrors, errors);
            Merge(userAgentErrors, errors);

            if (!SubscriptionRules.IsUsableOutputFormats(input.OutputFormats))
            {
                errors[nameof(NewSubscription.OutputFormats)] = SubscriptionRules.OutputFormatsRule;
            }

            if (input.MaxConnections is { } connections
                && !SubscriptionRules.IsUsableConnections(connections))
            {
                errors[nameof(NewSubscription.MaxConnections)] =
                    SubscriptionRules.MaxConnectionsRule;
            }

            var bouquetIds = await ResolveBouquetsAsync(
                input.BouquetIds,
                plan,
                errors,
                cancellationToken);

            // A plan-less subscription must carry at least one bouquet. The rule is stated as
            // "on creation without a plan all bouquets are preselected and at least one bouquet
            // is required", which is why an absent list resolves to every bouquet above and only
            // an explicit empty one reaches this refusal.
            if (plan is null
                && bouquetIds.Count == 0
                && !errors.ContainsKey("BouquetIds"))
            {
                errors["BouquetIds"] = SubscriptionRules.BouquetsRequiredRule;
            }

            // The three uniqueness rules are probed before the write so a duplicate is answered
            // with the field named rather than as a provider error; the partial unique indexes
            // remain the authority when two operators race. A duplicate is its own outcome (409,
            // naming the field) rather than a validation failure: "another subscription already
            // holds this value" is a conflict with the table, not a malformed request.
            var duplicate = await FindDuplicateAsync(
                playlistId,
                magMac,
                enigma2Mac,
                exceptSubscription: null,
                cancellationToken);

            if (duplicate is { } duplicatedField)
            {
                return AlreadyExists(duplicatedField);
            }

            // What the plan owns is derived; what it does not own is the request's. The two
            // branches produce one row, so a create with a plan never stores a value the plan did
            // not state.
            var derived = plan is null
                ? null
                : SubscriptionRules.Derive(plan, _time.GetUtcNow());

            var effective = new Subscription
            {
                Id = Guid.Empty,
                UserId = input.UserId,
                PlaylistId = playlistId,
                ExpireAt = derived?.ExpireAt ?? input.ExpireAt,
                TariffPlanId = plan?.Id,
                MaxConnections = derived is null ? input.MaxConnections : derived.MaxConnections,
                IsRestreamer = derived?.IsRestreamer ?? input.IsRestreamer,
                IpWhitelist = ipWhitelist,
                UserAgentWhitelist = userAgents,
                IsIspLock = input.IsIspLock,
                IsCountryLock = derived?.IsCountryLock ?? input.IsCountryLock,
                IsTrial = derived?.IsTrial ?? input.IsTrial,
                ResellerId = input.ResellerId,
                OutputFormats = derived?.OutputFormats ?? input.OutputFormats,
                MagMac = magMac,
                IsStbLock = derived?.IsStbLock ?? input.IsStbLock,
                Enigma2Mac = enigma2Mac,
                BouquetIds = bouquetIds,
                CreatedAt = _time.GetUtcNow(),
                UpdatedAt = _time.GetUtcNow()
            };

            var services = ApplyServiceSupport(effective, derived);

            if (services is null)
            {
                errors[nameof(NewSubscription.PlaylistId)] = SubscriptionRules.ServiceRequiredRule;
            }

            if (errors.Count > 0)
            {
                return Invalid(errors);
            }

            var subscriptionId = await _subscriptions.AddAsync(
                new NewSubscription
                {
                    UserId = input.UserId,
                    ResellerId = input.ResellerId,
                    PlaylistId = services!.PlaylistId,
                    ExpireAt = services.ExpireAt,
                    TariffPlanId = plan?.Id,
                    MaxConnections = services.MaxConnections,
                    IsRestreamer = services.IsRestreamer,
                    IpWhitelist = services.IpWhitelist,
                    UserAgentWhitelist = services.UserAgentWhitelist,
                    IsIspLock = services.IsIspLock,
                    IsCountryLock = services.IsCountryLock,
                    IsTrial = services.IsTrial,
                    OutputFormats = services.OutputFormats,
                    MagMac = services.MagMac,
                    IsStbLock = services.IsStbLock,
                    Enigma2Mac = services.Enigma2Mac,
                    BouquetIds = services.BouquetIds
                },
                bouquetIds,
                cancellationToken);

            if (await _unitOfWork.TrySaveAsync(cancellationToken) is { } createFailure)
            {
                return MapViolation(createFailure, subscriptionId);
            }

            return new SubscriptionOutcome
            {
                Action = SubscriptionAction.Created,
                SubscriptionId = subscriptionId
            };
        }

        /// <inheritdoc />
        public async Task<SubscriptionOutcome> UpdateAsync(
            Guid id,
            SubscriptionPatch patch,
            CancellationToken cancellationToken = default)
        {
            var current = await _subscriptions.FindForUpdateAsync(id, cancellationToken);

            if (current is null)
            {
                return NotFound(id);
            }

            var owner = await _subscriptions.GetOwnerContextAsync(current.UserId, cancellationToken);

            if (owner is null)
            {
                // The owner row is gone, which the cascade should have made impossible while this
                // subscription exists. Answering "not found" is the honest reading rather than
                // writing a row nobody owns.
                return NotFound(id);
            }

            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            // The issuer may only be reassigned to an administrator or a reseller. The check is
            // here rather than in the bulk operation because the role of the target is a fact
            // about the account the patch names, and this is the layer that reads it.
            if (patch.ResellerId is { } resellerId)
            {
                var issuer = await _subscriptions.GetOwnerContextAsync(resellerId, cancellationToken);

                if (issuer is null)
                {
                    errors[nameof(SubscriptionPatch.ResellerId)] =
                        SubscriptionRules.CreatorNotFoundRule;
                }
                else if (issuer.Role is not (UserRole.Admin or UserRole.Reseller))
                {
                    errors[nameof(SubscriptionPatch.ResellerId)] = SubscriptionRules.CreatorRoleRule;
                }
            }

            // The plan the row will carry after this save: the named one, none when it is being
            // cleared, and the current one when the patch says nothing about it.
            var plan = await ResolvePlanForUpdateAsync(patch, current, owner, errors, cancellationToken);

            var playlistId = patch.PlaylistId is null
                ? current.PlaylistId
                : NormalizePlaylistId(patch.PlaylistId, errors);

            var magMac = patch.MagMac is null
                ? current.MagMac
                : NormalizeMac(patch.MagMac, errors, nameof(SubscriptionPatch.MagMac));

            var enigma2Mac = patch.Enigma2Mac is null
                ? current.Enigma2Mac
                : NormalizeMac(patch.Enigma2Mac, errors, nameof(SubscriptionPatch.Enigma2Mac));

            var ipWhitelist = current.IpWhitelist;
            if (patch.IpWhitelist is not null)
            {
                var (value, ipErrors) = SubscriptionRules.NormalizeIpWhitelist(patch.IpWhitelist);
                ipWhitelist = value;
                Merge(ipErrors, errors);
            }

            var userAgents = current.UserAgentWhitelist;
            if (patch.UserAgentWhitelist is not null)
            {
                var (value, agentErrors) =
                    SubscriptionRules.NormalizeUserAgentWhitelist(patch.UserAgentWhitelist);
                userAgents = value;
                Merge(agentErrors, errors);
            }

            var outputFormats = patch.OutputFormats ?? current.OutputFormats;
            if (!SubscriptionRules.IsUsableOutputFormats(outputFormats))
            {
                errors[nameof(SubscriptionPatch.OutputFormats)] = SubscriptionRules.OutputFormatsRule;
            }

            var maxConnections = patch.ClearMaxConnections
                ? null
                : patch.MaxConnections ?? current.MaxConnections;
            if (maxConnections is { } connections
                && !SubscriptionRules.IsUsableConnections(connections))
            {
                errors[nameof(SubscriptionPatch.MaxConnections)] = SubscriptionRules.MaxConnectionsRule;
            }

            var bouquetIds = patch.BouquetIds is null
                ? current.BouquetIds
                : await ResolveBouquetsAsync(patch.BouquetIds, plan, errors, cancellationToken);

            // Clearing the plan re-activates the stored list, and a list that is empty is
            // preselected again - the rule the specification states as "if it is empty, all
            // bouquets are preselected again". A subscription that carried a plan never stored
            // its own bouquets, so clearing one would otherwise leave nothing to re-activate.
            if (plan is null
                && patch.BouquetIds is null
                && bouquetIds.Count == 0
                && current.TariffPlanId is not null)
            {
                bouquetIds = await _subscriptions.GetAllBouquetIdsAsync(cancellationToken);
            }

            var duplicate = await FindDuplicateAsync(
                playlistId,
                magMac,
                enigma2Mac,
                id,
                cancellationToken);

            if (duplicate is { } duplicatedField)
            {
                return AlreadyExists(duplicatedField, id);
            }

            // The plan-derived values, measured from the subscription's own creation date so a
            // re-save never extends it.
            var derived = plan is null
                ? null
                : SubscriptionRules.Derive(plan, current.CreatedAt);

            var effective = current with
            {
                PlaylistId = playlistId,
                ExpireAt = derived?.ExpireAt
                    ?? (patch.ExpireAt is null
                        ? current.ExpireAt
                        : ParseExpiry(patch.ExpireAt)),
                TariffPlanId = plan?.Id,
                MaxConnections = derived is null ? maxConnections : derived.MaxConnections,
                IsRestreamer = derived?.IsRestreamer ?? patch.IsRestreamer ?? current.IsRestreamer,
                IpWhitelist = ipWhitelist,
                UserAgentWhitelist = userAgents,
                IsIspLock = patch.IsIspLock ?? current.IsIspLock,
                IsCountryLock = derived?.IsCountryLock ?? patch.IsCountryLock ?? current.IsCountryLock,
                IsTrial = derived?.IsTrial ?? patch.IsTrial ?? current.IsTrial,
                OutputFormats = derived?.OutputFormats ?? outputFormats,
                MagMac = magMac,
                IsStbLock = derived?.IsStbLock ?? patch.IsStbLock ?? current.IsStbLock,
                Enigma2Mac = enigma2Mac,
                ResellerId = patch.ResellerId ?? current.ResellerId,
                BouquetIds = bouquetIds
            };

            var services = ApplyServiceSupport(effective, derived);

            if (services is null)
            {
                errors[nameof(SubscriptionPatch.PlaylistId)] = SubscriptionRules.ServiceRequiredRule;
            }

            // A plan-less subscription must carry at least one bouquet - the rule the specification
            // states as "on creation without a plan all bouquets are preselected and at least one
            // bouquet is required".
            if (plan is null
                && services is not null
                && services.BouquetIds.Count == 0
                && !errors.ContainsKey(nameof(SubscriptionPatch.BouquetIds)))
            {
                errors[nameof(SubscriptionPatch.BouquetIds)] = SubscriptionRules.BouquetsRequiredRule;
            }

            if (errors.Count > 0)
            {
                return Invalid(errors, id);
            }

            if (await _subscriptions.UpdateAsync(id, services!, cancellationToken) is false)
            {
                return NotFound(id);
            }

            // The subscription's own bouquet set is written only while it has no plan. With a plan
            // the stored list is left unchanged and unused, so clearing the plan re-activates
            // exactly what was there - and a list that was empty is filled again above, which is
            // why the write also runs when the plan was just cleared rather than when only the
            // patch named bouquets.
            if (plan is null
                && (patch.BouquetIds is not null || current.TariffPlanId is not null))
            {
                await _subscriptions.ReplaceBouquetsAsync(id, bouquetIds, cancellationToken);
            }
            if (await _unitOfWork.TrySaveAsync(cancellationToken) is { } updateFailure)
            {
                return MapViolation(updateFailure, id);
            }

            return new SubscriptionOutcome
            {
                Action = SubscriptionAction.Updated,
                SubscriptionId = id
            };
        }

        /// <inheritdoc />
        public async Task<SubscriptionOutcome> DeleteAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            if (await _subscriptions.RemoveAsync(id, cancellationToken) is false)
            {
                return NotFound(id);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new SubscriptionOutcome
            {
                Action = SubscriptionAction.Deleted,
                SubscriptionId = id
            };
        }

        /// <inheritdoc />
        public async Task<string> GeneratePlaylistIdAsync(CancellationToken cancellationToken = default)
        {
            for (var attempt = 0; attempt < PlaylistIdAttempts; attempt++)
            {
                var candidate = Convert.ToHexStringLower(
                    RandomNumberGenerator.GetBytes(SubscriptionLimits.PlaylistIdLength / 2));

                if (await _subscriptions.PlaylistIdExistsAsync(candidate, cancellationToken) is false)
                {
                    return candidate;
                }
            }

            // Unreachable in practice, and refused rather than answered with a duplicate: a
            // generator that returned a taken id would make the operator's save fail for a reason
            // they did not cause.
            throw new InvalidOperationException(
                "Could not mint a playlist id no subscription holds. The random source appears to " +
                "be repeating.");
        }

        /// <summary>
        /// Resolves the plan a create names, recording why it cannot be attached.
        /// </summary>
        private async Task<SubscriptionPlanContext?> ResolvePlanAsync(
            Guid? planId,
            SubscriptionOwnerContext owner,
            Dictionary<string, string[]> errors,
            CancellationToken cancellationToken)
        {
            if (planId is not { } wanted)
            {
                return null;
            }

            var plan = await _subscriptions.GetPlanContextAsync(wanted, cancellationToken);

            if (plan is null)
            {
                errors["TariffPlanId"] = SubscriptionRules.PlanNotFoundRule;
                return null;
            }

            // The plan is only attachable while its groups include the group the owner belongs to.
            if (SubscriptionRules.IsAvailableToOwner(plan, owner.GroupId) is false)
            {
                errors["TariffPlanId"] = SubscriptionRules.PlanUnavailableRule;
                return null;
            }

            return plan;
        }

        /// <summary>
        /// Resolves the plan a change leaves the row with, recording why it cannot be attached.
        /// </summary>
        /// <remarks>
        /// Only a plan the request <i>names</i> is re-checked for availability: one the row
        /// already carries was accepted when it was attached, and re-checking it on every later
        /// save would refuse an ordinary edit the day the owner moved to another group. Clearing
        /// and keeping therefore answer without a probe, and only an attach pays for one.
        /// </remarks>
        private async Task<SubscriptionPlanContext?> ResolvePlanForUpdateAsync(
            SubscriptionPatch patch,
            Subscription current,
            SubscriptionOwnerContext owner,
            Dictionary<string, string[]> errors,
            CancellationToken cancellationToken)
        {
            // Clearing wins over naming, because a body that names both is a body saying "not
            // this plan" twice; the create-style resolution then never sees it.
            if (patch.ClearTariffPlan && patch.TariffPlanId is null)
            {
                return null;
            }

            if (patch.TariffPlanId is { } wanted)
            {
                return await ResolvePlanAsync(wanted, owner, errors, cancellationToken);
            }

            if (current.TariffPlanId is not { } existing)
            {
                return null;
            }

            return await _subscriptions.GetPlanContextAsync(existing, cancellationToken);
        }

        /// <summary>
        /// Resolves the bouquet set a save stores, recording identifiers that name nothing.
        /// </summary>
        private async Task<IReadOnlyList<Guid>> ResolveBouquetsAsync(
            IReadOnlyList<Guid>? bouquetIds,
            SubscriptionPlanContext? plan,
            Dictionary<string, string[]> errors,
            CancellationToken cancellationToken)
        {
            // With a plan the stored list is left alone; the plan's list is what the subscriber
            // receives, and this value is only used to stage a create the plan then overrides.
            if (plan is not null)
            {
                return [];
            }

            // An absent list on a create means "every bouquet the installation has", which is the
            // domain's default for a plan-less subscription. An empty list is the caller's own
            // statement and is refused by the minimum-one rule.
            if (bouquetIds is null)
            {
                return await _subscriptions.GetAllBouquetIdsAsync(cancellationToken);
            }

            if (bouquetIds.Count == 0)
            {
                return [];
            }

            var existing = await _subscriptions.GetExistingBouquetIdsAsync(
                bouquetIds,
                cancellationToken);

            if (existing.Count != bouquetIds.Distinct().Count())
            {
                errors["BouquetIds"] = SubscriptionRules.BouquetIdsRule;
            }

            return bouquetIds.Distinct().ToList();
        }

        /// <summary>
        /// Clears the services the effective plan does not support, or reports that nothing would
        /// be left.
        /// </summary>
        /// <returns>The row with unsupported services cleared, or null when none would remain.</returns>
        private static Subscription? ApplyServiceSupport(Subscription row, SubscriptionDerived? derived)
        {
            var supportsPlaylist = derived?.SupportsPlaylist ?? true;
            var supportsMag = derived?.SupportsMag ?? true;
            var supportsEnigma2 = derived?.SupportsEnigma2 ?? true;

            if (SubscriptionRules.LeavesNoService(
                row.PlaylistId,
                row.MagMac,
                row.Enigma2Mac,
                supportsPlaylist,
                supportsMag,
                supportsEnigma2))
            {
                return null;
            }

            return row with
            {
                PlaylistId = supportsPlaylist ? row.PlaylistId : null,
                MagMac = supportsMag ? row.MagMac : null,
                Enigma2Mac = supportsEnigma2 ? row.Enigma2Mac : null
            };
        }

        /// <summary>
        /// Finds the first of the three uniqueness rules a create or a change would break.
        /// </summary>
        /// <returns>The command property name of the duplicated field, or null when none is.</returns>
        /// <remarks>
        /// The three fields are independent, so the first duplicate found is the one answered: a
        /// body that names two values another subscription already holds has one reason to give
        /// and the form draws the refusal under one input. Each probe is skipped when the field is
        /// null, which is the "no playlist" and "no device" state rather than a collision.
        /// </remarks>
        private async Task<string?> FindDuplicateAsync(
            string? playlistId,
            string? magMac,
            string? enigma2Mac,
            Guid? exceptSubscription,
            CancellationToken cancellationToken)
        {
            if (playlistId is not null
                && await _subscriptions.IsPlaylistIdTakenAsync(
                    playlistId,
                    exceptSubscription,
                    cancellationToken))
            {
                return "PlaylistId";
            }

            if (magMac is not null
                && await _subscriptions.IsMagMacTakenAsync(
                    magMac,
                    exceptSubscription,
                    cancellationToken))
            {
                return "MagMac";
            }

            if (enigma2Mac is not null
                && await _subscriptions.IsEnigma2MacTakenAsync(
                    enigma2Mac,
                    exceptSubscription,
                    cancellationToken))
            {
                return "Enigma2Mac";
            }

            return null;
        }

        /// <summary>
        /// Canonical form of a playlist id the caller sent, or null when it is being cleared.
        /// </summary>
        /// <remarks>
        /// A blank value clears; a value of the wrong shape is refused rather than normalized.
        /// Accepting an arbitrary spelling would let two subscriptions hold what look like the same
        /// identifier to a player, which is the one thing the uniqueness rule exists to prevent.
        /// </remarks>
        private static string? NormalizePlaylistId(
            string? playlistId,
            Dictionary<string, string[]> errors)
        {
            if (playlistId is null)
            {
                return null;
            }

            var trimmed = playlistId.Trim();

            if (trimmed.Length == 0)
            {
                return null;
            }

            if (SubscriptionNormalizer.IsPlaylistId(trimmed) is false)
            {
                errors["PlaylistId"] = SubscriptionRules.PlaylistIdRule;
                return null;
            }

            return trimmed;
        }

        /// <summary>Canonical form of a MAC the caller sent, or null when it is being cleared.</summary>
        private static string? NormalizeMac(
            string? mac,
            Dictionary<string, string[]> errors,
            string field)
        {
            if (mac is null)
            {
                return null;
            }

            if (mac.Trim().Length == 0)
            {
                return null;
            }

            var canonical = SubscriptionNormalizer.NormalizeMac(mac);

            if (canonical is null)
            {
                errors[field] = SubscriptionRules.MacRule;
            }

            return canonical;
        }

        /// <summary>
        /// Reads an expiry the caller sent as text: blank clears it, a date sets it.
        /// </summary>
        /// <remarks>
        /// Parsed with the invariant culture and no time, because the value is a calendar date the
        /// operator chose rather than an instant: a bare date lands at midnight UTC, and the whole
        /// day is what the operator meant. A value that does not parse is treated as "no change" -
        /// the transport refuses a malformed date before this point, so reaching here means the
        /// body carried one and the honest answer is the date on the row.
        /// </remarks>
        private static DateTimeOffset? ParseExpiry(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return DateTimeOffset.TryParse(
                value.Trim(),
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal
                    | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var parsed)
                ? parsed
                : null;
        }

        /// <summary>
        /// Maps a unique violation to the field of the index that produced it, and rethrows
        /// anything else.
        /// </summary>
        /// <remarks>
        /// Three indexes can refuse this table's write and each is about one field. A violation
        /// from an index this method does not recognise is a bug rather than a duplicate, so it is
        /// rethrown instead of being dressed up as one - the rule the repository section states.
        /// </remarks>
        private static SubscriptionOutcome MapViolation(
            Core.Persistence.Exceptions.UniqueViolationException violation,
            Guid subscriptionId)
        {
            if (violation.IsConstraint(SubscriptionRules.PlaylistIdIndexName))
            {
                return AlreadyExists("PlaylistId", subscriptionId);
            }

            if (violation.IsConstraint(SubscriptionRules.MagMacIndexName))
            {
                return AlreadyExists("MagMac", subscriptionId);
            }

            if (violation.IsConstraint(SubscriptionRules.Enigma2MacIndexName))
            {
                return AlreadyExists("Enigma2Mac", subscriptionId);
            }

            throw violation;
        }

        private static void Merge(
            Dictionary<string, string[]> source,
            Dictionary<string, string[]> target)
        {
            foreach (var entry in source)
            {
                target[entry.Key] = entry.Value;
            }
        }

        private static SubscriptionOutcome NotFound(Guid id)
            => new() { Action = SubscriptionAction.NotFound, SubscriptionId = id };

        private static SubscriptionOutcome AlreadyExists(string field, Guid id = default)
            => new()
            {
                Action = SubscriptionAction.AlreadyExists,
                SubscriptionId = id,
                Errors = new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    [field] = ["Another subscription already holds that value."]
                }
            };

        private static SubscriptionOutcome Invalid(
            IReadOnlyDictionary<string, string[]> errors,
            Guid id = default)
            => new() { Action = SubscriptionAction.Invalid, SubscriptionId = id, Errors = errors };
    }
}
