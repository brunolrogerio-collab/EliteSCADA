#!/usr/bin/env python3
"""Exact-head HA-D1 evidence using two independent Scada.Api OS processes."""
from __future__ import annotations

import base64
import hashlib
import hmac
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request
import uuid
from datetime import datetime, timezone


ROOT = Path(__file__).resolve().parents[2]
API_DLL = ROOT / "src" / "Scada.Api" / "bin" / "Release" / "net10.0" / "Scada.Api.dll"
SECRET = "ha-d1-evidence-shared-secret-0123456789abcdef"
CLUSTER = "ha-d1-ci"
A_URL = "http://127.0.0.1:6211"
B_URL = "http://127.0.0.1:6212"
REPLICATION_PATH = "/api/runtime/ha/peer/replicate"
TAG_ID = "33333333-3333-3333-3333-333333333333"


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def node_env(node_id: str, own_url: str, peer_url: str, state_path: Path, reference_path: Path) -> dict[str, str]:
    env = os.environ.copy()
    env.update({
        "ASPNETCORE_URLS": own_url,
        "Authentication__Enabled": "false",
        "HighAvailability__Enabled": "true",
        "HighAvailability__ClusterId": CLUSTER,
        "HighAvailability__NodeId": node_id,
        "HighAvailability__InitialActiveNodeId": "node-a",
        "HighAvailability__TopologyVersion": "3",
        "HighAvailability__FreshnessSeconds": "4",
        "HighAvailability__Nodes__0__NodeId": "node-a",
        "HighAvailability__Nodes__0__LocalEndpoint": A_URL,
        "HighAvailability__Nodes__0__RemoteEndpoint": A_URL,
        "HighAvailability__Nodes__1__NodeId": "node-b",
        "HighAvailability__Nodes__1__LocalEndpoint": B_URL,
        "HighAvailability__Nodes__1__RemoteEndpoint": B_URL,
        "HighAvailability__PeerTransport__Enabled": "true",
        "HighAvailability__PeerTransport__PeerEndpoint": peer_url + REPLICATION_PATH,
        "HighAvailability__PeerTransport__SharedSecret": SECRET,
        "HighAvailability__PeerTransport__StatePath": str(state_path),
        "HighAvailability__PeerTransport__PollMilliseconds": "500",
        "HighAvailability__PeerTransport__RequestTimeoutSeconds": "1",
        "HighAvailability__PeerTransport__MaximumRetrySeconds": "2",
        "HighAvailability__PeerTransport__AuthenticationFreshnessSeconds": "10",
        "HighAvailability__Protection__Enabled": "true",
        "HighAvailability__Protection__AutomaticFailoverEnabled": "true",
        "HighAvailability__Protection__ReferencePath": str(reference_path),
        "HighAvailability__Protection__LeaseSeconds": "4",
        "HighAvailability__Protection__PollMilliseconds": "250",
        "HighAvailability__Protection__ReadyWitnessMaximumAgeSeconds": "20",
        "Logging__LogLevel__Default": "Warning",
    })
    return env


def http_json(url: str, method: str = "GET", body: bytes | None = None,
              headers: dict[str, str] | None = None, timeout: float = 2.0) -> tuple[int, object]:
    request = urllib.request.Request(url, data=body, method=method)
    if body is not None:
        request.add_header("Content-Type", "application/json")
    for key, value in (headers or {}).items():
        request.add_header(key, value)
    try:
        with urllib.request.urlopen(request, timeout=timeout) as response:
            raw = response.read()
            return response.status, json.loads(raw) if raw else None
    except urllib.error.HTTPError as error:
        raw = error.read()
        return error.code, json.loads(raw) if raw else None


def wait_health(url: str, timeout: float = 20.0) -> None:
    deadline = time.monotonic() + timeout
    last = None
    while time.monotonic() < deadline:
        try:
            status, payload = http_json(url + "/health", timeout=1.0)
            last = (status, payload)
            if status == 200 and payload.get("status") == "ok":
                return
        except (OSError, urllib.error.URLError, TimeoutError):
            pass
        time.sleep(0.25)
    raise AssertionError(f"Timed out waiting for {url}/health; last={last!r}")


