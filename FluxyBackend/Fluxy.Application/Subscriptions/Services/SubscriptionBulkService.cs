using Fluxy.Core.Subscriptions.Commands;
using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Subscriptions.Interfaces;
using Fluxy.Core.Subscriptions.Outcomes;
using Fluxy.Core.Subscriptions.Utils;
using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.Interfaces;
using Fluxy.Core.Users.Outcomes;

namespace Fluxy.Application.Subscriptions.Services
{
    /// <summary>
    /// Applies one operation to a set of subscriptions, one subscription at a time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The bulk endpoint is not a second implementation of the single-row operations, it is those
    /// operations applied to each row in turn. A plan change is still the save that derives its
    /// parameters and clears the services the plan cannot deliver, and a creator change is still
    /// the rule that only an administrator or a reseller may issue a subscription.
    /// </para>
    /// <para>
    /// Three rules live here because they are about the <i>collective</i> operation rather than
    /// about a subscription: a plan change reports each service it cleared as a warning, refuses
    /// the row whose only service it would clear, and the owner block reuses the account block -
    /// including its per-target reach and its self-protection.
    /// </para>
    /// </remarks>
    public sealed class SubscriptionBulkService : ISubscriptionBulkService
    {
        /// <summary>The sentence a plan change without a destination gets.</summary>
        private static readonly string[] PlanRule =
        [
            "A bulk plan change needs the tariff plan to attach."
        ];

        /// <summary>The sentence a creator change without an issuer gets.</summary>
        private static readonly string[] CreatorRule =
        [
            "A bulk creator change needs the account to assign."
        ];

        private readonly ISubscriptionService _subscriptions;
        private readonly IUserAdminService _users;

        /// <summary>
        /// Initializes a new instance of the <see cref="SubscriptionBulkService"/> class.
        /// </summary>
        /// <param name="subscriptions">Service holding the single-subscription operations.</param>
        /// <param name="users">
        /// Admin service the owner block goes through, so a blocked owner loses its sessions
        /// exactly as it does from the accounts screen.
        /// </param>
        public SubscriptionBulkService(
            ISubscriptionService subscriptions,
            IUserAdminService users)
        {
            _subscriptions = subscriptions;
            _users = users;
        }

        /// <inheritdoc />
        public async Task<BulkSubscriptionOutcome> BulkAsync(
            BulkSubscriptionOperation operation,
            Guid actingUserId,
            IReadOnlySet<UserPermission> granted,
            CancellationToken cancellationToken = default)
        {
            // A payload an operation cannot run without is refused for every row rather than
            // ignored. The transport refuses the same mistakes with a 400 before any row is read;
            // if one ever arrives here, every subscription is answered with the field that was
            // missing instead of the run reporting success over rows it never changed.
            if (MissingPayload(operation) is { } missing)
            {
                return RefuseAll(operation, missing.Field, missing.Sentences);
            }

            var items = new List<BulkSubscriptionItemResult>(operation.SubscriptionIds.Count);

            foreach (var subscriptionId in operation.SubscriptionIds)
            {
                items.Add(operation.Action switch
                {
                    BulkSubscriptionAction.ChangeTariff =>
                        await ChangeTariffAsync(subscriptionId, operation, cancellationToken),

                    BulkSubscriptionAction.ChangeCreator =>
                        await ChangeCreatorAsync(subscriptionId, operation, cancellationToken),

                    BulkSubscriptionAction.Delete =>
                        await RunAsync(
                            subscriptionId,
                            () => _subscriptions.DeleteAsync(subscriptionId, cancellationToken)),

                    BulkSubscriptionAction.DeletePlaylist =>
                        await RunAsync(
                            subscriptionId,
                            () => _subscriptions.UpdateAsync(
                                subscriptionId,
                                new SubscriptionPatch { PlaylistId = string.Empty },
                                cancellationToken)),

                    BulkSubscriptionAction.DeleteMag =>
                        await RunAsync(
                            subscriptionId,
                            () => _subscriptions.UpdateAsync(
                                subscriptionId,
                                new SubscriptionPatch { MagMac = string.Empty },
                                cancellationToken)),

                    BulkSubscriptionAction.DeleteEnigma2 =>
                        await RunAsync(
                            subscriptionId,
                            () => _subscriptions.UpdateAsync(
                                subscriptionId,
                                new SubscriptionPatch { Enigma2Mac = string.Empty },
                                cancellationToken)),

                    // The single-row action in a loop: the bulk run answers with the codes that
                    // endpoint gives, including its "no plan to prolong by" refusal.
                    BulkSubscriptionAction.Prolong =>
                        await RunAsync(
                            subscriptionId,
                            () => _subscriptions.ProlongAsync(subscriptionId, cancellationToken)),

                    BulkSubscriptionAction.BlockOwner =>
                        await BlockOwnerAsync(
                            subscriptionId,
                            actingUserId,
                            granted,
                            cancellationToken),

                    // Unreachable: the enum holds no other member and the transport refuses a name
                    // that maps to none. Thrown rather than skipped, because an entry missing from
                    // the answer is a row the client believes it acted on and the server never
                    // mentioned.
                    _ => throw new ArgumentOutOfRangeException(
                        nameof(operation),
                        operation.Action,
                        "Unknown bulk subscription action.")
                });
            }

            return new BulkSubscriptionOutcome { Action = operation.Action, Items = items };
        }

