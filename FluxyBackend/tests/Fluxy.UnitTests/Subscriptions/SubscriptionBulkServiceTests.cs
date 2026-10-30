using Fluxy.Application.Subscriptions.Services;
using Fluxy.Core.Subscriptions.Commands;
using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Subscriptions.Interfaces;
using Fluxy.Core.Subscriptions.Outcomes;
using Fluxy.Core.Subscriptions.ReadModels;
using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.Interfaces;
using Fluxy.Core.Users.Outcomes;
using Fluxy.Core.Users.ReadModels;

namespace Fluxy.UnitTests.Subscriptions
{
    /// <summary>
    /// The rules the bulk operation owns: the body it cannot run without, the plan change that
    /// refuses the row whose last service would be cleared, and the owner block that reuses the
    /// account block.
    /// </summary>
    /// <remarks>
    /// The two services it delegates to are faked: what is under test is the collective layer, and
    /// the single-row rules are exercised by <c>SubscriptionServiceTests</c>. The fakes record what
    /// they were asked to do, which is how a test proves the operation called the account block
    /// with the caller's own grants rather than with a set it invented.
    /// </remarks>
    public sealed class SubscriptionBulkServiceTests
    {
        private static readonly Guid Actor = Guid.NewGuid();

        [Fact]
        public async Task APlanChangeWithoutADestinationIsRefusedForEveryRow()
        {
            var subscriptions = new FakeSubscriptions();
            var service = NewService(subscriptions, new FakeUsers());

            var outcome = await service.BulkAsync(
                Operation(BulkSubscriptionAction.ChangeTariff, [Guid.NewGuid(), Guid.NewGuid()]),
                Actor,
                Grants(),
                TestContext.Current.CancellationToken);

            Assert.Equal(2, outcome.Items.Count);
            Assert.All(outcome.Items, item =>
            {
                Assert.Equal(SubscriptionAction.Invalid, item.Action);
                Assert.True(item.Errors!.ContainsKey(nameof(BulkSubscriptionOperation.TariffPlanId)));
            });

            // No row was touched: the refusal is about the body, not about the selection.
            Assert.Empty(subscriptions.Updated);
        }

        [Fact]
        public async Task ACreatorChangeWithoutAnIssuerIsRefusedForEveryRow()
        {
            var service = NewService(new FakeSubscriptions(), new FakeUsers());

            var outcome = await service.BulkAsync(
                Operation(BulkSubscriptionAction.ChangeCreator, [Guid.NewGuid()]),
                Actor,
                Grants(),
                TestContext.Current.CancellationToken);

            Assert.Single(outcome.Items);
            Assert.Equal(SubscriptionAction.Invalid, outcome.Items[0].Action);
            Assert.True(outcome.Items[0].Errors!
                .ContainsKey(nameof(BulkSubscriptionOperation.ResellerId)));
        }

        [Fact]
        public async Task APlanChangeReportsEachServiceItClearedAsAWarning()
        {
            var id = Guid.NewGuid();
            var plan = Guid.NewGuid();
            var detail = Detail(id, playlistId: "abc", magMac: "AA:BB:CC:DD:EE:FF");
            var cleared = Detail(id, playlistId: null, magMac: null);

            var subscriptions = new FakeSubscriptions
            {
                Details = { [id] = [detail, cleared] }
            };
            var service = NewService(subscriptions, new FakeUsers());

            var outcome = await service.BulkAsync(
                Operation(BulkSubscriptionAction.ChangeTariff, [id], planId: plan),
                Actor,
                Grants(),
                TestContext.Current.CancellationToken);

            var item = Assert.Single(outcome.Items);
            Assert.Equal(SubscriptionAction.Updated, item.Action);
            Assert.NotNull(item.Warnings);
            Assert.Contains(SubscriptionBulkWarning.PlaylistCleared, item.Warnings);
            Assert.Contains(SubscriptionBulkWarning.MagCleared, item.Warnings);
            Assert.DoesNotContain(SubscriptionBulkWarning.Enigma2Cleared, item.Warnings);
        }

