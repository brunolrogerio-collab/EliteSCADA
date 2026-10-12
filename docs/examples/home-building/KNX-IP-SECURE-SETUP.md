# KNX/IP Secure v1 setup

EliteSCADA's KNX/IP v1 driver uses XKNX 3.20.0 through a managed CPython 3.13.x sidecar on Windows x64 and Linux x64. Provision the sidecar dependencies once in a dedicated, access-controlled virtual environment on the host; the runtime performs no package installation or internet access.

```powershell
py -3.13 -m venv C:\EliteSCADA\runtime\knx-ip
C:\EliteSCADA\runtime\knx-ip\Scripts\python.exe -m pip install --require-hashes --only-binary=:all: -r C:\EliteSCADA\app\sidecars\knx-ip\requirements.lock
```

```sh
python3.13 -m venv /opt/elitescada/runtime/knx-ip
/opt/elitescada/runtime/knx-ip/bin/python -m pip install --require-hashes --only-binary=:all: -r /opt/elitescada/app/sidecars/knx-ip/requirements.lock
```

Configure the trusted host setting `CommunicationDrivers:KnxIp:PythonExecutable` to the virtual environment's Python executable. If `ArtifactDirectory` is overridden, it must point to the deployed `sidecars/knx-ip` directory containing `main.py` and `security.py`. Keep the environment and executable writable only by the service administrator. Do not install packages while the service is running.

Create a `knx.ip` Data Source with `gatewayHost`, optional `gatewayPort` (default `3671`), the assigned tunnel `individualAddress`, and `userId`. Provision these Authority-owned materials separately through the authenticated protected-material endpoint:

- `knx.ip-secure.user-password`: UTF-8 tunnel user password.
- `knx.ip-secure.device-authentication`: UTF-8 device authentication password.
- `knx.data-secure.keyring`: UTF-8 JSON envelope with exactly `format`, `knxkeysBase64`, and `keyringPassword`; use format `elitescada.knxkeys-bundle.v1`, with the signed ETS `.knxkeys` file in strict Base64. The supplied tunnel credentials must match the selected keyring tunneling interface.

The endpoint accepts raw `application/octet-stream` and requires `x-elitescada-workspace-version`; send the UTF-8 bytes of the JSON envelope for the keyring purpose. Do not put these values in Engineering settings or `.escadapkg`. The package carries only host-owned protected-material slot markers.

Each TAG uses the `elitescada.driver.knx.ip` CommunicationBinding v1. Its portable address is the write/command group address; `stateGroupAddress` is separately configured as the authoritative status/readback address. Supported DPTs are `1.001`, `1.002`, `5.001`, `9.001`, `9.004`, `9.007`, and `9.024`. All configured command and state addresses must have Data Secure group keys in the keyring.

The runtime selects only `TUNNELING_TCP_SECURE`, refuses a missing or mismatched keyring slot/key, and rejects telegrams not marked Data Secure. It never falls back to automatic discovery, UDP tunneling, routing, or plaintext KNX/IP. A secure readback—not command transmission alone—is the source of TAG truth. This profile does not claim physical-device L4 qualification; validate the exact gateway, ETS keyring and group-address configuration in the target installation before commissioning.
