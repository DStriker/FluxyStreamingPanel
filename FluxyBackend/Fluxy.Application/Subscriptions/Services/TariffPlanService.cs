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

namespace Fluxy.Application.Subscriptions.Services
{
    /// <summary>
    /// Reads the tariff plans and changes them on an administrator's behalf.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A plan is a price with a length and a delivery shape, plus two relations it only points
    /// at. Everything that can be judged without the database is judged by
    /// <see cref="TariffPlanRules"/>; what is left here is the merging of a patch into the row,
    /// the name probe and the check that every identifier in the two relations names a row that
    /// still exists.
    /// </para>
    /// <para>
    /// Nothing here knows about HTTP. Which <see cref="TariffPlanAction"/> becomes a 201 and
    /// which a 409 is the transport layer's business.
    /// </para>
    /// </remarks>
    public sealed class TariffPlanService : ITariffPlanService
    {
        private readonly ITariffPlanRepository _plans;
        private readonly IUnitOfWork _unitOfWork;

        /// <summary>
        /// Initializes a new instance of the <see cref="TariffPlanService"/> class.
        /// </summary>
        /// <param name="plans">Repository holding the plans.</param>
        /// <param name="unitOfWork">Commits the changes one operation stages.</param>
        public TariffPlanService(ITariffPlanRepository plans, IUnitOfWork unitOfWork)
        {
            _plans = plans;
            _unitOfWork = unitOfWork;
        }

        /// <inheritdoc />
        public Task<TariffPlanPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            TariffPlanSortField sortBy,
            TariffPlanSortOrder sortOrder,
            CancellationToken cancellationToken = default)
            => _plans.GetPageAsync(page, pageSize, search, sortBy, sortOrder, cancellationToken);

        /// <inheritdoc />
        public Task<TariffPlanDetail?> GetAsync(
            Guid id,
            CancellationToken cancellationToken = default)
            => _plans.GetAsync(id, cancellationToken);

        /// <inheritdoc />
        public async Task<TariffPlanOutcome> CreateAsync(
            NewTariffPlan input,
            CancellationToken cancellationToken = default)
        {
            var name = input.Name.Trim();

            var errors = TariffPlanRules.Validate(
                name,
                input.IsTrial,
                input.Cost,
                input.DurationValue,
                input.DurationUnit,
                input.SupportsM3u,
                input.SupportsMag,
                input.SupportsEnigma2,
                input.OutputMpegTs,
                input.OutputHls,
                input.StrictStbDeviceId,
                input.MaxConnections);

            MergeLinkErrors(input.UserGroupIds, input.BouquetIds, errors);

            if (errors.Count > 0)
            {
                return Invalid(errors);
            }

            if (await _plans.IsNameTakenAsync(name, exceptPlan: null, cancellationToken))
            {
                return AlreadyExists(nameof(NewTariffPlan.Name));
            }

            var userGroupIds = await ExistingUserGroupIdsAsync(
                await DefaultGroupIdsAsync(input.UserGroupIds, cancellationToken),
                errors,
                cancellationToken);

            var bouquetIds = await ExistingBouquetIdsAsync(input.BouquetIds, errors, cancellationToken);

            // A plan nothing may buy and a plan that delivers nothing are both refusals rather
            // than rows, and they are judged after the identifier probes so that a body naming an
            // identifier that does not exist is answered about the identifier.
            if (userGroupIds.Count == 0 && !errors.ContainsKey(nameof(NewTariffPlan.UserGroupIds)))
            {
                errors[nameof(NewTariffPlan.UserGroupIds)] = TariffPlanRules.UserGroupsRequiredRule;
            }

            if (bouquetIds.Count == 0 && !errors.ContainsKey(nameof(NewTariffPlan.BouquetIds)))
            {
                errors[nameof(NewTariffPlan.BouquetIds)] = TariffPlanRules.BouquetsRequiredRule;
            }

            if (errors.Count > 0)
            {
                return Invalid(errors);
            }

            var planId = await _plans.AddAsync(
                new NewTariffPlan
                {
                    Name = name,
                    IsTrial = input.IsTrial,
                    Cost = input.Cost,
                    DurationValue = input.DurationValue,
                    DurationUnit = input.DurationUnit,
                    SupportsM3u = input.SupportsM3u,
                    SupportsMag = input.SupportsMag,
                    SupportsEnigma2 = input.SupportsEnigma2,
                    OutputMpegTs = input.OutputMpegTs,
                    OutputHls = input.OutputHls,
                    StrictStbDeviceId = input.StrictStbDeviceId,
                    AllowRestream = input.AllowRestream,
                    MaxConnections = input.MaxConnections,
                    AllowMultipleCountries = input.AllowMultipleCountries
                },
                userGroupIds,
                bouquetIds,
                cancellationToken);

            if (await _unitOfWork.TrySaveAsync(cancellationToken) is { } createFailure)
            {
                // Only the name index may be answered as a name conflict. Any other unique
                // violation is a bug in this service, and dressing it up as a taken name would
                // hide it behind a sentence about something else entirely.
                return createFailure.IsConstraint(TariffPlanRules.NameIndexName)
                    ? AlreadyExists(nameof(NewTariffPlan.Name))
                    : throw createFailure;
            }

            return new TariffPlanOutcome
            {
                Action = TariffPlanAction.Created,
                PlanId = planId
            };
        }

