"""Platform-independent fail-closed tests for the frozen KNX/IP sidecar contract."""

from __future__ import annotations

import asyncio
import base64
import io
import json
import unittest
from types import SimpleNamespace
from unittest.mock import patch

import main
import security
from xknx.dpt import DPTBase
from xknx.io import ConnectionType
from xknx.secure.keyring import InterfaceType
from xknx.telegram import GroupAddress


class KnxIpSecurityTests(unittest.TestCase):
    def setUp(self) -> None:
        self.config = {
            "gatewayHost": "192.0.2.10",
            "gatewayPort": "3671",
            "individualAddress": "1.1.10",
            "userId": "1",
        }
        self.point = {
            "tagId": "f3c9e8e1-1398-4cb7-b4f5-b10a935b93e8",
            "dpt": "1.001",
            "writeGroupAddress": "1/2/3",
            "stateGroupAddress": "1/2/4",
            "writable": True,
        }

    def test_connection_is_explicit_secure_tcp_and_requires_matching_tunnel_slot(self) -> None:
        keyring = self.fake_keyring()
        secrets = {
            "userPassword": "user-secret",
            "deviceAuthentication": "device-secret",
            "dataSecureKeyringBundle": "opaque-bundle",
        }
        with patch.object(security, "parse_keyring_bundle", return_value=keyring):
            client, group_keys = security.create_xknx_connection(self.config, secrets, [self.point])

        connection = client.knxip_interface.connection_config
        self.assertIs(connection.connection_type, ConnectionType.TUNNELING_TCP_SECURE)
        self.assertEqual(connection.gateway_ip, self.config["gatewayHost"])
        self.assertEqual(connection.secure_config.user_id, 1)
        self.assertEqual(connection.secure_config.user_password, "user-secret")
        self.assertEqual(connection.secure_config.device_authentication_password, "device-secret")
        self.assertIs(connection.secure_config.keyring, keyring)
        self.assertEqual(set(group_keys), {GroupAddress("1/2/3"), GroupAddress("1/2/4")})

    def test_rejects_credential_mismatch_and_missing_data_secure_key(self) -> None:
        keyring = self.fake_keyring()
        with patch.object(security, "parse_keyring_bundle", return_value=keyring):
            with self.assertRaisesRegex(security.KnxConfigurationError, "CREDENTIAL_MISMATCH"):
                security.create_xknx_connection(
                    self.config,
                    {"userPassword": "wrong", "deviceAuthentication": "device-secret", "dataSecureKeyringBundle": "opaque"},
                    [self.point],
                )

            keyring.get_data_secure_group_keys = lambda: {GroupAddress("1/2/4"): b"state-key"}
            with self.assertRaisesRegex(security.KnxConfigurationError, "GROUP_KEY_MISSING"):
                security.create_xknx_connection(
                    self.config,
                    {"userPassword": "user-secret", "deviceAuthentication": "device-secret", "dataSecureKeyringBundle": "opaque"},
                    [self.point],
                )

    def test_rejects_plain_automatic_and_ambiguous_configuration(self) -> None:
        bad_cases = [
            ({**self.config, "gatewayHost": "https://192.0.2.10"}, [self.point]),
            ({**self.config, "gatewayPort": " 3671"}, [self.point]),
            ({**self.config, "userId": "+1"}, [self.point]),
            (self.config, [{**self.point, "dpt": "9.999"}]),
            (self.config, [{**self.point, "tagId": "not-a-guid"}]),
            (self.config, [{**self.point, "stateGroupAddress": "1/8/0"}]),
        ]
        for config, points in bad_cases:
            with self.subTest(config=config, points=points):
                with self.assertRaises(security.KnxConfigurationError):
                    security.validate_connection_settings(config, points)

    def test_rejects_command_address_aliases_and_non_string_settings(self) -> None:
        readonly_state = {**self.point, "tagId": "6d0ba24c-84db-4bd4-b39e-1f15aec5fb87", "writable": False,
                          "writeGroupAddress": "1/2/5", "stateGroupAddress": "1/2/3"}
        with self.assertRaisesRegex(security.KnxConfigurationError, "WRITE_STATE_ADDRESS_COLLISION"):
            security.validate_connection_settings(self.config, [self.point, readonly_state])
        with self.assertRaisesRegex(security.KnxConfigurationError, "INDIVIDUAL_ADDRESS_INVALID"):
            security.validate_connection_settings({**self.config, "individualAddress": 10}, [self.point])

    def test_keyring_bundle_schema_and_size_are_checked_before_xml_processing(self) -> None:
        malformed = base64.b64encode(json.dumps({"format": "wrong"}).encode()).decode()
        with self.assertRaisesRegex(security.KnxConfigurationError, "BUNDLE_INVALID"):
            security.parse_keyring_bundle(malformed)

        oversized = base64.b64encode(b"x" * (1024 * 1024 + 1)).decode()
        with self.assertRaises(security.KnxConfigurationError):
            security.parse_keyring_bundle(oversized)

    def test_bootstrap_is_bounded_stdin_data_and_rpc_token_stays_environment_scoped(self) -> None:
        bootstrap = {
            "type": "bootstrap",
            "configuration": self.config,
            "secrets": {
                "userPassword": "stdin-user-secret",
                "deviceAuthentication": "stdin-device-secret",
                "dataSecureKeyringBundle": "stdin-keyring-bundle",
            },
            "points": [self.point],
        }
        environment = {
            "KNX_RPC_HOST": "127.0.0.1",
            "KNX_RPC_PORT": "3671",
            "KNX_RPC_TOKEN": "a" * 64,
        }
        raw = (json.dumps(bootstrap, separators=(",", ":")) + "\n").encode()
        with patch.dict("os.environ", environment, clear=True):
            public, secrets, points, rpc_host, rpc_port, token = main._read_environment(io.BytesIO(raw))

        self.assertEqual(public, self.config)
        self.assertEqual(secrets["userPassword"], "stdin-user-secret")
        self.assertEqual(points, [self.point])
        self.assertEqual((rpc_host, rpc_port), ("127.0.0.1", "3671"))
        self.assertEqual(token, "a" * 64)

    def test_bootstrap_rejects_missing_newline_and_oversized_startup_frame(self) -> None:
        for raw in (b"{}", b"x" * (main.MAX_BOOTSTRAP_FRAME_LENGTH + 1)):
            with self.subTest(length=len(raw)):
                with self.assertRaises(security.KnxConfigurationError):
                    main._read_bootstrap(io.BytesIO(raw))

    def test_only_data_secure_readback_is_publishable(self) -> None:
        class Reader:
            def __init__(self, telegram):
                self.telegram = telegram

            async def read(self):
                return self.telegram

        class FakeXknx:
            pass

        decoded = DPTBase.parse_transcoder("1.001").to_knx(True)
        plain = SimpleNamespace(data_secure=False, payload=SimpleNamespace(value=decoded))
        secure = SimpleNamespace(data_secure=True, payload=SimpleNamespace(value=decoded))

        with patch.object(security, "ValueReader", return_value=Reader(plain)):
            with self.assertRaisesRegex(security.KnxConfigurationError, "PLAIN_OR_UNVERIFIED"):
                asyncio.run(security.read_secure_group_value(FakeXknx(), self.point))
        with patch.object(security, "ValueReader", return_value=Reader(secure)):
            self.assertTrue(asyncio.run(security.read_secure_group_value(FakeXknx(), self.point)))

    def fake_keyring(self):
        interface = SimpleNamespace(
            type=InterfaceType.TUNNELING,
            individual_address="1.1.10",
            user_id=1,
            decrypted_password="user-secret",
            decrypted_authentication="device-secret",
        )
        return SimpleNamespace(
            interfaces=[interface],
            get_data_secure_group_keys=lambda: {
                GroupAddress("1/2/3"): b"write-key",
                GroupAddress("1/2/4"): b"state-key",
            },
        )


if __name__ == "__main__":
    unittest.main()
