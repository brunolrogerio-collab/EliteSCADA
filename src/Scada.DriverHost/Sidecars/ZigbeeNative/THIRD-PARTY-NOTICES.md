# Third-party notices

This sidecar distributes the 24 production npm packages listed below, pinned by `package-lock.json`. Each linked file is the full license text audited for that exact package version. The `slip@1.0.2` package declares `(MIT OR GPL-2.0)`; this artifact selects the MIT option and includes its upstream MIT notice.

| Package | Version | Declared license | Distribution notice |
|---|---:|---|---|
| `@leichtgewicht/ip-codec` | `2.0.5` | `MIT` | [@leichtgewicht__ip-codec@2.0.5-LICENSE](licenses/@leichtgewicht__ip-codec@2.0.5-LICENSE) |
| `@serialport/bindings-cpp` | `13.0.1` | `MIT` | [@serialport__bindings-cpp@13.0.1-LICENSE](licenses/@serialport__bindings-cpp@13.0.1-LICENSE) |
| `@serialport/bindings-interface` | `1.2.2` | `MIT` | [@serialport__bindings-interface@1.2.2-LICENSE](licenses/@serialport__bindings-interface@1.2.2-LICENSE) |
| `@serialport/parser-delimiter` | `13.0.0` | `MIT` | [@serialport__parser-delimiter@13.0.0-LICENSE](licenses/@serialport__parser-delimiter@13.0.0-LICENSE) |
| `@serialport/parser-readline` | `13.0.0` | `MIT` | [@serialport__parser-readline@13.0.0-LICENSE](licenses/@serialport__parser-readline@13.0.0-LICENSE) |
| `@serialport/stream` | `13.0.0` | `MIT` | [@serialport__stream@13.0.0-LICENSE](licenses/@serialport__stream@13.0.0-LICENSE) |
| `bonjour-service` | `1.4.4` | `MIT` | [bonjour-service@1.4.4-LICENSE](licenses/bonjour-service@1.4.4-LICENSE) |
| `buffer-crc32` | `1.0.0` | `MIT` | [buffer-crc32@1.0.0-LICENSE](licenses/buffer-crc32@1.0.0-LICENSE) |
| `debounce` | `2.2.0` | `MIT` | [debounce@2.2.0-license](licenses/debounce@2.2.0-license) |
| `debug` | `4.4.0` | `MIT` | [debug@4.4.0-LICENSE](licenses/debug@4.4.0-LICENSE) |
| `dns-packet` | `5.6.1` | `MIT` | [dns-packet@5.6.1-LICENSE](licenses/dns-packet@5.6.1-LICENSE) |
| `fast-deep-equal` | `3.1.3` | `MIT` | [fast-deep-equal@3.1.3-LICENSE](licenses/fast-deep-equal@3.1.3-LICENSE) |
| `iconv-lite` | `0.6.3` | `MIT` | [iconv-lite@0.6.3-LICENSE](licenses/iconv-lite@0.6.3-LICENSE) |
| `mixin-deep` | `2.0.1` | `MIT` | [mixin-deep@2.0.1-LICENSE](licenses/mixin-deep@2.0.1-LICENSE) |
| `ms` | `2.1.3` | `MIT` | [ms@2.1.3-license.md](licenses/ms@2.1.3-license.md) |
| `multicast-dns` | `7.2.5` | `MIT` | [multicast-dns@7.2.5-LICENSE](licenses/multicast-dns@7.2.5-LICENSE) |
| `node-addon-api` | `8.3.0` | `MIT` | [node-addon-api@8.3.0-LICENSE.md](licenses/node-addon-api@8.3.0-LICENSE.md) |
| `node-gyp-build` | `4.8.4` | `MIT` | [node-gyp-build@4.8.4-LICENSE](licenses/node-gyp-build@4.8.4-LICENSE) |
| `safer-buffer` | `2.1.2` | `MIT` | [safer-buffer@2.1.2-LICENSE](licenses/safer-buffer@2.1.2-LICENSE) |
| `semver` | `7.8.5` | `ISC` | [semver@7.8.5-LICENSE](licenses/semver@7.8.5-LICENSE) |
| `slip` | `1.0.2` | `(MIT OR GPL-2.0)` | [slip@1.0.2-MIT-LICENSE.txt](licenses/slip@1.0.2-MIT-LICENSE.txt) — MIT option selected; GPL-2.0 option not used |
| `thunky` | `1.1.0` | `MIT` | [thunky@1.1.0-LICENSE](licenses/thunky@1.1.0-LICENSE) |
| `zigbee-herdsman` | `3.3.2` | `MIT` | [zigbee-herdsman@3.3.2-LICENSE](licenses/zigbee-herdsman@3.3.2-LICENSE) |
| `zigbee-herdsman-converters` | `23.7.0` | `MIT` | [zigbee-herdsman-converters@23.7.0-LICENSE](licenses/zigbee-herdsman-converters@23.7.0-LICENSE) |

## Node.js runtime

The qualified runtime is the official Node.js `v24.21.0` Linux x64 distribution. Its complete bundled runtime license and third-party notices are included here:

- [Node.js and bundled third-party license texts](licenses/node-v24.21.0-LICENSE.txt)
- Official distribution: `https://nodejs.org/dist/v24.21.0/`
- Linux x64 archive SHA-256: `fd8e59d5a511510f6a298afb548f18c7d2b1be404d8b4a27d94fbe49f56cb2d6`

## Audited distribution counts

- Production npm closure: **24** packages (22 declared MIT, 1 ISC, 1 dual-license package with MIT option selected).
- Unknown licenses: **0**.
- GPL-3.0 packages: **0**.

This inventory covers the npm runtime lock and the selected Node.js distribution. The CycloneDX SBOM records the npm dependency graph plus Node.js as an external runtime component.
