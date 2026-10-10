function throwCollected(failures, message) {
  if (failures.length === 1) throw failures[0];
  if (failures.length > 1) throw new AggregateError(failures, message);
}

export async function cleanupController(controller) {
  if (!controller) return;

  const failures = [];
  const attempt = async (operation) => {
    try { await operation(); } catch (error) { failures.push(error); }
  };

  clearInterval(controller.backupTimer);
  clearInterval(controller.databaseSaveTimer);
  await attempt(() => controller.removeAllListeners?.());
  if (controller.database && typeof controller.databaseSave === 'function') {
    await attempt(() => controller.databaseSave());
  }
  await attempt(() => controller.adapter?.removeAllListeners?.());
  if (controller.adapter && typeof controller.adapter.stop === 'function') {
    await attempt(() => controller.adapter.stop());
  }

  throwCollected(failures, 'Native Zigbee coordinator cleanup failed.');
}

export function createGracefulShutdown(controller, host, waitForPendingRequests, onBeginShutdown = () => {}) {
  if (typeof waitForPendingRequests !== 'function') {
    throw new TypeError('Native Zigbee shutdown requires a pending-request drain function.');
  }

  let shutdownTask;
  return () => {
    if (shutdownTask) return shutdownTask;
    shutdownTask = Promise.resolve().then(async () => {
      const failures = [];
      const attempt = async (operation) => {
        try { await operation(); } catch (error) { failures.push(error); }
      };

      await attempt(() => onBeginShutdown());
      await attempt(() => host?.reader?.close());
      await attempt(() => waitForPendingRequests());
      await attempt(() => cleanupController(controller));
      await attempt(() => host?.socket?.destroy());
      throwCollected(failures, 'Native Zigbee sidecar shutdown failed.');
    });
    return shutdownTask;
  };
}
