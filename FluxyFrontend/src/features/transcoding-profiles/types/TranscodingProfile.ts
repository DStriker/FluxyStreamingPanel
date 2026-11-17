/**
 * The transcoding profile's response contract, as `GET /admin/transcoding-profiles` answers
 * with it in both the list and the detail.
 *
 * The four enum fields carry ffmpeg's own lowercase names (`libx264`, `pcm_alaw`, `veryslow`,
 * `high4.0`) rather than C# member names, because that is what the operator reads and what
 * `ffmpegOptions.ts` mirrors from the backend's text parser. Every optional option is `null`
 * when the row carries no value, and `null` means "do not pass this flag to ffmpeg" - a real
 * state rather than a missing field.
 */
export interface TranscodingProfile {
  /** Identifier of the profile, as text. The table shows it shortened. */
  id: string
  /** Display name, unique for the installation. */
  name: string
  /** Video encoder name, or `copy` to leave the video stream alone. */
  videoCodec: string
  /** Audio encoder name, or `copy` to leave the audio stream alone. */
  audioCodec: string
  /** Encoder preset name, or `default` for none. */
  preset: string
  /** H.264 profile name, or null. */
  videoProfile: string | null
  /** Average video bitrate in kbps, or null. */
  videoBitrateAvg: number | null
  /** Average audio bitrate in kbps, or null. */
  audioBitrateAvg: number | null
  /** Lower bitrate tolerance in kbps, or null. */
  minBitrateTolerance: number | null
  /** Upper bitrate tolerance in kbps, or null. */
  maxBitrateTolerance: number | null
  /** Decoder buffer size in kbps, or null. */
  bufferSize: number | null
  /** Constant rate factor 0..51, or null. */
  crf: number | null
  /** Output width, or null. `-1` keeps the aspect ratio. */
  width: number | null
  /** Output height, or null. `-1` keeps the aspect ratio. */
  height: number | null
  /** Aspect ratio override such as `16:9`, or null. */
  aspectRatio: string | null
  /** Output frame rate, or null. */
  framerate: number | null
  /** Audio sample rate in hertz, or null. */
  audioSampleRate: number | null
  /** Audio channel count, or null. */
  audioChannels: number | null
  /** Encoder thread count, or null. `0` means auto. */
  threads: number | null
  /** Watermark address, or null. */
  logoUrl: string | null
  /** Left edge of the redaction rectangle, or null. */
  redactionX: number | null
  /** Top edge of the redaction rectangle, or null. */
  redactionY: number | null
  /** Width of the redaction rectangle, or null. */
  redactionWidth: number | null
  /** Height of the redaction rectangle, or null. */
  redactionHeight: number | null
}
