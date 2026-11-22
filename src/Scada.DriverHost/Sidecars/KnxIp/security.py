"""Fail-closed Secure configuration helpers for the XKNX sidecar."""

from __future__ import annotations

import base64
import hashlib
import hmac
import ipaddress
import json
import math
import re
import uuid
import xml.sax
from xml.dom import minidom

from xknx import XKNX
from xknx.core import ValueReader
from xknx.dpt import DPTBase
from xknx.io import ConnectionConfig, ConnectionType, SecureConfig
from xknx.secure.keyring import InterfaceType, Keyring, KeyringSAXContentHandler
from xknx.telegram import GroupAddress

SUPPORTED_DPTS = frozenset({"1.001", "1.002", "5.001", "9.001", "9.004", "9.007", "9.024"})
MAX_KEYRING_BYTES = 1024 * 1024
MAX_POINT_COUNT = 512
_INDIVIDUAL_ADDRESS = re.compile(r"^(\d{1,2})\.(\d{1,2})\.(\d{1,3})$")
_GROUP_ADDRESS = re.compile(r"^(\d{1,2})/(\d{1,2})/(\d{1,3})$")
_DNS_NAME = re.compile(r"(?=^.{1,253}\.?$)(?:[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?\.)*[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?\.?$")


class KnxConfigurationError(ValueError):
    """An intentionally sanitized configuration error code."""


def _valid_individual_address(value: str) -> bool:
    match = _INDIVIDUAL_ADDRESS.fullmatch(value or "")
    return bool(match and 0 <= int(match[1]) <= 15 and 0 <= int(match[2]) <= 15 and 1 <= int(match[3]) <= 255)


def _valid_group_address(value: str) -> bool:
    match = _GROUP_ADDRESS.fullmatch(value or "")
    return bool(match and 0 <= int(match[1]) <= 31 and 0 <= int(match[2]) <= 7 and 0 <= int(match[3]) <= 255)


def _valid_host(value: str) -> bool:
    try:
        ipaddress.ip_address(value)
        return True
    except ValueError:
        return bool(_DNS_NAME.fullmatch(value))


