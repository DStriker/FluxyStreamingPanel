namespace Fluxy.Core.Epg.Enums
{
    /// <summary>
    /// Which column of the channels-using-a-source table a caller asked to order by.
    /// </summary>
    public enum EpgSourceChannelSortField
    {
        /// <summary>Display name of the stream, which is what the column draws.</summary>
        StreamName,

        /// <summary>Channel key the stream is filed under inside the guide.</summary>
        ChannelKey
    }
}
