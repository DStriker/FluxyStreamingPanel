/**
 * How an on-demand video is delivered, spelled exactly as the backend's `VodType` spells
 * it.
 *
 * The two members are not two kinds of file - both point at one source - but two answers to
 * "who carries the bytes": a relay is served by this installation (the play endpoint proxies
 * a remote address or streams a local file), and a redirect sends the client to the source.
 */
export type VideoOnDemandType = 'Relay' | 'Redirect'
