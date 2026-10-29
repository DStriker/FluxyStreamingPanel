using Fluxy.API.Contracts.Streams;
using Fluxy.API.Contracts.Series;
using Fluxy.API.Contracts.Subscriptions;
using Fluxy.API.Contracts.UserGroups;
using Fluxy.API.Contracts.Users;
using Fluxy.API.Contracts.Vod;
using Fluxy.Core.Series.Enums;
using Fluxy.Core.Series.Outcomes;
using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.Outcomes;
using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Subscriptions.Outcomes;
using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.Outcomes;
using Fluxy.Core.Vod.Enums;
using Fluxy.Core.Vod.Outcomes;

namespace Fluxy.API.Contracts.Common
{
    /// <summary>
    /// The one place a bulk outcome becomes a body: one code for the run as a whole, one entry
    /// per row, and a per-row code taken from the very tables the single-row endpoints read.
    /// </summary>
    /// <remarks>
    /// Nothing here decides what a row's code means. Each entry is answered by
    /// <see cref="UserAdminResponses.Describe"/> or <see cref="UserGroupResponses.Describe"/> -
    /// the same call the one-row endpoint makes - so a bulk run and a single request spell the
    /// same fact the same way, and a rule that changes one of them changes both at once. The
    /// only new thing here is the shape around them: counts, and a sentence that says how much
    /// of the run landed.
    ///
    /// <c>Ok</c> is read off the status that mapping produced rather than off the outcome word,
    /// which keeps "what a client counts as success" in the one place the statuses are already
    /// decided. It is a number comparison rather than a list of members so that a refusal
    /// added tomorrow counts as a refusal without anybody remembering to add it here.
    /// </remarks>
    internal static class BulkResponses
    {
        /// <summary>Code reported once a run over accounts has been answered for.</summary>
        public const string UsersCompletedCode = "users_bulk_completed";

        /// <summary>Code reported once a run over groups has been answered for.</summary>
        public const string GroupsCompletedCode = "user_groups_bulk_completed";

        /// <summary>Code reported once a run over streams has been answered for.</summary>
        public const string VideoStreamsCompletedCode = "video_streams_bulk_completed";

        /// <summary>Code reported once a run over audio streams has been answered for.</summary>
        public const string AudioStreamsCompletedCode = "audio_streams_bulk_completed";

        /// <summary>Code reported once a run over on-demand videos has been answered for.</summary>
        public const string VideoOnDemandCompletedCode = "video_on_demand_bulk_completed";

        /// <summary>Code reported once a run over series has been answered for.</summary>
        public const string SeriesCompletedCode = "series_bulk_completed";

        /// <summary>Code reported once a run over episodes has been answered for.</summary>
        public const string EpisodesCompletedCode = "series_episodes_bulk_completed";

        /// <summary>Code reported once a run over subscriptions has been answered for.</summary>
        public const string SubscriptionsCompletedCode = "subscriptions_bulk_completed";

        /// <summary>Body for a run over accounts.</summary>
        /// <param name="outcome">What the service reported, one entry per identifier.</param>
        public static BulkOperationResponse Describe(BulkUserOutcome outcome)
        {
            var items = outcome.Items
                .Select(item =>
                {
                    var (status, body) = UserAdminResponses.Describe(new AdminUserOutcome
                    {
                        Action = item.Action,
                        UserId = item.UserId,
                        Errors = item.Errors
                    });

                    return ToItem(item.UserId, status, body);
                })
                .ToList();

            return Answer(UsersCompletedCode, Summary(outcome.Action, items), items);
        }

        /// <summary>Body for a run over groups.</summary>
        /// <param name="outcome">What the service reported, one entry per identifier.</param>
        public static BulkOperationResponse Describe(BulkUserGroupOutcome outcome)
        {
            var items = outcome.Items
                .Select(item =>
                {
                    var (status, body) = UserGroupResponses.Describe(new UserGroupOutcome
                    {
                        Action = item.Action,
                        GroupId = item.GroupId,
                        Errors = item.Errors
                    });

                    return ToItem(item.GroupId, status, body);
                })
                .ToList();

            return Answer(GroupsCompletedCode, Summary(outcome.Action, items), items);
        }