        /// <summary>
        /// Attaches one plan to a subscription, reporting each service the destination cannot
        /// deliver as cleared.
        /// </summary>
        /// <remarks>
        /// The row is read before the save and again after it, because the answer carries a
        /// warning for every clearance rather than only the refusal for the last one. Comparing
        /// the two reads is what makes the warnings describe what actually happened rather than
        /// what the plan's flags suggested should happen - and a refused row has no warnings at
        /// all, because nothing was written.
        /// </remarks>
        private async Task<BulkSubscriptionItemResult> ChangeTariffAsync(
            Guid subscriptionId,
            BulkSubscriptionOperation operation,
            CancellationToken cancellationToken)
        {
            var before = await _subscriptions.GetAsync(subscriptionId, cancellationToken);

            if (before is null)
            {
                return Item(subscriptionId, SubscriptionAction.NotFound);
            }

            var outcome = await _subscriptions.UpdateAsync(
                subscriptionId,
                new SubscriptionPatch { TariffPlanId = operation.TariffPlanId },
                cancellationToken);

            if (outcome.Action is not SubscriptionAction.Updated)
            {
                return Item(subscriptionId, outcome.Action, outcome.Errors);
            }

            var after = await _subscriptions.GetAsync(subscriptionId, cancellationToken);
            var warnings = new List<SubscriptionBulkWarning>();

            if (after is not null)
            {
                if (before.PlaylistId is not null && after.PlaylistId is null)
                {
                    warnings.Add(SubscriptionBulkWarning.PlaylistCleared);
                }

                if (before.MagMac is not null && after.MagMac is null)
                {
                    warnings.Add(SubscriptionBulkWarning.MagCleared);
                }

                if (before.Enigma2Mac is not null && after.Enigma2Mac is null)
                {
                    warnings.Add(SubscriptionBulkWarning.Enigma2Cleared);
                }
            }

            return Item(
                subscriptionId,
                SubscriptionAction.Updated,
                null,
                warnings.Count > 0 ? warnings : null);
        }

        /// <summary>
        /// Reassigns the issuer of one subscription, refusing an account that is neither an
        /// administrator nor a reseller.
        /// </summary>
        /// <remarks>
        /// The refusal is made by the single-row service, which reads the account the patch names
        /// and checks its role: the role of the target is a fact about that row, so the check
        /// belongs where the row is read rather than in the transport.
        /// </remarks>
        private async Task<BulkSubscriptionItemResult> ChangeCreatorAsync(
            Guid subscriptionId,
            BulkSubscriptionOperation operation,
            CancellationToken cancellationToken)
        {
            if (operation.ResellerId is not { } resellerId)
            {
                return Item(
                    subscriptionId,
                    SubscriptionAction.Invalid,
                    Field(nameof(BulkSubscriptionOperation.ResellerId), CreatorRule));
            }

            var outcome = await _subscriptions.UpdateAsync(
                subscriptionId,
                new SubscriptionPatch { ResellerId = resellerId },
                cancellationToken);

            return Item(subscriptionId, outcome.Action, outcome.Errors);
        }

        /// <summary>Runs one operation and reports how the row ended.</summary>
        private static async Task<BulkSubscriptionItemResult> RunAsync(
            Guid subscriptionId,
            Func<Task<SubscriptionOutcome>> run)
        {
            var outcome = await run();

            return Item(subscriptionId, outcome.Action, outcome.Errors);
        }