        [Fact]
        public async Task APlanChangeThatWouldClearTheOnlyServiceFailsThatRowAndTheRestContinue()
        {
            var refused = Guid.NewGuid();
            var accepted = Guid.NewGuid();
            var plan = Guid.NewGuid();

            var subscriptions = new FakeSubscriptions
            {
                Details =
                {
                    [refused] = [Detail(refused, playlistId: "abc")],
                    [accepted] = [Detail(accepted)]
                },
                UpdateOutcomes =
                {
                    [refused] = new SubscriptionOutcome
                    {
                        Action = SubscriptionAction.Invalid,
                        SubscriptionId = refused,
                        Errors = new Dictionary<string, string[]>(StringComparer.Ordinal)
                        {
                            ["PlaylistId"] = ["A subscription must deliver a service."]
                        }
                    },
                    [accepted] = new SubscriptionOutcome
                    {
                        Action = SubscriptionAction.Updated,
                        SubscriptionId = accepted
                    }
                }
            };
            var service = NewService(subscriptions, new FakeUsers());

            var outcome = await service.BulkAsync(
                Operation(BulkSubscriptionAction.ChangeTariff, [refused, accepted], planId: plan),
                Actor,
                Grants(),
                TestContext.Current.CancellationToken);

            Assert.Equal(2, outcome.Items.Count);
            Assert.Equal(SubscriptionAction.Invalid, outcome.Items[0].Action);
            Assert.Null(outcome.Items[0].Warnings);
            Assert.Equal(SubscriptionAction.Updated, outcome.Items[1].Action);
        }

        [Fact]
        public async Task APlanChangeOnAMissingSubscriptionIsReportedAsNotFound()
        {
            var service = NewService(new FakeSubscriptions(), new FakeUsers());

            var outcome = await service.BulkAsync(
                Operation(BulkSubscriptionAction.ChangeTariff, [Guid.NewGuid()], planId: Guid.NewGuid()),
                Actor,
                Grants(),
                TestContext.Current.CancellationToken);

            Assert.Single(outcome.Items);
            Assert.Equal(SubscriptionAction.NotFound, outcome.Items[0].Action);
        }

        [Theory]
        [InlineData(BulkSubscriptionAction.DeletePlaylist)]
        [InlineData(BulkSubscriptionAction.DeleteMag)]
        [InlineData(BulkSubscriptionAction.DeleteEnigma2)]
        public async Task AFieldClearIsTheSingleRowUpdateAndSucceedsOnAnAlreadyEmptyField(
            BulkSubscriptionAction action)
        {
            var id = Guid.NewGuid();
            var subscriptions = new FakeSubscriptions
            {
                UpdateOutcomes =
                {
                    [id] = new SubscriptionOutcome
                    {
                        Action = SubscriptionAction.Updated,
                        SubscriptionId = id
                    }
                }
            };
            var service = NewService(subscriptions, new FakeUsers());

            var outcome = await service.BulkAsync(
                Operation(action, [id]),
                Actor,
                Grants(),
                TestContext.Current.CancellationToken);

            Assert.Single(outcome.Items);
            Assert.Equal(SubscriptionAction.Updated, outcome.Items[0].Action);
            Assert.Single(subscriptions.Updated);
        }

        [Fact]
        public async Task TheOwnerBlockGoesThroughTheAccountBlockWithTheCallersOwnGrants()
        {
            var id = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var subscriptions = new FakeSubscriptions
            {
                Details = { [id] = [Detail(id, ownerId: ownerId)] }
            };
            var users = new FakeUsers { Outcome = new AdminUserOutcome { Action = AdminUserAction.Blocked } };
            var service = NewService(subscriptions, users);

            var outcome = await service.BulkAsync(
                Operation(BulkSubscriptionAction.BlockOwner, [id]),
                Actor,
                Grants(UserPermission.EditClients),
                TestContext.Current.CancellationToken);

            Assert.Single(outcome.Items);
            Assert.Equal(SubscriptionAction.Updated, outcome.Items[0].Action);
            Assert.Equal(ownerId, users.BlockedUserId);
            Assert.True(users.ReceivedGrants!.Contains(UserPermission.EditClients));
        }

