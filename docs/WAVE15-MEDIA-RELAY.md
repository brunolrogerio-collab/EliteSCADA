# Media relay deployment and boundaries

## Deployment

The stable Compose group includes `media-gateway`, pinned to MediaMTX 1.21.1 and its image
digest. It publishes no host ports and shares a separate network only with the API. Its
configuration file is read-only; dynamic camera paths and credentials remain in memory.
Stop/start the whole Compose group to restart API, web, database and the gateway together.
PostgreSQL and protected-material/key volumes are unchanged.

Hosts outside Compose must provision `MediaSources:GatewayControlUrl` and
`MediaSources:GatewayPlaybackUrl` as trusted deployment settings. These are not project
fields or user-supplied playback URLs. Without a gateway, RTSP returns `unsupported`.
The gateway does remuxing, not arbitrary codec transcoding: H.264/AAC is the recommended
browser-compatible camera configuration. Unsupported codecs surface a diagnostic.
RTSPS accepts literal addresses with valid certificates; DNS-based RTSPS needs a future
TLS-aware pinned resolver and is explicitly rejected instead of bypassing verification.

## Request authority and network policy

- Every request requires Runtime View and the source must exist and be enabled in the
  exact persisted Active project/revision. Browser queries cannot replace the source URL.
- HTTP connections resolve and check all DNS addresses at connect time, then pin the socket
  to an allowed address. Redirects, cookies and system HTTP proxies are disabled.
- Loopback, unspecified, multicast and link-local/metadata addresses are always blocked.
  Private LANs require `MediaSources:AllowPrivateNetwork=true` (explicitly enabled for the
  isolated industrial test deployment). IPv4-mapped and translation IPv6 cannot bypass it.
- HLS playlists, variant playlists, keys and segments must remain on the configured origin.
  Cross-origin CDN resources, variable substitution and content steering are rejected.
  URLs are encrypted/authenticated in expiring tickets scoped to source/project/revision.
  Old tickets fail after API restart or Active revision changes.
- RTSP destinations are policy-checked and DNS is pinned before gateway provisioning.
  Basic camera credentials are resolved through a protected lease and passed over the
  private control network, never in browser URLs, console logs or process arguments.
  Bearer RTSP credentials are explicitly unsupported. Gateway source pulling is on demand.
- HTTP/MJPEG streams have a 10-minute lifetime and an eight-request host concurrency cap.
  Upstream connections are bounded per origin. HLS playlists are at most 1 MiB and segments
  at most 32 MiB, with bounded connect/read deadlines. Disconnection cancels requests.

## Player lifecycle

HLS.js 1.7.3 is an exact, lazy-loaded dependency; Safari/native HLS is retained as fallback.
Video uses ordinary play/pause/seek/mute controls. Local PDF assets use the sandboxed browser
PDF viewer and configured initial page/zoom/toolbar. Engineering shows source identity only,
without continuously connecting cameras. Runtime statuses distinguish connecting, live,
offline, auth, busy and unsupported; retries use bounded exponential backoff and are cancelled
on unmount/source change. Thirty seconds of stable playback resets the retry budget.

## Evidence and dependency notices

Local Release API and production web builds pass. Twenty focused relay tests cover denied
destinations, opaque revision/source tickets, playlist rewriting and origin escapes. This
does not claim live camera or mounted browser-stream acceptance; that evidence is separate.
The dependency audit reports existing Monaco/DOMPurify advisories, not an HLS.js advisory;
no unrelated major editor upgrade was folded into this media change.

MediaMTX and HLS.js are MIT licensed. Primary references:

- https://mediamtx.org/docs/features/control-api
- https://mediamtx.org/docs/references/configuration-file
- https://github.com/bluenviron/mediamtx/blob/v1.21.1/LICENSE
- https://github.com/video-dev/hls.js
- https://github.com/video-dev/hls.js/blob/v1.7.3/LICENSE
