"use strict";

const assert = require("node:assert/strict");
const {
  OPCUAClient,
  AttributeIds,
  DataType,
  TimestampsToReturn,
  ClientSubscription,
  ClientMonitoredItem,
} = require("node-opcua");

const endpoint = process.env.LAB_OPCUA_ENDPOINT || "opc.tcp://opcua-peer:4840";
const temperatureNode = "ns=2;s=Lab.Temperature";
const counterNode = "ns=2;s=Lab.Counter";

function withTimeout(promise, milliseconds, label) {
  return Promise.race([
    promise,
    new Promise((_, reject) =>
      setTimeout(() => reject(new Error(`${label} timed out after ${milliseconds} ms`)), milliseconds),
    ),
  ]);
}

async function main() {
  const client = OPCUAClient.create({
    endpointMustExist: false,
    connectionStrategy: {
      initialDelay: 100,
      maxDelay: 1000,
      maxRetry: 8,
    },
  });

  let session;
  let subscription;

  try {
    await client.connect(endpoint);
    session = await client.createSession();

    const browseResult = await session.browse("ObjectsFolder");
    const nodeIds = new Set((browseResult.references || []).map((reference) => reference.nodeId.toString()));
    assert(nodeIds.has(temperatureNode), `browse did not expose ${temperatureNode}`);
    assert(nodeIds.has(counterNode), `browse did not expose ${counterNode}`);

    const initialTemperature = await session.read({
      nodeId: temperatureNode,
      attributeId: AttributeIds.Value,
    });
    assert.equal(initialTemperature.statusCode.isGood(), true, "initial temperature status is not Good");
    assert.equal(initialTemperature.value.dataType, DataType.Double);
    assert.equal(initialTemperature.value.value, 21.5);

    const writeTemperature = await session.write({
      nodeId: temperatureNode,
      attributeId: AttributeIds.Value,
      value: {
        value: {
          dataType: DataType.Double,
          value: 33.75,
        },
      },
    });
    assert.equal(writeTemperature.isGood(), true, `temperature write failed: ${writeTemperature.toString()}`);

    const readBackTemperature = await session.read({
      nodeId: temperatureNode,
      attributeId: AttributeIds.Value,
    });
    assert.equal(readBackTemperature.value.value, 33.75, "temperature readback did not match write");

    subscription = ClientSubscription.create(session, {
      requestedPublishingInterval: 100,
      requestedLifetimeCount: 60,
      requestedMaxKeepAliveCount: 5,
      maxNotificationsPerPublish: 20,
      publishingEnabled: true,
      priority: 1,
    });

    await withTimeout(
      new Promise((resolve, reject) => {
        subscription.once("started", resolve);
        subscription.once("internal_error", reject);
      }),
      5000,
      "subscription start",
    );

    const monitoredItem = ClientMonitoredItem.create(
      subscription,
      {
        nodeId: counterNode,
        attributeId: AttributeIds.Value,
      },
      {
        samplingInterval: 50,
        discardOldest: true,
        queueSize: 10,
      },
      TimestampsToReturn.Both,
    );

    await withTimeout(
      new Promise((resolve, reject) => {
        monitoredItem.once("initialized", resolve);
        monitoredItem.once("err", reject);
      }),
      5000,
      "monitored item initialization",
    );

    const counterChanged = withTimeout(
      new Promise((resolve) => {
        monitoredItem.on("changed", (dataValue) => {
          if (dataValue.statusCode.isGood() && dataValue.value.value === 7)
            resolve(dataValue);
        });
      }),
      5000,
      "counter subscription change",
    );

    const writeCounter = await session.write({
      nodeId: counterNode,
      attributeId: AttributeIds.Value,
      value: {
        value: {
          dataType: DataType.Int32,
          value: 7,
        },
      },
    });
    assert.equal(writeCounter.isGood(), true, `counter write failed: ${writeCounter.toString()}`);

    const changedValue = await counterChanged;
    assert.equal(changedValue.value.value, 7);

    console.log(JSON.stringify({
      protocol: "opc-ua",
      peer: "open62541-v1.5.7",
      client: "node-opcua",
      endpoint,
      browse: "pass",
      read: "pass",
      writeReadback: "pass",
      subscription: "pass",
    }));
  } finally {
    if (subscription) {
      try {
        await subscription.terminate();
      } catch {
        // best-effort teardown in a test-only smoke process
      }
    }
    if (session) {
      try {
        await session.close();
      } catch {
        // best-effort teardown in a test-only smoke process
      }
    }
    try {
      await client.disconnect();
    } catch {
      // best-effort teardown in a test-only smoke process
    }
  }
}

main().catch((error) => {
  console.error(error && error.stack ? error.stack : error);
  process.exitCode = 1;
});