        /// <summary>Body for a run over streams.</summary>
        /// <param name="outcome">What the service reported, one entry per identifier.</param>
        public static BulkOperationResponse Describe(BulkVideoStreamOutcome outcome)
        {
            var items = outcome.Items
                .Select(item =>
                {
                    var (status, body) = VideoStreamResponses.Describe(new VideoStreamOutcome
                    {
                        Action = item.Action,
                        StreamId = item.StreamId,
                        Errors = item.Errors
                    });

                    var warnings = item.Warnings?
                        .Select(warning => VideoStreamResponses.DescribeWarning(warning))
                        .Select(warning => new BulkItemWarningResponse
                        {
                            Code = warning.Code,
                            Message = warning.Message
                        })
                        .ToList();

                    return ToItem(item.StreamId, status, body, warnings);
                })
                .ToList();

            return Answer(VideoStreamsCompletedCode, Summary(outcome.Action, items), items);
        }

        /// <summary>Body for a run over audio streams.</summary>
        /// <param name="outcome">What the service reported, one entry per identifier.</param>
        public static BulkOperationResponse Describe(BulkAudioStreamOutcome outcome)
        {
            var items = outcome.Items
                .Select(item =>
                {
                    var (status, body) = AudioStreamResponses.Describe(new AudioStreamOutcome
                    {
                        Action = item.Action,
                        StreamId = item.StreamId,
                        Errors = item.Errors
                    });

                    // No warnings column: an audio stream has no field a kind of it can switch
                    // off, so no bulk operation over audio streams can clear anything and
                    // there is nothing for a warning to say.
                    return ToItem(item.StreamId, status, body);
                })
                .ToList();

            return Answer(AudioStreamsCompletedCode, Summary(outcome.Action, items), items);
        }

        /// <summary>Body for a run over on-demand videos.</summary>
        /// <param name="outcome">What the service reported, one entry per identifier.</param>
        public static BulkOperationResponse Describe(BulkVodOutcome outcome)
        {
            var items = outcome.Items
                .Select(item =>
                {
                    var (status, body) = VodResponses.Describe(new VodOutcome
                    {
                        Action = item.Action,
                        VodId = item.VodId,
                        Errors = item.Errors
                    });

                    return ToItem(item.VodId, status, body);
                })
                .ToList();

            return Answer(VideoOnDemandCompletedCode, Summary(outcome.Action, items), items);
        }

        /// <summary>Body for a run over series.</summary>
        /// <param name="outcome">What the service reported, one entry per identifier.</param>
        public static BulkOperationResponse Describe(SeriesBulkOutcome outcome)
        {
            var items = outcome.Items
                .Select(item =>
                {
                    var (status, body) = SeriesResponses.Describe(new SeriesOutcome
                    {
                        Action = item.Action,
                        SeriesId = item.SeriesId,
                        Errors = item.Errors
                    });

                    return ToItem(item.SeriesId, status, body);
                })
                .ToList();

            return Answer(SeriesCompletedCode, Summary(outcome.Action, items), items);
        }

        /// <summary>Body for a run over episodes.</summary>
        /// <param name="outcome">What the service reported, one entry per identifier.</param>
        public static BulkOperationResponse Describe(EpisodeBulkOutcome outcome)
        {
            var items = outcome.Items
                .Select(item =>
                {
                    var (status, body) = SeriesResponses.Describe(new EpisodeOutcome
                    {
                        Action = item.Action,
                        SeriesId = Guid.Empty,
                        EpisodeId = item.EpisodeId,
                        Errors = item.Errors
                    });

                    return ToItem(item.EpisodeId, status, body);
                })
                .ToList();

            return Answer(EpisodesCompletedCode, Summary(outcome.Action, items), items);
        }

        /// <summary>Body for a run over subscriptions.</summary>
        /// <param name="outcome">What the service reported, one entry per identifier.</param>
        public static BulkOperationResponse Describe(BulkSubscriptionOutcome outcome)
        {
            var items = outcome.Items
                .Select(item =>
                {
                    var (status, body) = SubscriptionResponses.Describe(new SubscriptionOutcome
                    {
                        Action = item.Action,
                        SubscriptionId = item.SubscriptionId,
                        Errors = item.Errors
                    });

                    // A warning belongs to a row that was written: a plan change that cleared a
                    // service succeeded and lost a setting, which is not a refusal.
                    var warnings = item.Warnings?
                        .Select(SubscriptionResponses.DescribeWarning)
                        .Select(warning => new BulkItemWarningResponse
                        {
                            Code = warning.Code,
                            Message = warning.Message
                        })
                        .ToList();

                    return ToItem(item.SubscriptionId, status, body, warnings);
                })
                .ToList();

            return Answer(SubscriptionsCompletedCode, Summary(outcome.Action, items), items);
        }

