# MQTT Live Broker Validation

Status: **DEV Driver 10 validation procedure**  
Branch: `driver10/mqtt`

This procedure exercises the real `MqttNetClientTransport` against an external broker. It is intentionally opt-in and must not be confused with the deterministic unit tests.

## Current automated live contract

`tests/Scada.Drivers.Tests/MqttBrokerIntegrationTests.cs` validates, for each configured protocol:

- connection through the production MQTTnet transport adapter;
- MQTT 5.0 and/or MQTT 3.1.1;
- subscribe/publish round trip for QoS 0, 1 and 2;
- exact topic and payload fidelity;
- expected delivered QoS;
- live publish delivered with `Retained == false`;
- retained publish stored by the broker and delivered to a later subscriber with `Retained == true`;
- retained test state deletion after validation;
- optional username/password authentication through `MqttResolvedCredentials`;
- optional TLS through the same platform certificate-validation path used by the runtime transport;
- bounded-queue burst recovery with 64 QoS 1 messages against an application queue capacity of 4;
- delayed consumer drain after the burst, requiring every unique burst index to be observed without assuming QoS 1 cannot redeliver duplicates.

The test uses unique `elitescada/integration/<run-id>/...` topics so concurrent or repeated runs do not reuse authoritative production topics.

## Environment variables

| Variable | Required | Default | Meaning |
| --- | --- | --- | --- |
| `ELITESCADA_MQTT_INTEGRATION_HOST` | yes to activate live validation | none | broker DNS name or IP address |
| `ELITESCADA_MQTT_INTEGRATION_PORT` | no | 1883 without TLS; 8883 with TLS | broker TCP port |
| `ELITESCADA_MQTT_INTEGRATION_TLS` | no | `false` | enable TLS using platform trust and hostname validation |
| `ELITESCADA_MQTT_INTEGRATION_PROTOCOLS` | no | `mqtt5,mqtt311` | comma-separated protocol matrix |
| `ELITESCADA_MQTT_INTEGRATION_USERNAME` | no | none | broker username |
| `ELITESCADA_MQTT_INTEGRATION_PASSWORD` | no | none | broker password; requires username |

The password is supplied through the process environment for this validation harness. The test converts it to the owned `MqttResolvedCredentials` byte buffer, which is cleared on disposal. The process environment itself remains host/test-runner responsibility and is not a production secret-storage mechanism.

## Run command

From a machine with the repository and .NET 10 SDK:

```bash
dotnet test tests/Scada.Drivers.Tests/Scada.Drivers.Tests.csproj --filter Category=BrokerIntegration
```

Example for a local plaintext Mosquitto instance:

```bash
export ELITESCADA_MQTT_INTEGRATION_HOST=127.0.0.1
export ELITESCADA_MQTT_INTEGRATION_PORT=1883
export ELITESCADA_MQTT_INTEGRATION_TLS=false
export ELITESCADA_MQTT_INTEGRATION_PROTOCOLS=mqtt5,mqtt311

dotnet test tests/Scada.Drivers.Tests/Scada.Drivers.Tests.csproj --filter Category=BrokerIntegration
```

For authenticated/TLS validation, configure the broker certificate chain and credentials first, then set the corresponding environment variables. Do not disable certificate validation to make a test pass.

## Important opt-in behavior

When `ELITESCADA_MQTT_INTEGRATION_HOST` is absent, the broker integration tests return without opening a network connection. This keeps ordinary unit-test and CI runs independent from external infrastructure.

Therefore, a green test run **without that variable configured is not evidence of broker interoperability**. Broker validation evidence must record the broker implementation/version, protocol matrix, TLS/auth configuration, test command and result.

## Required broker matrix before production integration

Run the contract against at least:

1. Eclipse Mosquitto.
2. A second independent implementation such as HiveMQ Community Edition.

For each implementation, retain evidence for:

- MQTT 5.0;
- MQTT 3.1.1;
- QoS 0/1/2;
- retained delivery;
- bounded-queue burst recovery;
- plaintext only where explicitly acceptable for a lab;
- trusted TLS hostname/chain validation;
- username/password authentication when the common host secret resolver is integrated.

## Additional destructive/resilience scenarios still required

The current live contract now covers a bounded burst larger than the application queue, but it does not simulate broker/process failure or a deliberately interrupted admission. Perform separate controlled validation for:

- persistent-session reconnect;
- broker restart;
- network interruption;
- sustained high-rate traffic over a longer interval than the 64-message burst contract;
- shutdown while the bounded inbound queue is full;
- QoS 1/2 redelivery when queue admission is interrupted before acknowledgement;
- oversized normal and retained payloads;
- operator remediation after an oversized retained payload faults the Data Source;
- freshness timeout and recovery with real traffic;
- duplicate/redelivery observations under QoS 1/2.

These scenarios must preserve the current semantic boundaries: QoS is not TAG quality, ACK means bounded-queue admission rather than canonical TAG transaction completion, and broker connectivity alone does not fabricate a `Good` TAG sample.
