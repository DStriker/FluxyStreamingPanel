/**
 * The ffmpeg option lists the profile form offers, mirroring the backend's text parser.
 *
 * Every value here is a **wire name**: the lowercase spelling ffmpeg itself uses on a command
 * line (`libx264`, `msmpeg4v2`, `pcm_alaw`, `high4.0`). The backend's
 * `TranscodingProfileTextParser` owns the same tables in the other direction, and a test
 * (`tests/features/transcoding-profiles/ffmpegOptions.test.ts`) pins the sets so the two cannot
 * drift: a name accepted here and unknown there would be a `validation_failed` on the field the
 * operator had just chosen.
 *
 * Each list carries the locale key of its label rather than a literal, because every visible
 * string in this application lives in `en.ts`/`ru.ts`.
 */

/** One option of a select: the wire value and the locale key of its label. */
export interface FfmpegOption {
  /** The value the API reads and writes. */
  value: string
  /** Locale key of the label shown for it. */
  labelKey: string
}

/** The 34 video encoders the profile may name. */
export const VIDEO_CODECS: readonly FfmpegOption[] = [
  { value: 'copy', labelKey: 'transcodingProfilesForm.codecs.copy' },
  { value: 'libx264', labelKey: 'transcodingProfilesForm.codecs.libx264' },
  { value: 'libx265', labelKey: 'transcodingProfilesForm.codecs.libx265' },
  { value: 'libvpx', labelKey: 'transcodingProfilesForm.codecs.libvpx' },
  { value: 'libvpx-vp9', labelKey: 'transcodingProfilesForm.codecs.libvpxVp9' },
  { value: 'libaom-av1', labelKey: 'transcodingProfilesForm.codecs.libaomAv1' },
  { value: 'libsvtav1', labelKey: 'transcodingProfilesForm.codecs.libsvtav1' },
  { value: 'librav1e', labelKey: 'transcodingProfilesForm.codecs.librav1e' },
  { value: 'mpeg4', labelKey: 'transcodingProfilesForm.codecs.mpeg4' },
  { value: 'msmpeg4v2', labelKey: 'transcodingProfilesForm.codecs.msmpeg4v2' },
  { value: 'msmpeg4', labelKey: 'transcodingProfilesForm.codecs.msmpeg4' },
  { value: 'libtheora', labelKey: 'transcodingProfilesForm.codecs.libtheora' },
  { value: 'libxvid', labelKey: 'transcodingProfilesForm.codecs.libxvid' },
  { value: 'h264_nvenc', labelKey: 'transcodingProfilesForm.codecs.h264Nvenc' },
  { value: 'hevc_nvenc', labelKey: 'transcodingProfilesForm.codecs.hevcNvenc' },
  { value: 'av1_nvenc', labelKey: 'transcodingProfilesForm.codecs.av1Nvenc' },
  { value: 'h264_qsv', labelKey: 'transcodingProfilesForm.codecs.h264Qsv' },
  { value: 'hevc_qsv', labelKey: 'transcodingProfilesForm.codecs.hevcQsv' },
  { value: 'av1_qsv', labelKey: 'transcodingProfilesForm.codecs.av1Qsv' },
  { value: 'h264_vaapi', labelKey: 'transcodingProfilesForm.codecs.h264Vaapi' },
  { value: 'hevc_vaapi', labelKey: 'transcodingProfilesForm.codecs.hevcVaapi' },
  { value: 'av1_vaapi', labelKey: 'transcodingProfilesForm.codecs.av1Vaapi' },
  { value: 'h264_amf', labelKey: 'transcodingProfilesForm.codecs.h264Amf' },
  { value: 'hevc_amf', labelKey: 'transcodingProfilesForm.codecs.hevcAmf' },
  { value: 'av1_amf', labelKey: 'transcodingProfilesForm.codecs.av1Amf' },
  { value: 'h264_videotoolbox', labelKey: 'transcodingProfilesForm.codecs.h264Videotoolbox' },
  { value: 'hevc_videotoolbox', labelKey: 'transcodingProfilesForm.codecs.hevcVideotoolbox' },
  { value: 'prores', labelKey: 'transcodingProfilesForm.codecs.prores' },
  { value: 'mjpeg', labelKey: 'transcodingProfilesForm.codecs.mjpeg' },
  { value: 'dnxhd', labelKey: 'transcodingProfilesForm.codecs.dnxhd' },
  { value: 'ffv1', labelKey: 'transcodingProfilesForm.codecs.ffv1' },
  { value: 'huffyuv', labelKey: 'transcodingProfilesForm.codecs.huffyuv' },
  { value: 'wmv2', labelKey: 'transcodingProfilesForm.codecs.wmv2' },
  { value: 'rawvideo', labelKey: 'transcodingProfilesForm.codecs.rawvideo' },
]