        [Fact]
        public async Task TheOwnerBlockReportsAnAlreadyBlockedOwnerTheSameWay()
        {
            // The block is idempotent by its own rule: an owner already blocked answers `Blocked`.
            var id = Guid.NewGuid();
            var subscriptions = new FakeSubscriptions { Details = { [id] = [Detail(id)] } };
            var users = new FakeUsers { Outcome = new AdminUserOutcome { Action = AdminUserAction.Blocked } };
            var service = NewService(subscriptions, users);

            var outcome = await service.BulkAsync(
                Operation(BulkSubscriptionAction.BlockOwner, [id]),
                Actor,
                Grants(),
                TestContext.Current.CancellationToken);

            Assert.Equal(SubscriptionAction.Updated, outcome.Items[0].Action);
        }

        [Fact]
        public async Task BlockingTheActingUsersOwnAccountIsRefusedForThatRow()
        {
            var id = Guid.NewGuid();
            var subscriptions = new FakeSubscriptions { Details = { [id] = [Detail(id, ownerId: Actor)] } };
            var users = new FakeUsers
            {
                Outcome = new AdminUserOutcome { Action = AdminUserAction.CannotBlockSelf }
            };
            var service = NewService(subscriptions, users);

            var outcome = await service.BulkAsync(
                Operation(BulkSubscriptionAction.BlockOwner, [id]),
                Actor,
                Grants(),
                TestContext.Current.CancellationToken);

            Assert.Single(outcome.Items);
            Assert.Equal(SubscriptionAction.Invalid, outcome.Items[0].Action);
            Assert.True(outcome.Items[0].Errors!.ContainsKey("UserId"));
        }

        [Fact]
        public async Task AnOwnerBlockWithoutTheAccountGrantIsRefusedForThatRow()
        {
            var id = Guid.NewGuid();
            var subscriptions = new FakeSubscriptions { Details = { [id] = [Detail(id)] } };
            var users = new FakeUsers
            {
                Outcome = new AdminUserOutcome { Action = AdminUserAction.PermissionDenied }
            };
            var service = NewService(subscriptions, users);

            var outcome = await service.BulkAsync(
                Operation(BulkSubscriptionAction.BlockOwner, [id]),
                Actor,
                Grants(),
                TestContext.Current.CancellationToken);

            Assert.Equal(SubscriptionAction.Invalid, outcome.Items[0].Action);
            Assert.True(outcome.Items[0].Errors!.ContainsKey("UserId"));
        }

        [Fact]
        public async Task AProlongDelegatesToTheSingleRowServiceAndReportsItsCode()
        {
            // The bulk action is the single-row write in a loop (design decision 14), so the row
            // answers with `subscription_prolonged` and the no-plan refusal comes from the same
            // service rather than a second copy of the rule.
            var id = Guid.NewGuid();
            var subscriptions = new FakeSubscriptions { Details = { [id] = [Detail(id)] } };
            var service = NewService(subscriptions, new FakeUsers());

            var outcome = await service.BulkAsync(
                Operation(BulkSubscriptionAction.Prolong, [id]),
                Actor,
                Grants(),
                TestContext.Current.CancellationToken);

            Assert.Equal(SubscriptionAction.Prolonged, outcome.Items[0].Action);
            Assert.Contains(id, subscriptions.Prolonged);
            Assert.DoesNotContain(id, subscriptions.Updated);
        }

        [Fact]
        public async Task ABulkProlongOfAPlanLessRowReportsTheSingleRowRefusal()
        {
            var id = Guid.NewGuid();
            var subscriptions = new FakeSubscriptions
            {
                ProlongOutcomes =
                {
                    [id] = new SubscriptionOutcome
                    {
                        Action = SubscriptionAction.Invalid,
                        SubscriptionId = id,
                        Errors = new Dictionary<string, string[]>(StringComparer.Ordinal)
                        {
                            ["TariffPlanId"] = ["A plan is required to prolong."]
                        }
                    }
                }
            };
            var service = NewService(subscriptions, new FakeUsers());

            var outcome = await service.BulkAsync(
                Operation(BulkSubscriptionAction.Prolong, [id]),
                Actor,
                Grants(),
                TestContext.Current.CancellationToken);

            Assert.Equal(SubscriptionAction.Invalid, outcome.Items[0].Action);
            Assert.True(outcome.Items[0].Errors!.ContainsKey("TariffPlanId"));
        }