        /// <summary>
        /// Blocks the owner of one subscription through the account block.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The block is the existing account operation, not a status write of this service's own:
        /// it must revoke the owner's sessions and be idempotent, and both of those are rules the
        /// accounts area already owns. <paramref name="granted"/> is the caller's own grant set,
        /// so the block's per-target rule still decides which accounts this operator may reach -
        /// the subscription grant authorises reaching the action, and the account grant decides
        /// whose account it may touch.
        /// </para>
        /// <para>
        /// The block's self-protection is refused from <c>actingUserId</c> rather than from a
        /// grant, so blocking the acting user's own account is refused for that row whatever the
        /// caller holds - which is exactly the requirement.
        /// </para>
        /// </remarks>
        private async Task<BulkSubscriptionItemResult> BlockOwnerAsync(
            Guid subscriptionId,
            Guid actingUserId,
            IReadOnlySet<UserPermission> granted,
            CancellationToken cancellationToken)
        {
            var detail = await _subscriptions.GetAsync(subscriptionId, cancellationToken);

            if (detail is null)
            {
                return Item(subscriptionId, SubscriptionAction.NotFound);
            }

            var outcome = await _users.SetBlockedAsync(
                detail.UserId,
                blocked: true,
                actingUserId,
                granted,
                cancellationToken);

            return outcome.Action switch
            {
                // The account is blocked, or already was: both are the answer the requirement
                // calls idempotent, and neither changed the subscription row.
                AdminUserAction.Blocked => Item(subscriptionId, SubscriptionAction.Updated),

                AdminUserAction.PermissionDenied => Item(
                    subscriptionId,
                    SubscriptionAction.Invalid,
                    Field("UserId", ["Your account does not have permission for this."])),

                _ => Item(
                    subscriptionId,
                    SubscriptionAction.Invalid,
                    outcome.Errors ?? RefusalFor(outcome.Action))
            };
        }

        /// <summary>
        /// The field and sentence a row is refused with when the account block refuses it.
        /// </summary>
        private static Dictionary<string, string[]> RefusalFor(AdminUserAction action)
            => action switch
            {
                AdminUserAction.CannotBlockSelf => Field(
                    "UserId",
                    ["You cannot block your own account. Ask another administrator."]),

                AdminUserAction.NotFound => Field(
                    "UserId",
                    ["There is no account with that identifier."]),

                AdminUserAction.BlockedByGroup => Field(
                    "UserId",
                    ["That account is blocked by the group it belongs to."]),

                _ => Field(
                    "UserId",
                    ["The owner's account could not be blocked."])
            };

        /// <summary>
        /// The payload this operation cannot run without, or null when it has one.
        /// </summary>
        private static (string Field, string[] Sentences)? MissingPayload(
            BulkSubscriptionOperation operation)
            => operation.Action switch
            {
                BulkSubscriptionAction.ChangeTariff when operation.TariffPlanId is null =>
                    (nameof(BulkSubscriptionOperation.TariffPlanId), PlanRule),

                BulkSubscriptionAction.ChangeCreator when operation.ResellerId is null =>
                    (nameof(BulkSubscriptionOperation.ResellerId), CreatorRule),

                _ => ((string Field, string[] Sentences)?)null
            };

        /// <summary>Answers every named subscription with one payload refusal.</summary>
        private static BulkSubscriptionOutcome RefuseAll(
            BulkSubscriptionOperation operation,
            string field,
            string[] sentences)
            => new()
            {
                Action = operation.Action,
                Items = operation.SubscriptionIds
                    .Select(subscriptionId => Item(
                        subscriptionId,
                        SubscriptionAction.Invalid,
                        Field(field, sentences)))
                    .ToList()
            };

        private static BulkSubscriptionItemResult Item(
            Guid subscriptionId,
            SubscriptionAction action,
            IReadOnlyDictionary<string, string[]>? errors = null,
            IReadOnlyList<SubscriptionBulkWarning>? warnings = null)
            => new()
            {
                SubscriptionId = subscriptionId,
                Action = action,
                Errors = errors,
                Warnings = warnings
            };

        private static Dictionary<string, string[]> Field(string name, string[] sentences)
            => new(StringComparer.Ordinal) { [name] = sentences };
    }
}