/** The 27 audio encoders the profile may name. */
export const AUDIO_CODECS: readonly FfmpegOption[] = [
  { value: 'copy', labelKey: 'transcodingProfilesForm.codecs.copy' },
  { value: 'aac', labelKey: 'transcodingProfilesForm.codecs.aac' },
  { value: 'libmp3lame', labelKey: 'transcodingProfilesForm.codecs.libmp3lame' },
  { value: 'libopus', labelKey: 'transcodingProfilesForm.codecs.libopus' },
  { value: 'libvorbis', labelKey: 'transcodingProfilesForm.codecs.libvorbis' },
  { value: 'ac3', labelKey: 'transcodingProfilesForm.codecs.ac3' },
  { value: 'eac3', labelKey: 'transcodingProfilesForm.codecs.eac3' },
  { value: 'mp2', labelKey: 'transcodingProfilesForm.codecs.mp2' },
  { value: 'alac', labelKey: 'transcodingProfilesForm.codecs.alac' },
  { value: 'flac', labelKey: 'transcodingProfilesForm.codecs.flac' },
  { value: 'pcm_s16le', labelKey: 'transcodingProfilesForm.codecs.pcmS16le' },
  { value: 'pcm_s24le', labelKey: 'transcodingProfilesForm.codecs.pcmS24le' },
  { value: 'pcm_alaw', labelKey: 'transcodingProfilesForm.codecs.pcmAlaw' },
  { value: 'pcm_mulaw', labelKey: 'transcodingProfilesForm.codecs.pcmMulaw' },
  { value: 'wmav2', labelKey: 'transcodingProfilesForm.codecs.wmav2' },
  { value: 'libopencore_amrnb', labelKey: 'transcodingProfilesForm.codecs.libopencoreAmrnb' },
  { value: 'libvo_amrwbenc', labelKey: 'transcodingProfilesForm.codecs.libvoAmrwbenc' },
  { value: 'dca', labelKey: 'transcodingProfilesForm.codecs.dca' },
  { value: 'truehd', labelKey: 'transcodingProfilesForm.codecs.truehd' },
  { value: 'libmpegh', labelKey: 'transcodingProfilesForm.codecs.libmpegh' },
  { value: 'libspeex', labelKey: 'transcodingProfilesForm.codecs.libspeex' },
  { value: 'libfdk_aac', labelKey: 'transcodingProfilesForm.codecs.libfdkAac' },
  { value: 'mp3', labelKey: 'transcodingProfilesForm.codecs.mp3' },
  { value: 'ac4', labelKey: 'transcodingProfilesForm.codecs.ac4' },
  { value: 'opus', labelKey: 'transcodingProfilesForm.codecs.opus' },
  { value: 'mp1', labelKey: 'transcodingProfilesForm.codecs.mp1' },
  { value: 'aac_at', labelKey: 'transcodingProfilesForm.codecs.aacAt' },
]

/** The ten presets, `default` first because it is what a create applies. */
export const PRESETS: readonly FfmpegOption[] = [
  { value: 'default', labelKey: 'transcodingProfilesForm.presets.default' },
  { value: 'ultrafast', labelKey: 'transcodingProfilesForm.presets.ultrafast' },
  { value: 'superfast', labelKey: 'transcodingProfilesForm.presets.superfast' },
  { value: 'veryfast', labelKey: 'transcodingProfilesForm.presets.veryfast' },
  { value: 'faster', labelKey: 'transcodingProfilesForm.presets.faster' },
  { value: 'fast', labelKey: 'transcodingProfilesForm.presets.fast' },
  { value: 'medium', labelKey: 'transcodingProfilesForm.presets.medium' },
  { value: 'slow', labelKey: 'transcodingProfilesForm.presets.slow' },
  { value: 'slower', labelKey: 'transcodingProfilesForm.presets.slower' },
  { value: 'veryslow', labelKey: 'transcodingProfilesForm.presets.veryslow' },
]

/** The eight H.264 profiles, `none` first because it means "send no flag". */
export const VIDEO_PROFILES: readonly FfmpegOption[] = [
  { value: 'none', labelKey: 'transcodingProfilesForm.videoProfiles.none' },
  { value: 'baseline3.0', labelKey: 'transcodingProfilesForm.videoProfiles.baseline30' },
  { value: 'baseline3.1', labelKey: 'transcodingProfilesForm.videoProfiles.baseline31' },
  { value: 'main3.1', labelKey: 'transcodingProfilesForm.videoProfiles.main31' },
  { value: 'main4.0', labelKey: 'transcodingProfilesForm.videoProfiles.main40' },
  { value: 'high4.0', labelKey: 'transcodingProfilesForm.videoProfiles.high40' },
  { value: 'high4.1', labelKey: 'transcodingProfilesForm.videoProfiles.high41' },
  { value: 'high4.2', labelKey: 'transcodingProfilesForm.videoProfiles.high42' },
]

/** The sample rates the form offers, the two a receiver actually uses and a few beside them. */
export const SAMPLE_RATES: readonly number[] = [8000, 11025, 16000, 22050, 32000, 44100, 48000, 96000]

/** The channel counts the form offers, one through eight. */
export const AUDIO_CHANNELS: readonly number[] = [1, 2, 3, 4, 5, 6, 7, 8]
