"""Managed XKNX child process. Secrets are supplied by the host, never by project packages."""

from __future__ import annotations

import asyncio
import json
import logging
import math
import os
import platform
import queue
import re
import sys
import threading
from datetime import datetime, timezone

from xknx import XKNX
from xknx.dpt import DPTBase
from xknx.telegram import GroupAddress, Telegram, TelegramDirection
from xknx.telegram.apci import GroupValueResponse, GroupValueWrite

from security import KnxConfigurationError, create_xknx_connection, decode_secure_value, read_secure_group_value

ARTIFACT_ID = "elitescada.knx.ip"
ARTIFACT_VERSION = "1.0.0"
RUNTIME_VERSION = "3.13.x"
SCHEMA_VERSION = 1
MAX_FRAME_LENGTH = 65_536
MAX_BOOTSTRAP_FRAME_LENGTH = 2 * 1024 * 1024


def _read_bootstrap(stream) -> tuple[dict[str, str], dict[str, str], list[dict]]:
    raw = stream.readline(MAX_BOOTSTRAP_FRAME_LENGTH + 1)
    if not raw or len(raw) > MAX_BOOTSTRAP_FRAME_LENGTH or not raw.endswith(b"\n"):
        raise KnxConfigurationError("KNX_BOOTSTRAP_FRAME_INVALID")
    try:
        bootstrap = json.loads(raw)
    except Exception:
        raise KnxConfigurationError("KNX_BOOTSTRAP_JSON_INVALID") from None
    if not isinstance(bootstrap, dict) or set(bootstrap) != {"type", "configuration", "secrets", "points"} or bootstrap.get("type") != "bootstrap":
        raise KnxConfigurationError("KNX_BOOTSTRAP_INVALID")
    public = bootstrap["configuration"]
    secrets = bootstrap["secrets"]
    points = bootstrap["points"]
    if not isinstance(public, dict) or set(public) != {"gatewayHost", "gatewayPort", "individualAddress", "userId"}:
        raise KnxConfigurationError("KNX_BOOTSTRAP_CONFIGURATION_INVALID")
    if not isinstance(secrets, dict) or set(secrets) != {"userPassword", "deviceAuthentication", "dataSecureKeyringBundle"}:
        raise KnxConfigurationError("KNX_BOOTSTRAP_SECRET_FIELDS_INVALID")
    if not isinstance(points, list):
        raise KnxConfigurationError("KNX_POINT_CONFIG_INVALID")

    rpc_host = os.environ.pop("KNX_RPC_HOST", "")
    rpc_port_text = os.environ.pop("KNX_RPC_PORT", "")
    rpc_token = os.environ.pop("KNX_RPC_TOKEN", "")
    if not rpc_host or rpc_host not in ("127.0.0.1", "::1") or not re.fullmatch(r"[0-9a-f]{64}", rpc_token):
        raise KnxConfigurationError("KNX_RPC_CONFIGURATION_INVALID")
    if not re.fullmatch(r"[0-9]{1,5}", rpc_port_text) or not 1 <= int(rpc_port_text) <= 65535:
        raise KnxConfigurationError("KNX_RPC_CONFIGURATION_INVALID")
    return public, secrets, points, rpc_host, rpc_port_text, rpc_token


def _read_environment(stream=None) -> tuple[dict[str, str], dict[str, str], list[dict], str, str, str]:
    public, secrets, points, rpc_host, rpc_port, rpc_token = _read_bootstrap(
        sys.stdin.buffer if stream is None else stream
    )
    return public, secrets, points, rpc_host, rpc_port, rpc_token


def _write_json(stream, value: dict) -> None:
    stream.write((json.dumps(value, separators=(",", ":"), ensure_ascii=True) + "\n").encode("utf-8"))
    stream.flush()


async def _read_frame(reader: asyncio.StreamReader, timeout: float = 10.0) -> dict:
    try:
        raw = await asyncio.wait_for(reader.readline(), timeout=timeout)
    except asyncio.TimeoutError:
        raise KnxConfigurationError("KNX_RPC_TIMEOUT") from None
    if not raw or len(raw) > MAX_FRAME_LENGTH or not raw.endswith(b"\n"):
        raise KnxConfigurationError("KNX_RPC_FRAME_INVALID")
    try:
        message = json.loads(raw)
    except Exception:
        raise KnxConfigurationError("KNX_RPC_JSON_INVALID") from None
    if not isinstance(message, dict):
        raise KnxConfigurationError("KNX_RPC_MESSAGE_INVALID")
    return message


