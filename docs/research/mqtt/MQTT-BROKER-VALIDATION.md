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
- delayed consumer drain after the burst, requiring every unique burst index to be observed without assuming QoS 1 cannot redeliver duplicates;
- persistent-session QoS 1 redelivery after disconnect interrupts an inbound callback waiting for capacity in a queue of size 1.

`tests/Scada.Drivers.Tests/MqttBrokerShutdownRedeliveryTests.cs` extends the destructive session scenario to QoS 2 so the PUBREC/PUBREL/PUBCOMP path is not inferred from QoS 1 behavior.

The shutdown/redelivery tests use two ordered messages and an application queue capacity of 1. The first message occupies the EliteSCADA queue. The second delivery is given a bounded settling interval to reach the callback while no consumer drains the queue. `DisconnectAsync` then cancels receive writers before the MQTT client disconnect completes. Because the callback has disabled automatic acknowledgement and marks interrupted processing as failed, the pending QoS 1/2 message is expected to remain available in the persistent broker session and be delivered after reconnect with the same Client ID.

These are live interoperability tests. The 500 ms settling interval is deliberately not presented as a deterministic unit-test synchronization primitive; broker/network timing still needs to be recorded when validation evidence is collected.

The tests use unique `elitescada/integration/<run-id>/...` topics and unique Client IDs so concurrent or repeated runs do not reuse authoritative production topics or long-lived production sessions. Persistent test sessions use a short MQTT 5 expiry and are explicitly cleared at the end of a successful run; MQTT 3.1.1 cleanup reconnects with `CleanSession=true`.

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
- persistent-session QoS 1 and QoS 2 redelivery after full-queue shutdown interrupts application admission;
- plaintext only where explicitly acceptable for a lab;
- trusted TLS hostname/chain validation;
- username/password authentication when the common host secret resolver is integrated.

## Additional destructive/resilience scenarios still required

The current live contract now covers a bounded burst and controlled full-queue shutdown/redelivery for QoS 1/2, but it still does not simulate all production failures. Perform separate controlled validation for:

- broker process restart while a persistent client session exists;
- network interruption without a clean client disconnect;
- sustained high-rate traffic over a longer interval than the 64-message burst contract;
- process termination after bounded-queue admission but before canonical TAG cache processing;
- oversized normal and retained payloads;
- operator remediation after an oversized retained payload faults the Data Source;
- freshness timeout and recovery with real traffic;
- duplicate/redelivery observations and ordering across repeated QoS 1/2 reconnect cycles.

These scenarios must preserve the current semantic boundaries: QoS is not TAG quality, ACK means bounded-queue admission rather than canonical TAG transaction completion, and broker connectivity alone does not fabricate a `Good` TAG sample.