        /// <inheritdoc />
        public async Task<TariffPlanOutcome> UpdateAsync(
            Guid id,
            TariffPlanPatch patch,
            CancellationToken cancellationToken = default)
        {
            var current = await _plans.FindForUpdateAsync(id, cancellationToken);

            if (current is null)
            {
                return NotFound(id);
            }

            // Merged first and judged afterwards, so the rules see the row as it is about to
            // become rather than as it was. A patch that turns MAG support off while leaving the
            // strict STB check on is therefore refused as the combination it would produce.
            var merged = new TariffPlan
            {
                Id = current.Id,
                Name = patch.Name is null ? current.Name : patch.Name.Trim(),
                IsTrial = patch.IsTrial ?? current.IsTrial,
                Cost = patch.Cost ?? current.Cost,
                DurationValue = patch.DurationValue ?? current.DurationValue,
                DurationUnit = patch.DurationUnit ?? current.DurationUnit,
                SupportsM3u = patch.SupportsM3u ?? current.SupportsM3u,
                SupportsMag = patch.SupportsMag ?? current.SupportsMag,
                SupportsEnigma2 = patch.SupportsEnigma2 ?? current.SupportsEnigma2,
                OutputMpegTs = patch.OutputMpegTs ?? current.OutputMpegTs,
                OutputHls = patch.OutputHls ?? current.OutputHls,
                StrictStbDeviceId = patch.StrictStbDeviceId ?? current.StrictStbDeviceId,
                AllowRestream = patch.AllowRestream ?? current.AllowRestream,
                MaxConnections = patch.MaxConnections ?? current.MaxConnections,
                AllowMultipleCountries = patch.AllowMultipleCountries ?? current.AllowMultipleCountries
            };

            var errors = TariffPlanRules.Validate(
                merged.Name,
                merged.IsTrial,
                merged.Cost,
                merged.DurationValue,
                merged.DurationUnit,
                merged.SupportsM3u,
                merged.SupportsMag,
                merged.SupportsEnigma2,
                merged.OutputMpegTs,
                merged.OutputHls,
                merged.StrictStbDeviceId,
                merged.MaxConnections);

            MergeLinkErrors(patch.UserGroupIds, patch.BouquetIds, errors);

            if (errors.Count > 0)
            {
                return Invalid(errors, id);
            }

            if (await _plans.IsNameTakenAsync(merged.Name, id, cancellationToken))
            {
                return AlreadyExists(nameof(TariffPlanPatch.Name), id);
            }

            var userGroupIds = patch.UserGroupIds is null
                ? null
                : await ExistingUserGroupIdsAsync(patch.UserGroupIds, errors, cancellationToken);

            var bouquetIds = patch.BouquetIds is null
                ? null
                : await ExistingBouquetIdsAsync(patch.BouquetIds, errors, cancellationToken);

            // The two minimums, judged on the set the plan would end up with rather than on the
            // body: a patch that names `[]` clears the links, and a patch that names nothing
            // keeps whatever the row already holds. A plan the row already has links for is not
            // refused because this body did not restate them.
            if (userGroupIds is { Count: 0 } && !errors.ContainsKey(nameof(NewTariffPlan.UserGroupIds)))
            {
                errors[nameof(NewTariffPlan.UserGroupIds)] = TariffPlanRules.UserGroupsRequiredRule;
            }

            if (bouquetIds is { Count: 0 } && !errors.ContainsKey(nameof(NewTariffPlan.BouquetIds)))
            {
                errors[nameof(NewTariffPlan.BouquetIds)] = TariffPlanRules.BouquetsRequiredRule;
            }

            if (errors.Count > 0)
            {
                return Invalid(errors, id);
            }

            if (await _plans.UpdateAsync(id, merged, cancellationToken) is false)
            {
                return NotFound(id);
            }

            if (userGroupIds is not null)
            {
                await _plans.SetUserGroupsAsync(id, userGroupIds, cancellationToken);
            }

            if (bouquetIds is not null)
            {
                await _plans.SetBouquetsAsync(id, bouquetIds, cancellationToken);
            }

            if (await _unitOfWork.TrySaveAsync(cancellationToken) is { } updateFailure)
            {
                return updateFailure.IsConstraint(TariffPlanRules.NameIndexName)
                    ? AlreadyExists(nameof(TariffPlanPatch.Name), id)
                    : throw updateFailure;
            }

            return new TariffPlanOutcome
            {
                Action = TariffPlanAction.Updated,
                PlanId = id
            };
        }