async def _connect_host(host: str, port_text: str, token: str):
    try:
        port = int(port_text)
        reader, writer = await asyncio.wait_for(asyncio.open_connection(host, port), timeout=5.0)
    except Exception:
        raise KnxConfigurationError("KNX_RPC_UNAVAILABLE") from None
    _write_json(writer, {
        "type": "hello",
        "token": token,
        "artifactId": ARTIFACT_ID,
        "artifactVersion": ARTIFACT_VERSION,
        "runtimeVersion": RUNTIME_VERSION,
        "schemaVersion": SCHEMA_VERSION,
    })
    welcome = await _read_frame(reader, timeout=5.0)
    if welcome.get("type") != "welcome" or welcome.get("schemaVersion") != SCHEMA_VERSION:
        writer.close()
        await writer.wait_closed()
        raise KnxConfigurationError("KNX_RPC_HOST_REJECTED")
    return reader, writer


def _safe_error_code(error: Exception) -> str:
    if isinstance(error, KnxConfigurationError) and re.fullmatch(r"[A-Z0-9_]{1,96}", str(error)):
        return str(error)
    return "KNX_SECURE_OPERATION_FAILED"


async def run_sidecar(env=None, connection_builder=create_xknx_connection, bootstrap_stream=None):
    environment = os.environ if env is None else env
    if environment is not os.environ:
        original_environment = os.environ.copy()
        os.environ.clear()
        os.environ.update(environment)
    else:
        original_environment = None

    xknx = None
    rpc_writer = None
    report_task = None
    ready_sent = False
    try:
        logging.disable(logging.CRITICAL)
        public, secrets, points, rpc_host, rpc_port, rpc_token = await asyncio.to_thread(_read_environment, bootstrap_stream)
        xknx, group_keys = connection_builder(public, secrets, points)
        reports: asyncio.Queue[dict] = asyncio.Queue(maxsize=1024)
        point_by_state = {}
        for point in points:
            point_by_state.setdefault(point["stateGroupAddress"], []).append(point)

        def on_telegram(telegram):
            if getattr(telegram, "direction", None) != TelegramDirection.INCOMING:
                return
            if getattr(telegram, "data_secure", None) is not True:
                return
            if not isinstance(telegram.payload, (GroupValueWrite, GroupValueResponse)):
                return
            address = str(telegram.destination_address)
            for point in point_by_state.get(address, ()):
                try:
                    value = decode_secure_value(point["dpt"], telegram.payload.value)
                    reports.put_nowait({
                        "type": "report",
                        "tagId": point["tagId"],
                        "source": "data-secure-status",
                        "secure": True,
                        "value": value if isinstance(value, bool) else float(value),
                        "observedAt": datetime.now(timezone.utc).isoformat(),
                    })
                except (asyncio.QueueFull, Exception):
                    # Missing/overflowed state notifications never create a value or a false success.
                    continue

        xknx.telegram_queue.register_telegram_received_cb(
            on_telegram,
            group_addresses=[GroupAddress(point["stateGroupAddress"]) for point in points],
            match_for_outgoing=False,
        )
        xknx.telegram_queue.register_data_secure_group_key_issue_cb(lambda _telegram: None)
        await asyncio.wait_for(xknx.start(), timeout=20.0)
        rpc_reader, rpc_writer = await _connect_host(rpc_host, rpc_port, rpc_token)

        async def report_sender():
            while True:
                report = await reports.get()
                _write_json(rpc_writer, report)
                await rpc_writer.drain()
                reports.task_done()

        report_task = asyncio.create_task(report_sender())
        _write_json(sys.stdout.buffer, {
            "type": "ready",
            "artifactId": ARTIFACT_ID,
            "artifactVersion": ARTIFACT_VERSION,
            "runtimeVersion": RUNTIME_VERSION,
            "schemaVersion": SCHEMA_VERSION,
            "message": "Explicit Secure TCP tunnel and configured Data Secure keys are active.",
        })
        ready_sent = True

        loop = asyncio.get_running_loop()
        stopped = asyncio.Event()
        stop_thread = threading.Thread(
            target=lambda: _wait_for_stop(loop, stopped), name="knx-sidecar-stop", daemon=True
        )
        stop_thread.start()

        try:
            while not stopped.is_set():
                read_task = asyncio.create_task(_read_frame(rpc_reader, timeout=60.0))
                stop_task = asyncio.create_task(stopped.wait())
                done, pending = await asyncio.wait((read_task, stop_task), return_when=asyncio.FIRST_COMPLETED)
                for task in pending:
                    task.cancel()
                await asyncio.gather(*pending, return_exceptions=True)
                if stop_task in done:
                    break
                try:
                    request = read_task.result()
                except KnxConfigurationError as error:
                    if "TIMEOUT" in str(error):
                        continue
                    break
                await _handle_request(xknx, points, request, rpc_writer)
        finally:
            # Stop XKNX before draining the callback channel so no new status
            # telegrams can be enqueued while the child is shutting down.
            if xknx is not None:
                await xknx.stop()
                xknx = None
            if report_task is not None:
                report_task.cancel()
                await asyncio.gather(report_task, return_exceptions=True)
            rpc_writer.close()
            await rpc_writer.wait_closed()
            rpc_writer = None
        return 0
    except BaseException as error:
        if ready_sent:
            print(_safe_error_code(error), file=sys.stderr, flush=True)
        else:
            print(_safe_error_code(error), file=sys.stderr, flush=True)
        if rpc_writer is not None:
            rpc_writer.close()
            try:
                await rpc_writer.wait_closed()
            except Exception:
                pass
        if xknx is not None:
            try:
                await xknx.stop()
            except Exception:
                pass
        return 1
    finally:
        for key in ("KNX_RPC_TOKEN",):
            os.environ.pop(key, None)
        if original_environment is not None:
            os.environ.clear()
            os.environ.update(original_environment)


