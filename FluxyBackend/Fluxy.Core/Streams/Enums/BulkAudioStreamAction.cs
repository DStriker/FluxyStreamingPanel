namespace Fluxy.Core.Streams.Enums
{
    /// <summary>
    /// The operations one bulk request may ask for over a set of audio streams.
    /// </summary>
    /// <remarks>
    /// A shorter menu than the video bulk endpoint's, because an audio stream has fewer
    /// independent facts: no delay, no archive, no payload that a kind can switch off. Restream
    /// settings are offered because both kinds carry them.
    /// </remarks>
    public enum BulkAudioStreamAction
    {
        /// <summary>Mark every selected stream active.</summary>
        Start,

        /// <summary>Mark every selected stream disabled.</summary>
        Stop,

        /// <summary>Mark every selected stream active, reporting a restart.</summary>
        Restart,

        /// <summary>Delete every selected stream.</summary>
        Delete,

        /// <summary>Move every selected stream into one category.</summary>
        MoveCategory,

        /// <summary>Change the kind of every selected stream.</summary>
        ChangeType,

        /// <summary>Replace the restream settings of every selected stream.</summary>
        ChangeRestream
    }
}