        /// <inheritdoc />
        public async Task<TariffPlanOutcome> DeleteAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            if (await _plans.RemoveAsync(id, cancellationToken) is false)
            {
                return NotFound(id);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new TariffPlanOutcome
            {
                Action = TariffPlanAction.Deleted,
                PlanId = id
            };
        }

        /// <summary>
        /// Rejects a relation list that names the same identifier twice before the database does.
        /// </summary>
        /// <remarks>
        /// A duplicate is refused rather than de-duplicated: the caller sent a set that contains
        /// the same row twice, and silently storing one of them would answer a request the
        /// operator did not make.
        /// </remarks>
        private static void MergeLinkErrors(
            IReadOnlyList<Guid>? userGroupIds,
            IReadOnlyList<Guid>? bouquetIds,
            Dictionary<string, string[]> errors)
        {
            if (userGroupIds is not null && userGroupIds.Count != userGroupIds.Distinct().Count())
            {
                errors[nameof(NewTariffPlan.UserGroupIds)] = TariffPlanRules.UserGroupIdsRule;
            }

            if (bouquetIds is not null && bouquetIds.Count != bouquetIds.Distinct().Count())
            {
                errors[nameof(NewTariffPlan.BouquetIds)] = TariffPlanRules.BouquetIdsRule;
            }
        }

        /// <summary>
        /// Resolves the group list a create starts from.
        /// </summary>
        /// <remarks>
        /// An absent list is not "no group": it is "the operator has not decided yet", and the
        /// domain's answer to that is every base group - a plan that arrives available to
        /// administrators, resellers and clients alike. A list that is present, even an empty
        /// one, is the operator's own statement and is left to the minimum-one rule below.
        /// </remarks>
        private async Task<IReadOnlyList<Guid>> DefaultGroupIdsAsync(
            IReadOnlyList<Guid>? userGroupIds,
            CancellationToken cancellationToken)
        {
            if (userGroupIds is not null)
            {
                return userGroupIds;
            }

            return await _plans.GetBaseUserGroupIdsAsync(cancellationToken);
        }

        /// <summary>Resolves the group list and records the identifiers that name nothing.</summary>
        private async Task<IReadOnlyList<Guid>> ExistingUserGroupIdsAsync(
            IReadOnlyList<Guid>? userGroupIds,
            Dictionary<string, string[]> errors,
            CancellationToken cancellationToken)
        {
            if (userGroupIds is null || userGroupIds.Count == 0)
            {
                return [];
            }

            var existing = await _plans.GetExistingUserGroupIdsAsync(userGroupIds, cancellationToken);

            if (existing.Count != userGroupIds.Distinct().Count())
            {
                errors[nameof(NewTariffPlan.UserGroupIds)] = TariffPlanRules.UserGroupIdsRule;
            }

            return userGroupIds.Distinct().ToList();
        }

        /// <summary>Resolves the bouquet list and records the identifiers that name nothing.</summary>
        private async Task<IReadOnlyList<Guid>> ExistingBouquetIdsAsync(
            IReadOnlyList<Guid>? bouquetIds,
            Dictionary<string, string[]> errors,
            CancellationToken cancellationToken)
        {
            if (bouquetIds is null || bouquetIds.Count == 0)
            {
                return [];
            }

            var existing = await _plans.GetExistingBouquetIdsAsync(bouquetIds, cancellationToken);

            if (existing.Count != bouquetIds.Distinct().Count())
            {
                errors[nameof(NewTariffPlan.BouquetIds)] = TariffPlanRules.BouquetIdsRule;
            }

            return bouquetIds.Distinct().ToList();
        }

        private static TariffPlanOutcome NotFound(Guid id)
            => new() { Action = TariffPlanAction.NotFound, PlanId = id };

        private static TariffPlanOutcome AlreadyExists(string field, Guid id = default)
            => new()
            {
                Action = TariffPlanAction.AlreadyExists,
                PlanId = id,
                Errors = Field(field, ["Another tariff plan already has that name."])
            };

        private static TariffPlanOutcome Invalid(
            IReadOnlyDictionary<string, string[]> errors,
            Guid id = default)
            => new() { Action = TariffPlanAction.Invalid, PlanId = id, Errors = errors };

        private static Dictionary<string, string[]> Field(string name, string[] sentences)
            => new(StringComparer.Ordinal) { [name] = sentences };
    }
}