        private static BulkItemResponse ToItem(            Guid id,
            int status,
            MessageResponse body,
            IReadOnlyList<BulkItemWarningResponse>? warnings = null)
            => new()
            {
                Id = id.ToString(),
                Ok = status < StatusCodes.Status400BadRequest,
                Code = body.Code,
                Message = body.Message,
                Errors = body.Errors,
                Warnings = warnings
            };

        private static BulkOperationResponse Answer(
            string code,
            string message,
            IReadOnlyList<BulkItemResponse> items)
            => new()
            {
                Code = code,
                Message = message,
                Succeeded = items.Count(item => item.Ok),
                Failed = items.Count(item => !item.Ok),
                Items = items
            };

        /// <summary>
        /// The sentence naming how much of a run over accounts landed.
        /// </summary>
        /// <remarks>
        /// Built from the operation rather than from the first row's code, because the codes
        /// inside one run are all different - a block that refused one account and completed
        /// nine has nine <c>user_blocked</c> entries and one <c>cannot_block_self</c>, and a
        /// sentence assembled from those would be a sentence about whichever one came first.
        /// The landed count beside it is what makes "9 of 10" read as the two halves it is.
        /// </remarks>
        private static string Summary(
            BulkUserAction action,
            IReadOnlyList<BulkItemResponse> items)
            => action switch
            {
                BulkUserAction.Block => $"{Landed(items)} of {items.Count} accounts blocked.",
                BulkUserAction.Unblock => $"{Landed(items)} of {items.Count} accounts unblocked.",
                BulkUserAction.Delete => $"{Landed(items)} of {items.Count} accounts deleted.",

                BulkUserAction.ConfirmRegistration =>
                    $"{Landed(items)} of {items.Count} accounts registered.",

                BulkUserAction.AssignGroup =>
                    $"{Landed(items)} of {items.Count} accounts moved to that group.",

                // Unreachable: the enum holds no other member.
                _ => $"{Landed(items)} of {items.Count} accounts changed."
            };

        /// <summary>The sentence naming how much of a run over groups landed.</summary>
        private static string Summary(
            BulkUserGroupAction action,
            IReadOnlyList<BulkItemResponse> items)
            => action switch
            {
                BulkUserGroupAction.Block => $"{Landed(items)} of {items.Count} groups blocked.",
                BulkUserGroupAction.Unblock => $"{Landed(items)} of {items.Count} groups unblocked.",
                BulkUserGroupAction.Delete => $"{Landed(items)} of {items.Count} groups deleted.",

                // Unreachable: the enum holds no other member.
                _ => $"{Landed(items)} of {items.Count} groups changed."
            };

        /// <summary>The sentence naming how much of a run over streams landed.</summary>
        private static string Summary(
            BulkVideoStreamAction action,
            IReadOnlyList<BulkItemResponse> items)
            => action switch
            {
                BulkVideoStreamAction.Start => $"{Landed(items)} of {items.Count} streams started.",
                BulkVideoStreamAction.Stop => $"{Landed(items)} of {items.Count} streams stopped.",
                BulkVideoStreamAction.Restart => $"{Landed(items)} of {items.Count} streams restarted.",
                BulkVideoStreamAction.Delete => $"{Landed(items)} of {items.Count} streams deleted.",

                BulkVideoStreamAction.MoveCategory =>
                    $"{Landed(items)} of {items.Count} streams moved to that category.",

                BulkVideoStreamAction.ChangeType =>
                    $"{Landed(items)} of {items.Count} streams changed type.",

                BulkVideoStreamAction.ChangeDelay =>
                    $"{Landed(items)} of {items.Count} streams had their delay updated.",

                BulkVideoStreamAction.ChangeArchive =>
                    $"{Landed(items)} of {items.Count} streams had their archive length updated.",

                BulkVideoStreamAction.ChangeRestream =>
                    $"{Landed(items)} of {items.Count} streams had their restream settings updated.",

                // Unreachable: the enum holds no other member.
                _ => $"{Landed(items)} of {items.Count} streams changed."
            };