def sign(body: bytes, node_id: str, nonce: str | None = None) -> dict[str, str]:
    timestamp = str(int(time.time() * 1000))
    nonce = nonce or uuid.uuid4().hex
    body_hash = hashlib.sha256(body).hexdigest()
    canonical = "\n".join(("POST", REPLICATION_PATH, timestamp, nonce, body_hash))
    signature = base64.b64encode(
        hmac.new(SECRET.encode(), canonical.encode(), hashlib.sha256).digest()
    ).decode()
    return {
        "X-EliteSCADA-HA-Node": node_id,
        "X-EliteSCADA-HA-Timestamp": timestamp,
        "X-EliteSCADA-HA-Nonce": nonce,
        "X-EliteSCADA-HA-Signature": signature,
    }


def dummy_observation(source_node: str, now: str) -> dict:
    return {
        "schema": "elitescada.runtime-ha-peer-observation",
        "schemaVersion": 1,
        "clusterId": CLUSTER,
        "topologyVersion": 3,
        "sourceNodeId": source_node,
        "sourceObservationInstanceId": str(uuid.uuid4()),
        "observationSequence": 1,
        "sourceAuthorityInstanceId": str(uuid.uuid4()),
        "authorityEpoch": 1,
        "effectiveActiveNodeId": "node-a",
        "ambiguousAuthority": False,
        "readiness": {
            "healthy": False,
            "synchronizationComplete": False,
            "haLicenseEntitled": False,
            "runtime": {"mode": "unknown", "projectKey": None, "revision": None},
            "observedAtUtc": now,
            "diagnostic": "evidence-probe",
        },
        "observedAtUtc": now,
    }


def topology_probe(target_url: str, source_node: str) -> dict:
    """Read topology through an authenticated request rejected before state mutation."""
    now = utc_now()
    envelope = {
        "schema": "elitescada.runtime-ha-evidence-probe.invalid",
        "schemaVersion": 1,
        "clusterId": CLUSTER,
        "topologyVersion": 3,
        "sourceNodeId": source_node,
        "sourceTransportInstanceId": str(uuid.uuid4()),
        "replicationSequence": 1,
        "observation": dummy_observation(source_node, now),
        "authoritativeState": None,
        "sentAtUtc": now,
    }
    body = json.dumps(envelope, separators=(",", ":")).encode()
    status, payload = http_json(
        target_url + REPLICATION_PATH,
        "POST",
        body,
        sign(body, source_node),
    )
    assert status == 409, (status, payload)
    assert payload["reasonCode"] == "peer-replication-schema-mismatch", payload
    return payload["topology"]


def wait_topology(target_url: str, source_node: str, predicate,
                  timeout: float = 20.0) -> dict:
    deadline = time.monotonic() + timeout
    last = None
    while time.monotonic() < deadline:
        try:
            topology = topology_probe(target_url, source_node)
            last = topology
            if predicate(topology):
                return topology
        except (OSError, urllib.error.URLError, TimeoutError, AssertionError):
            pass
        time.sleep(0.25)
    raise AssertionError(f"Timed out waiting for topology at {target_url}; last={last!r}")


def node(topology: dict, node_id: str) -> dict:
    return next(item for item in topology["nodes"] if item["nodeId"] == node_id)


def tail(path: Path, lines: int = 80) -> str:
    try:
        return "\n".join(path.read_text(encoding="utf-8", errors="replace").splitlines()[-lines:])
    except OSError:
        return "<log unavailable>"


def terminate(process: subprocess.Popen | None) -> None:
    if process is None or process.poll() is not None:
        return
    process.terminate()
    try:
        process.wait(timeout=5)
    except subprocess.TimeoutExpired:
        process.kill()
        process.wait(timeout=5)


