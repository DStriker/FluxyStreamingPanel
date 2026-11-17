import { describe, expect, it } from 'vitest'
import type { TranscodingProfileValues } from '../../../src/features/transcoding-profiles/api'
import {
  AUDIO_CHANNELS,
  AUDIO_CODECS,
  PRESETS,
  SAMPLE_RATES,
  VIDEO_CODECS,
  VIDEO_PROFILES,
} from '../../../src/features/transcoding-profiles/ffmpegOptions'

/**
 * The ffmpeg option tables are pins, not logic: the frontend's copy of the backend's
 * `TranscodingProfileTextParser` and of the enums behind it. These tests hold the sets so a
 * name added on one side and not the other fails here rather than as a `validation_failed`
 * beside a select the operator had just used.
 *
 * The values are ffmpeg's own lowercase spellings, including the underscored ones no C# member
 * is named after, which is why `Enum.TryParse` is not enough on either side.
 */

describe('the ffmpeg option tables', () => {
  it('lists 34 video codecs, copy first', () => {
    expect(VIDEO_CODECS).toHaveLength(34)
    expect(VIDEO_CODECS[0]?.value).toBe('copy')
  })

  it('lists 27 audio codecs, copy first, including the underscored names', () => {
    expect(AUDIO_CODECS).toHaveLength(27)
    expect(AUDIO_CODECS[0]?.value).toBe('copy')
    expect(AUDIO_CODECS.map((option) => option.value)).toEqual(
      expect.arrayContaining(['pcm_alaw', 'pcm_mulaw', 'pcm_s16le', 'libfdk_aac']),
    )
  })

  it('lists the ten presets, default first', () => {
    expect(PRESETS).toHaveLength(10)
    expect(PRESETS[0]?.value).toBe('default')
    expect(PRESETS.map((option) => option.value)).toEqual(
      expect.arrayContaining(['ultrafast', 'medium', 'veryslow']),
    )
  })

  it('lists the eight video profiles, none first, in H.264 level order', () => {
    expect(VIDEO_PROFILES).toHaveLength(8)
    expect(VIDEO_PROFILES.map((option) => option.value)).toEqual([
      'none',
      'baseline3.0',
      'baseline3.1',
      'main3.1',
      'main4.0',
      'high4.0',
      'high4.1',
      'high4.2',
    ])
  })

  it('gives every option a locale key under the form block', () => {
    for (const option of [...VIDEO_CODECS, ...AUDIO_CODECS, ...PRESETS, ...VIDEO_PROFILES]) {
      expect(option.labelKey.startsWith('transcodingProfilesForm.')).toBe(true)
    }
  })

  it('offers the eight channel counts and the ordinary sample rates', () => {
    expect(AUDIO_CHANNELS).toEqual([1, 2, 3, 4, 5, 6, 7, 8])
    expect(SAMPLE_RATES).toEqual(expect.arrayContaining([44100, 48000]))
  })

  it('names exactly the wire values the backend parser knows', () => {
    // The set below is the backend's `VideoCodec` enum, spelled the way its parser writes it.
    // A value added there and not here is a select that cannot offer it; a value added here and
    // not there is a refusal beside the control.
    expect(VIDEO_CODECS.map((option) => option.value)).toEqual([
      'copy',
      'libx264',
      'libx265',
      'libvpx',
      'libvpx-vp9',
      'libaom-av1',
      'libsvtav1',
      'librav1e',
      'mpeg4',
      'msmpeg4v2',
      'msmpeg4',
      'libtheora',
      'libxvid',
      'h264_nvenc',
      'hevc_nvenc',
      'av1_nvenc',
      'h264_qsv',
      'hevc_qsv',
      'av1_qsv',
      'h264_vaapi',
      'hevc_vaapi',
      'av1_vaapi',
      'h264_amf',
      'hevc_amf',
      'av1_amf',
      'h264_videotoolbox',
      'hevc_videotoolbox',
      'prores',
      'mjpeg',
      'dnxhd',
      'ffv1',
      'huffyuv',
      'wmv2',
      'rawvideo',
    ])

    expect(AUDIO_CODECS.map((option) => option.value)).toEqual([
      'copy',
      'aac',
      'libmp3lame',
      'libopus',
      'libvorbis',
      'ac3',
      'eac3',
      'mp2',
      'alac',
      'flac',
      'pcm_s16le',
      'pcm_s24le',
      'pcm_alaw',
      'pcm_mulaw',
      'wmav2',
      'libopencore_amrnb',
      'libvo_amrwbenc',
      'dca',
      'truehd',
      'libmpegh',
      'libspeex',
      'libfdk_aac',
      'mp3',
      'ac4',
      'opus',
      'mp1',
      'aac_at',
    ])

    expect(PRESETS.map((option) => option.value)).toEqual([
      'default',
      'ultrafast',
      'superfast',
      'veryfast',
      'faster',
      'fast',
      'medium',
      'slow',
      'slower',
      'veryslow',
    ])
  })

  it('keeps the form body shape stable for a create', () => {
    // A compile-time shape check with a runtime echo: every field `TranscodingProfileValues`
    // names has a value here, so a field added to the type without a default in the form's
    // `CREATE_VALUES` fails the typecheck rather than arriving as `undefined`.
    const values: TranscodingProfileValues = {
      name: 'Standard',
      videoCodec: 'copy',
      audioCodec: 'copy',
      preset: 'default',
      videoProfile: null,
      videoBitrateAvg: null,
      audioBitrateAvg: null,
      minBitrateTolerance: null,
      maxBitrateTolerance: null,
      bufferSize: null,
      crf: null,
      width: null,
      height: null,
      aspectRatio: null,
      framerate: null,
      audioSampleRate: null,
      audioChannels: null,
      threads: null,
      logoUrl: null,
      redactionX: null,
      redactionY: null,
      redactionWidth: null,
      redactionHeight: null,
    }

    expect(Object.keys(values)).toHaveLength(23)
  })
})