        [Fact]
        public async Task ADeleteReportsTheSingleRowCode()
        {
            var id = Guid.NewGuid();
            var subscriptions = new FakeSubscriptions
            {
                DeleteOutcomes =
                {
                    [id] = new SubscriptionOutcome
                    {
                        Action = SubscriptionAction.Deleted,
                        SubscriptionId = id
                    }
                }
            };
            var service = NewService(subscriptions, new FakeUsers());

            var outcome = await service.BulkAsync(
                Operation(BulkSubscriptionAction.Delete, [id]),
                Actor,
                Grants(),
                TestContext.Current.CancellationToken);

            Assert.Equal(SubscriptionAction.Deleted, outcome.Items[0].Action);
        }

        private static SubscriptionBulkService NewService(
            FakeSubscriptions subscriptions,
            FakeUsers users)
            => new(new FakeSubscriptionService(subscriptions), users);

        private static BulkSubscriptionOperation Operation(
            BulkSubscriptionAction action,
            IReadOnlyList<Guid> ids,
            Guid? planId = null)
            => new()
            {
                Action = action,
                SubscriptionIds = ids,
                TariffPlanId = planId
            };

        private static IReadOnlySet<UserPermission> Grants(params UserPermission[] granted)
            => granted.ToHashSet();

        private static SubscriptionDetail Detail(
            Guid id,
            Guid? ownerId = null,
            string? playlistId = "0123456789abcdef0123456789abcdef",
            string? magMac = null,
            string? enigma2Mac = null)
            => new()
            {
                Id = id,
                UserId = ownerId ?? Guid.NewGuid(),
                Username = "owner",
                ResellerId = null,
                CreatorUsername = null,
                PlaylistId = playlistId,
                ExpireAt = null,
                TariffPlanId = null,
                TariffPlanName = null,
                Status = SubscriptionStatus.Active,
                MaxConnections = null,
                IsRestreamer = false,
                IpWhitelist = [],
                UserAgentWhitelist = [],
                IsIspLock = false,
                IsCountryLock = false,
                IsTrial = false,
                OutputFormats = SubscriptionOutputFormat.MpegTs,
                MagMac = magMac,
                IsStbLock = false,
                Enigma2Mac = enigma2Mac,
                BouquetIds = [],
                CreatedAt = DateTimeOffset.UtcNow,
                Derived = null
            };

        /// <summary>
        /// The single-row service as the bulk operation sees it: the detail reads, the updates, the
        /// deletes and the identifier the tests set.
        /// </summary>
        private sealed class FakeSubscriptions
        {
            /// <summary>Queued details per subscription; each read takes the next, then repeats the last.</summary>
            public Dictionary<Guid, List<SubscriptionDetail>> Details { get; } = [];

            public Dictionary<Guid, SubscriptionOutcome> UpdateOutcomes { get; } = [];

            public Dictionary<Guid, SubscriptionOutcome> DeleteOutcomes { get; } = [];

            public Dictionary<Guid, SubscriptionOutcome> ProlongOutcomes { get; } = [];

            public List<Guid> Updated { get; } = [];

            public List<Guid> Deleted { get; } = [];

            public List<Guid> Prolonged { get; } = [];

            private readonly Dictionary<Guid, int> _reads = [];

            /// <summary>How many times the detail of one subscription has been read.</summary>
            public int ReadCount(Guid id) => _reads.GetValueOrDefault(id);

            /// <summary>Records one read, so the next one can answer with the "after" row.</summary>
            public void Advance(Guid id) => _reads[id] = ReadCount(id) + 1;
        }

        /// <summary>
        /// The single-row service behind the bulk one. It answers from the fake repository and
        /// records what it was asked, so a test can prove no row was touched by a refused body.
        /// </summary>
        private sealed class FakeSubscriptionService : ISubscriptionService
        {
            private readonly FakeSubscriptions _inner;

            public FakeSubscriptionService(FakeSubscriptions inner) => _inner = inner;

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
                => throw new NotSupportedException();

