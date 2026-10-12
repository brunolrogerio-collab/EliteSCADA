# Third-party notices

The managed KNX/IP sidecar does not vendor or bundle Python wheels. Deployments provision the runtime in a dedicated CPython 3.13.x environment using `requirements.lock`; that file pins the complete production dependency closure and permits only the listed SHA-256 artifacts. The installed distributions carry their upstream license texts in their `.dist-info` metadata.

| Package | Version | License used | Upstream project |
|---|---:|---|---|
| `xknx` | `3.20.0` | MIT | [XKNX](https://github.com/XKNX/xknx/tree/3.20.0) |
| `cryptography` | `50.0.2` | BSD-3-Clause (the project offers Apache-2.0 OR BSD-3-Clause) | [cryptography](https://github.com/pyca/cryptography/tree/50.0.2) |
| `ifaddr` | `0.2.0` | MIT | [ifaddr](https://github.com/pydron/ifaddr/tree/0.2.0) |
| `cffi` | `2.1.1` | MIT-0 | [cffi](https://github.com/python-cffi/cffi/tree/v2.1.1) |
| `pycparser` | `3.11` | BSD-3-Clause | [pycparser](https://github.com/eliben/pycparser/tree/release_v3.11) |

The exact versions, transitive relationships and platform wheel hashes are recorded in `SBOM.cdx.json` and `requirements.lock`. No KNX keyring, credential, or customer data is part of this dependency inventory.