        /// <summary>The sentence naming how much of a run over audio streams landed.</summary>
        private static string Summary(
            BulkAudioStreamAction action,
            IReadOnlyList<BulkItemResponse> items)
            => action switch
            {
                BulkAudioStreamAction.Start => $"{Landed(items)} of {items.Count} streams started.",
                BulkAudioStreamAction.Stop => $"{Landed(items)} of {items.Count} streams stopped.",
                BulkAudioStreamAction.Restart => $"{Landed(items)} of {items.Count} streams restarted.",
                BulkAudioStreamAction.Delete => $"{Landed(items)} of {items.Count} streams deleted.",

                BulkAudioStreamAction.MoveCategory =>
                    $"{Landed(items)} of {items.Count} streams moved to that category.",

                BulkAudioStreamAction.ChangeType =>
                    $"{Landed(items)} of {items.Count} streams changed type.",

                BulkAudioStreamAction.ChangeRestream =>
                    $"{Landed(items)} of {items.Count} streams had their restream settings updated.",

                // Unreachable: the enum holds no other member.
                _ => $"{Landed(items)} of {items.Count} streams changed."
            };

        /// <summary>The sentence naming how much of a run over on-demand videos landed.</summary>
        private static string Summary(
            BulkVodAction action,
            IReadOnlyList<BulkItemResponse> items)
            => action switch
            {
                BulkVodAction.ChangeStatus =>
                    $"{Landed(items)} of {items.Count} videos changed state.",

                BulkVodAction.MoveCategory =>
                    $"{Landed(items)} of {items.Count} videos moved to that category.",

                BulkVodAction.ChangeType =>
                    $"{Landed(items)} of {items.Count} videos changed type.",

                BulkVodAction.Delete =>
                    $"{Landed(items)} of {items.Count} videos deleted.",

                // Unreachable: the enum holds no other member.
                _ => $"{Landed(items)} of {items.Count} videos changed."
            };

        /// <summary>The sentence naming how much of a run over series landed.</summary>
        private static string Summary(
            BulkSeriesAction action,
            IReadOnlyList<BulkItemResponse> items)
            => action switch
            {
                BulkSeriesAction.ChangeStatus =>
                    $"{Landed(items)} of {items.Count} series changed state.",

                BulkSeriesAction.MoveCategory =>
                    $"{Landed(items)} of {items.Count} series moved to that category.",

                BulkSeriesAction.Delete =>
                    $"{Landed(items)} of {items.Count} series deleted.",

                // Unreachable: the enum holds no other member.
                _ => $"{Landed(items)} of {items.Count} series changed."
            };

        /// <summary>The sentence naming how much of a run over episodes landed.</summary>
        private static string Summary(
            BulkEpisodeAction action,
            IReadOnlyList<BulkItemResponse> items)
            => action switch
            {
                BulkEpisodeAction.ChangeStatus =>
                    $"{Landed(items)} of {items.Count} episodes changed state.",

                BulkEpisodeAction.ChangeType =>
                    $"{Landed(items)} of {items.Count} episodes changed type.",

                BulkEpisodeAction.Delete =>
                    $"{Landed(items)} of {items.Count} episodes deleted.",

                // Unreachable: the enum holds no other member.
                _ => $"{Landed(items)} of {items.Count} episodes changed."
            };

        /// <summary>The sentence naming how much of a run over subscriptions landed.</summary>
        private static string Summary(
            BulkSubscriptionAction action,
            IReadOnlyList<BulkItemResponse> items)
            => action switch
            {
                BulkSubscriptionAction.ChangeTariff =>
                    $"{Landed(items)} of {items.Count} subscriptions moved to that tariff plan.",

                BulkSubscriptionAction.ChangeCreator =>
                    $"{Landed(items)} of {items.Count} subscriptions reassigned.",

                BulkSubscriptionAction.Delete =>
                    $"{Landed(items)} of {items.Count} subscriptions deleted.",

                BulkSubscriptionAction.DeletePlaylist =>
                    $"{Landed(items)} of {items.Count} subscriptions had their playlist cleared.",

                BulkSubscriptionAction.DeleteMag =>
                    $"{Landed(items)} of {items.Count} subscriptions had their MAG cleared.",

                BulkSubscriptionAction.DeleteEnigma2 =>
                    $"{Landed(items)} of {items.Count} subscriptions had their Enigma2 cleared.",

                BulkSubscriptionAction.BlockOwner =>
                    $"{Landed(items)} of {items.Count} owners blocked.",

                // Unreachable: the enum holds no other member.
                _ => $"{Landed(items)} of {items.Count} subscriptions changed."
            };

        private static int Landed(IReadOnlyList<BulkItemResponse> items)
            => items.Count(item => item.Ok);
    }
}