            public Task<SubscriptionDetail?> GetAsync(
                Guid id,
                CancellationToken cancellationToken = default)
            {
                if (!_inner.Details.TryGetValue(id, out var queue) || queue.Count == 0)
                {
                    return Task.FromResult<SubscriptionDetail?>(null);
                }

                // Each read advances, so the "before" and "after" of a plan change can differ.
                var index = Math.Min(_inner.ReadCount(id), queue.Count - 1);
                _inner.Advance(id);

                return Task.FromResult<SubscriptionDetail?>(queue[index]);
            }

            public Task<SubscriptionOutcome> UpdateAsync(
                Guid id,
                SubscriptionPatch patch,
                CancellationToken cancellationToken = default)
            {
                _inner.Updated.Add(id);

                if (_inner.UpdateOutcomes.TryGetValue(id, out var outcome))
                {
                    return Task.FromResult(outcome);
                }

                return Task.FromResult(new SubscriptionOutcome
                {
                    Action = SubscriptionAction.Updated,
                    SubscriptionId = id
                });
            }

            public Task<SubscriptionOutcome> DeleteAsync(
                Guid id,
                CancellationToken cancellationToken = default)
            {
                _inner.Deleted.Add(id);

                return Task.FromResult(_inner.DeleteOutcomes.TryGetValue(id, out var outcome)
                    ? outcome
                    : new SubscriptionOutcome
                    {
                        Action = SubscriptionAction.Deleted,
                        SubscriptionId = id
                    });
            }

            /// <summary>
            /// Prolongation as the bulk layer sees it: delegated, so the fake answers the outcome a
            /// test queued and records that the single-row service was asked.
            /// </summary>
            public Task<SubscriptionOutcome> ProlongAsync(
                Guid id,
                CancellationToken cancellationToken = default)
            {
                _inner.Prolonged.Add(id);

                return Task.FromResult(_inner.ProlongOutcomes.TryGetValue(id, out var outcome)
                    ? outcome
                    : new SubscriptionOutcome
                    {
                        Action = SubscriptionAction.Prolonged,
                        SubscriptionId = id
                    });
            }

            public Task<SubscriptionOutcome> CreateAsync(
                NewSubscription input,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<string> GeneratePlaylistIdAsync(CancellationToken cancellationToken = default)
                => throw new NotSupportedException();
        }

        /// <summary>The account block as the bulk operation sees it.</summary>
        private sealed class FakeUsers : IUserAdminService
        {
            public AdminUserOutcome Outcome { get; init; } =
                new() { Action = AdminUserAction.Blocked };

            public Guid? BlockedUserId { get; private set; }

            public IReadOnlySet<UserPermission>? ReceivedGrants { get; private set; }

            public Task<AdminUserOutcome> SetBlockedAsync(
                Guid userId,
                bool blocked,
                Guid actingUserId,
                IReadOnlySet<UserPermission> granted,
                CancellationToken cancellationToken = default)
            {
                BlockedUserId = userId;
                ReceivedGrants = granted;

                return Task.FromResult(Outcome with { UserId = userId });
            }

            public Task<AdminUserPage> GetPageAsync(
                int page,
                int pageSize,
                string? search,
                Guid? groupId,
                UserStatus? status,
                AdminUserSortField sortBy,
                AdminUserSortOrder sortOrder,
                IReadOnlySet<UserPermission> granted,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<AdminUserReadResult> GetAsync(
                Guid userId,
                IReadOnlySet<UserPermission> granted,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<AdminUserOutcome> CreateAsync(
                Core.Users.Commands.NewUser user,
                IReadOnlySet<UserPermission> granted,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<AdminUserOutcome> UpdateAsync(
                Guid userId,
                Core.Users.Commands.UserPatch patch,
                Guid actingUserId,
                IReadOnlySet<UserPermission> granted,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<AdminUserOutcome> ConfirmRegistrationAsync(
                Guid userId,
                IReadOnlySet<UserPermission> granted,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<AdminUserOutcome> DeleteAsync(
                Guid userId,
                Guid actingUserId,
                IReadOnlySet<UserPermission> granted,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();
        }
    }
}