def _wait_for_stop(loop: asyncio.AbstractEventLoop, stopped: asyncio.Event) -> None:
    while True:
        line = sys.stdin.buffer.readline(1024)
        if not line or line.rstrip(b"\r\n") == b"stop":
            loop.call_soon_threadsafe(stopped.set)
            return


async def _handle_request(xknx: XKNX, points: list[dict], request: dict, writer: asyncio.StreamWriter) -> None:
    request_id = request.get("id")
    response = {"type": "response", "id": request_id, "ok": False, "accepted": False, "errorCode": "KNX_RPC_REQUEST_INVALID"}
    try:
        if set(request) - {"type", "id", "method", "tagId", "value"} or request.get("type") != "request":
            raise KnxConfigurationError("KNX_RPC_REQUEST_INVALID")
        point = next((item for item in points if item["tagId"] == request.get("tagId")), None)
        if point is None:
            raise KnxConfigurationError("KNX_POINT_NOT_CONFIGURED")
        method = request.get("method")
        if method == "read":
            if "value" in request and request["value"] is not None:
                raise KnxConfigurationError("KNX_READ_ARGUMENT_INVALID")
            value = await read_secure_group_value(xknx, point)
            response = {"type": "response", "id": request_id, "ok": True, "accepted": False, "value": value}
        elif method == "write":
            if not point["writable"]:
                raise KnxConfigurationError("KNX_WRITE_NOT_ALLOWED")
            value = request.get("value")
            transcoder = DPTBase.parse_transcoder(point["dpt"])
            if point["dpt"] in ("1.001", "1.002"):
                if not isinstance(value, bool):
                    raise KnxConfigurationError("KNX_VALUE_TYPE_INVALID")
            elif isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(float(value)):
                raise KnxConfigurationError("KNX_VALUE_TYPE_INVALID")
            try:
                payload = transcoder.to_knx(value)
            except Exception:
                raise KnxConfigurationError("KNX_VALUE_OUT_OF_RANGE") from None
            telegram = Telegram(
                destination_address=GroupAddress(point["writeGroupAddress"]),
                payload=GroupValueWrite(payload),
                source_address=xknx.current_address,
            )
            xknx.telegrams.put_nowait(telegram)
            await asyncio.wait_for(xknx.join(), timeout=5.0)
            readback = await read_secure_group_value(xknx, point)
            response = {"type": "response", "id": request_id, "ok": True, "accepted": True, "value": readback}
        else:
            raise KnxConfigurationError("KNX_OPERATION_UNSUPPORTED")
    except Exception as error:
        response["errorCode"] = _safe_error_code(error)
    _write_json(writer, response)
    await writer.drain()


if __name__ == "__main__":
    raise SystemExit(asyncio.run(run_sidecar()))