def _reject_duplicate_json_fields(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise KnxConfigurationError("KNX_KEYRING_BUNDLE_INVALID")
        result[key] = value
    return result


def parse_keyring_bundle(bundle_b64: str) -> Keyring:
    """Verify the ETS signature and decrypt a keyring directly from memory."""
    bundle_bytes = bytearray()
    xml_bytes = bytearray()
    password_bytes = bytearray()
    try:
        bundle_bytes.extend(base64.b64decode(bundle_b64, validate=True))
        bundle = json.loads(bundle_bytes.decode("utf-8", errors="strict"), object_pairs_hook=_reject_duplicate_json_fields)
        if not isinstance(bundle, dict) or set(bundle) != {"format", "knxkeysBase64", "keyringPassword"}:
            raise KnxConfigurationError("KNX_KEYRING_BUNDLE_INVALID")
        if bundle["format"] != "elitescada.knxkeys-bundle.v1" or not isinstance(bundle["knxkeysBase64"], str):
            raise KnxConfigurationError("KNX_KEYRING_BUNDLE_INVALID")
        if not isinstance(bundle["keyringPassword"], str) or not bundle["keyringPassword"]:
            raise KnxConfigurationError("KNX_KEYRING_BUNDLE_INVALID")
        if len(bundle["keyringPassword"]) > 4096 or any(character in "\r\n\x00" for character in bundle["keyringPassword"]):
            raise KnxConfigurationError("KNX_KEYRING_BUNDLE_INVALID")
        password_bytes.extend(bundle["keyringPassword"].encode("utf-8", errors="strict"))
        encoded_xml = bundle["knxkeysBase64"]
        if len(encoded_xml) > 1_398_104:
            raise KnxConfigurationError("KNX_KEYRING_TOO_LARGE")
        xml_bytes.extend(base64.b64decode(encoded_xml, validate=True))
        if not xml_bytes or len(xml_bytes) > MAX_KEYRING_BYTES:
            raise KnxConfigurationError("KNX_KEYRING_TOO_LARGE")
        lowered = bytes(xml_bytes).lower()
        if b"<!doctype" in lowered or b"<!entity" in lowered:
            raise KnxConfigurationError("KNX_KEYRING_XML_UNSUPPORTED")

        password = bytes(password_bytes).decode("utf-8", errors="strict")
        document = minidom.parseString(bytes(xml_bytes))
        root = document.documentElement
        if root.nodeName != "Keyring":
            raise KnxConfigurationError("KNX_KEYRING_XML_INVALID")
        signature = base64.b64decode(root.getAttribute("Signature"), validate=True)
        handler = KeyringSAXContentHandler(password)
        xml.sax.parseString(bytes(xml_bytes), handler)
        expected_signature = hashlib.sha256(handler.output).digest()[:16]
        if not hmac.compare_digest(signature, expected_signature):
            raise KnxConfigurationError("KNX_KEYRING_SIGNATURE_INVALID")

        keyring = Keyring()
        keyring.parse_xml(root)
        keyring.decrypt(password)
        return keyring
    except KnxConfigurationError:
        raise
    except Exception:
        raise KnxConfigurationError("KNX_KEYRING_INVALID") from None
    finally:
        for buffer in (bundle_bytes, xml_bytes, password_bytes):
            _zero(buffer)


def _zero(buffer: bytearray) -> None:
    for index in range(len(buffer)):
        buffer[index] = 0


def validate_connection_settings(config: dict[str, str], points: list[dict]) -> None:
    if not isinstance(config, dict):
        raise KnxConfigurationError("KNX_CONFIG_INVALID")
    host = config.get("gatewayHost", "")
    if not isinstance(host, str) or not _valid_host(host):
        raise KnxConfigurationError("KNX_GATEWAY_INVALID")
    port_text = config.get("gatewayPort", "")
    user_id_text = config.get("userId", "")
    if not isinstance(port_text, str) or not re.fullmatch(r"[0-9]{1,5}", port_text) or not isinstance(user_id_text, str) or not re.fullmatch(r"[0-9]{1,3}", user_id_text):
        raise KnxConfigurationError("KNX_CONFIG_INVALID") from None
    port = int(port_text)
    user_id = int(user_id_text)
    if not 1 <= port <= 65535 or not 1 <= user_id <= 127:
        raise KnxConfigurationError("KNX_CONFIG_INVALID")
    individual_address = config.get("individualAddress", "")
    if not isinstance(individual_address, str) or not _valid_individual_address(individual_address):
        raise KnxConfigurationError("KNX_INDIVIDUAL_ADDRESS_INVALID")
    if not isinstance(points, list) or not 1 <= len(points) <= MAX_POINT_COUNT:
        raise KnxConfigurationError("KNX_POINT_COUNT_INVALID")

    seen = set()
    writable_addresses = set()
    state_addresses = set()
    for point in points:
        if not isinstance(point, dict) or set(point) != {"tagId", "dpt", "writeGroupAddress", "stateGroupAddress", "writable"}:
            raise KnxConfigurationError("KNX_POINT_CONFIG_INVALID")
        write_address = point.get("writeGroupAddress")
        state_address = point.get("stateGroupAddress")
        if not isinstance(write_address, str) or not _valid_group_address(write_address) or not isinstance(state_address, str) or not _valid_group_address(state_address):
            raise KnxConfigurationError("KNX_GROUP_ADDRESS_INVALID")
        if point.get("dpt") not in SUPPORTED_DPTS or not isinstance(point.get("writable"), bool):
            raise KnxConfigurationError("KNX_DPT_UNSUPPORTED")
        if not isinstance(point.get("tagId"), str) or point["tagId"] in seen:
            raise KnxConfigurationError("KNX_POINT_ID_INVALID")
        try:
            if str(uuid.UUID(point["tagId"])) != point["tagId"]:
                raise ValueError
        except (ValueError, AttributeError):
            raise KnxConfigurationError("KNX_POINT_ID_INVALID") from None
        seen.add(point["tagId"])
        state_addresses.add(state_address)
        if point["writable"] and write_address == state_address:
            raise KnxConfigurationError("KNX_WRITE_STATE_ADDRESS_COLLISION")
        if point["writable"] and write_address in writable_addresses:
            raise KnxConfigurationError("KNX_WRITE_ADDRESS_DUPLICATE")
        if point["writable"]:
            writable_addresses.add(write_address)
        DPTBase.parse_transcoder(point["dpt"])
    if writable_addresses.intersection(state_addresses):
        raise KnxConfigurationError("KNX_WRITE_STATE_ADDRESS_COLLISION")


def create_xknx_connection(config: dict[str, str], secrets: dict[str, str], points: list[dict]):
    """Build only an explicit Secure TCP tunnel, rejecting any plain fallback."""
    validate_connection_settings(config, points)
    if not isinstance(secrets, dict) or not all(
        isinstance(secrets.get(key), str) and secrets[key]
        for key in ("userPassword", "deviceAuthentication", "dataSecureKeyringBundle")
    ):
        raise KnxConfigurationError("KNX_SECURE_MATERIAL_REQUIRED")
    if any(len(secrets[key]) > 4096 or any(character in "\r\n\x00" for character in secrets[key])
           for key in ("userPassword", "deviceAuthentication")):
        raise KnxConfigurationError("KNX_SECURE_MATERIAL_INVALID")
    keyring = parse_keyring_bundle(secrets["dataSecureKeyringBundle"])
    try:
        selected_interface = next((
            interface for interface in keyring.interfaces
            if interface.type is InterfaceType.TUNNELING and
            str(interface.individual_address) == config["individualAddress"] and
            interface.user_id == int(config["userId"])
        ), None)
        if selected_interface is None:
            raise KnxConfigurationError("KNX_SECURE_TUNNEL_INTERFACE_NOT_IN_KEYRING")
        if not selected_interface.decrypted_password or not selected_interface.decrypted_authentication:
            raise KnxConfigurationError("KNX_SECURE_TUNNEL_CREDENTIALS_MISSING_FROM_KEYRING")
        if not hmac.compare_digest(
            secrets["userPassword"].encode("utf-8"),
            selected_interface.decrypted_password.encode("utf-8"),
        ) or not hmac.compare_digest(
            secrets["deviceAuthentication"].encode("utf-8"),
            selected_interface.decrypted_authentication.encode("utf-8"),
        ):
            raise KnxConfigurationError("KNX_SECURE_TUNNEL_CREDENTIAL_MISMATCH")
        group_keys = keyring.get_data_secure_group_keys()
        protected_addresses = {
            GroupAddress(point["stateGroupAddress"]) for point in points
        } | {
            GroupAddress(point["writeGroupAddress"]) for point in points if point["writable"]
        }
        if not protected_addresses.issubset(group_keys):
            raise KnxConfigurationError("KNX_DATA_SECURE_GROUP_KEY_MISSING")
        secure = SecureConfig(
            user_id=int(config["userId"]),
            user_password=secrets["userPassword"],
            device_authentication_password=secrets["deviceAuthentication"],
            keyring=keyring,
        )
        connection = ConnectionConfig(
            connection_type=ConnectionType.TUNNELING_TCP_SECURE,
            gateway_ip=config["gatewayHost"],
            gateway_port=int(config["gatewayPort"]),
            individual_address=config["individualAddress"],
            secure_config=secure,
            auto_reconnect=True,
        )
        return XKNX(connection_config=connection), group_keys
    except KnxConfigurationError:
        raise
    except Exception:
        raise KnxConfigurationError("KNX_SECURE_CONFIGURATION_INVALID") from None


def decode_secure_value(dpt: str, payload) -> bool | float:
    transcoder = DPTBase.parse_transcoder(dpt)
    value = transcoder.from_knx(payload)
    if dpt in ("1.001", "1.002"):
        if isinstance(value, bool):
            return value
        if isinstance(getattr(value, "value", None), bool):
            return value.value
        raise KnxConfigurationError("KNX_VALUE_TYPE_INVALID")
    if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(float(value)):
        raise KnxConfigurationError("KNX_VALUE_TYPE_INVALID")
    return float(value)


async def read_secure_group_value(xknx: XKNX, point: dict, timeout_seconds: float = 4.0) -> bool | float:
    address = GroupAddress(point["stateGroupAddress"])
    try:
        telegram = await ValueReader(xknx, address, timeout_in_seconds=timeout_seconds).read()
    except Exception:
        raise KnxConfigurationError("KNX_SECURE_READ_FAILED") from None
    if telegram is None:
        raise KnxConfigurationError("KNX_SECURE_READ_TIMEOUT")
    if telegram.data_secure is not True:
        raise KnxConfigurationError("KNX_PLAIN_OR_UNVERIFIED_TELEGRAM_REJECTED")
    if not hasattr(telegram.payload, "value"):
        raise KnxConfigurationError("KNX_SECURE_READBACK_INVALID")
    return decode_secure_value(point["dpt"], telegram.payload.value)