def main() -> int:
    if not API_DLL.exists():
        raise AssertionError(f"Scada.Api exact-head build is missing: {API_DLL}")

    with tempfile.TemporaryDirectory(prefix="elitescada-ha-d1-") as tmp:
        temp = Path(tmp)
        log_a = temp / "node-a.log"
        log_b = temp / "node-b.log"
        state_a = temp / "node-a-state.json"
        state_b = temp / "node-b-state.json"
        reference = temp / "ha-reference.json"
        proc_a = proc_b = None
        a_paused = False
        out_a = log_a.open("w", encoding="utf-8")
        out_b = log_b.open("w", encoding="utf-8")
        try:
            proc_a = subprocess.Popen(
                ["dotnet", str(API_DLL)],
                cwd=ROOT,
                env=node_env("node-a", A_URL, B_URL, state_a, reference),
                stdout=out_a,
                stderr=subprocess.STDOUT,
                text=True,
            )
            proc_b = subprocess.Popen(
                ["dotnet", str(API_DLL)],
                cwd=ROOT,
                env=node_env("node-b", B_URL, A_URL, state_b, reference),
                stdout=out_b,
                stderr=subprocess.STDOUT,
                text=True,
            )
            assert proc_a.pid != proc_b.pid

            wait_health(A_URL)
            wait_health(B_URL)

            reference_deadline = time.monotonic() + 10.0
            reference_state = None
            while time.monotonic() < reference_deadline:
                if reference.exists():
                    reference_state = json.loads(reference.read_text(encoding="utf-8"))
                    if reference_state.get("activeNodeId") == "node-a":
                        break
                time.sleep(0.1)
            assert reference_state is not None, "HA-D2 reference authority was not initialized"
            assert reference_state["activeNodeId"] == "node-a", reference_state
            assert reference_state["epoch"] == 1, reference_state

            # Probes are rejected before ApplyPeerObservation. Therefore peer Fresh=true
            # can only have been established by the two running server processes.
            topology_a = wait_topology(
                A_URL,
                "node-b",
                lambda value: node(value, "node-b")["fresh"] is True,
            )
            topology_b = wait_topology(
                B_URL,
                "node-a",
                lambda value: node(value, "node-a")["fresh"] is True,
            )
            b_local = node(topology_b, "node-b")
            assert b_local["state"] != 5, b_local
            assert not b_local["ready"], b_local

            now = utc_now()
            envelope = {
                "schema": "elitescada.runtime-ha-peer-replication",
                "schemaVersion": 1,
                "clusterId": CLUSTER,
                "topologyVersion": 3,
                "sourceNodeId": "node-a",
                "sourceTransportInstanceId": str(uuid.uuid4()),
                "replicationSequence": 1000,
                "observation": {
                    "schema": "elitescada.runtime-ha-peer-observation",
                    "schemaVersion": 1,
                    "clusterId": CLUSTER,
                    "topologyVersion": 3,
                    "sourceNodeId": "node-a",
                    "sourceObservationInstanceId": str(uuid.uuid4()),
                    "observationSequence": 1000,
                    "sourceAuthorityInstanceId": topology_a["authorityInstanceId"],
                    "authorityEpoch": topology_a["authorityEpoch"],
                    "effectiveActiveNodeId": "node-a",
                    "ambiguousAuthority": False,
                    "readiness": {
                        "healthy": True,
                        "synchronizationComplete": True,
                        "haLicenseEntitled": True,
                        "runtime": {"mode": "engineering", "projectKey": "project-a", "revision": 7},
                        "observedAtUtc": now,
                        "diagnostic": None,
                    },
                    "observedAtUtc": now,
                },
                "authoritativeState": {
                    "runtime": {"mode": "engineering", "projectKey": "project-a", "revision": 7},
                    "projectTagCount": 1,
                    "runtimeActivatedAtUtc": now,
                    "application": {
                        "schema": "scada.engineering",
                        "schemaVersion": 20,
                        "exportedAt": now,
                        "tags": [{
                            "id": TAG_ID,
                            "name": "HA Evidence",
                            "path": "HA.Evidence",
                            "dataType": 2,
                            "readOnly": True,
                        }],
                        "alarms": [],
                    },
                    "license": {
                        "licenseValid": True,
                        "haRuntimeEntitled": True,
                        "maximumTags": 500,
                        "interactiveSeats": 2,
                        "viewOnlySeats": 2,
                    },
                    "tags": [{
                        "tagId": TAG_ID,
                        "value": 42,
                        "timestamp": now,
                        "quality": 0,
                        "source": "ha-d1-evidence",
                        "sourceTimestamp": None,
                        "serverTimestamp": None,
                    }],
                    "sessions": [],
                    "sessionTombstones": [],
                    "capturedAtUtc": now,
                },
                "sentAtUtc": now,
            }
            body = json.dumps(envelope, separators=(",", ":")).encode()
            auth_headers = sign(body, "node-a")
            first_status, first_payload = http_json(
                B_URL + REPLICATION_PATH, "POST", body, auth_headers)
            assert first_status == 200, (first_status, first_payload)

            deadline = time.monotonic() + 5.0
            persisted = None
            while time.monotonic() < deadline:
                if state_b.exists():
                    persisted = json.loads(state_b.read_text(encoding="utf-8"))
                    break
                time.sleep(0.1)
            assert persisted is not None, "Standby durable mirror was not written"
            mirrored_tags = persisted["authoritativeState"]["tags"]
            assert any(item["tagId"] == TAG_ID for item in mirrored_tags), mirrored_tags

            current_status, current_tags = http_json(B_URL + "/api/tags/current")
            assert current_status == 200, (current_status, current_tags)
            assert current_tags == [], current_tags

            b_after_mirror = node(first_payload["topology"], "node-b")
            assert b_after_mirror["state"] != 5, b_after_mirror
            assert not b_after_mirror["ready"], b_after_mirror
            assert b_after_mirror["readinessReason"] in {
                "local-license-invalid",
                "ha-license-not-entitled",
                "passive-runtime-not-materialized",
                "passive-runtime-not-compatible",
            }, b_after_mirror

            replay_status, replay_payload = http_json(
                B_URL + REPLICATION_PATH, "POST", body, auth_headers)
            assert replay_status == 401, (replay_status, replay_payload)
            assert replay_payload["code"] == "peer-auth-replay", replay_payload

            os.kill(proc_a.pid, signal.SIGSTOP)
            a_paused = True
            stale_topology = wait_topology(
                B_URL,
                "node-a",
                lambda value: node(value, "node-a")["fresh"] is False,
                timeout=15.0,
            )
            stale_local = node(stale_topology, "node-b")
            assert stale_local["state"] != 5, stale_local
            assert stale_topology["effectiveActiveNodeId"] != "node-b", stale_topology

            lease_deadline = time.monotonic() + 10.0
            expired_reference = None
            while time.monotonic() < lease_deadline:
                current_reference = json.loads(reference.read_text(encoding="utf-8"))
                lease_until = datetime.fromisoformat(
                    current_reference["leaseUntilUtc"].replace("Z", "+00:00"))
                if lease_until <= datetime.now(timezone.utc):
                    expired_reference = current_reference
                    break
                time.sleep(0.1)
            assert expired_reference is not None, "HA-D2 reference lease did not expire"
            assert expired_reference["activeNodeId"] == "node-a", expired_reference

            post_expiry = wait_topology(
                B_URL,
                "node-a",
                lambda value: value["effectiveActiveNodeId"] != "node-b",
                timeout=5.0,
            )
            assert post_expiry["effectiveActiveNodeId"] != "node-b", post_expiry

            os.kill(proc_a.pid, signal.SIGCONT)
            a_paused = False
            resynced_topology = wait_topology(
                B_URL,
                "node-a",
                lambda value: node(value, "node-a")["fresh"] is True,
                timeout=20.0,
            )
            resynced_local = node(resynced_topology, "node-b")
            assert resynced_local["state"] != 5, resynced_local
            assert not resynced_local["ready"], resynced_local

            print(json.dumps({
                "processes": {
                    "nodeA": proc_a.pid,
                    "nodeB": proc_b.pid,
                    "distinct": True,
                },
                "realPeerTransport": {
                    "nodeAObservedNodeBFresh": node(topology_a, "node-b")["fresh"],
                    "nodeBObservedNodeAFresh": node(topology_b, "node-a")["fresh"],
                },
                "authenticatedMirror": {
                    "httpStatus": first_status,
                    "accepted": first_payload["accepted"],
                    "tagPersistedInStandbyMirror": True,
                    "tagExecutedInStandbyRuntime": False,
                },
                "replay": {"httpStatus": replay_status, "code": replay_payload["code"]},
                "disconnect": {
                    "peerFresh": node(stale_topology, "node-a")["fresh"],
                    "standbyPromoted": stale_topology["effectiveActiveNodeId"] == "node-b",
                    "referenceLeaseExpired": expired_reference is not None,
                    "promotionWithoutReadyWitness": post_expiry["effectiveActiveNodeId"] == "node-b",
                },
                "reconnect": {
                    "peerFresh": node(resynced_topology, "node-a")["fresh"],
                },
                "readyStandby": {
                    "ready": resynced_local["ready"],
                    "state": resynced_local["state"],
                    "reason": resynced_local["readinessReason"],
                    "note": "Fail-closed: peer state alone never grants Standby authority.",
                },
            }, indent=2, sort_keys=True))
            return 0
        except Exception:
            print("=== node-a log tail ===", file=sys.stderr)
            print(tail(log_a), file=sys.stderr)
            print("=== node-b log tail ===", file=sys.stderr)
            print(tail(log_b), file=sys.stderr)
            raise
        finally:
            if a_paused and proc_a is not None and proc_a.poll() is None:
                os.kill(proc_a.pid, signal.SIGCONT)
            terminate(proc_a)
            terminate(proc_b)
            out_a.close()
            out_b.close()


if __name__ == "__main__":
    raise SystemExit(main())
