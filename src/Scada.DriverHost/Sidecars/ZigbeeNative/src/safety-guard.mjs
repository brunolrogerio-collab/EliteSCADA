export function requireExistingCoordinatorStrategy(strategy) {
  if (strategy !== 'startup') {
    const error = new Error('Existing coordinator configuration is required; commissioning and restore are disabled.');
    error.code = 'EXISTING_COORDINATOR_REQUIRED';
    throw error;
  }
  return strategy;
}

export function installHerdsmanSafetyGuards(ControllerClass, ZnpAdapterManagerClass) {
  if (!ControllerClass?.prototype || !ZnpAdapterManagerClass?.prototype) {
    throw new TypeError('Pinned herdsman controller and zStack manager are required.');
  }

  const originalDetermineStrategy = ZnpAdapterManagerClass.prototype.determineStrategy;
  if (typeof originalDetermineStrategy !== 'function') {
    throw new Error('Pinned herdsman zStack strategy API is unavailable.');
  }

  ZnpAdapterManagerClass.prototype.determineStrategy = async function guardedDetermineStrategy(...args) {
    return requireExistingCoordinatorStrategy(await originalDetermineStrategy.apply(this, args));
  };

  // Herdsman normally adds its Green Power group during startup. v1 has no
  // group-management surface, so keep startup from mutating coordinator groups.
  ZnpAdapterManagerClass.prototype.addToGroup = async function noGroupManagement() {};

  // Controller.start() may automatically announce a channel update when the
  // configured channel differs. Fail closed before it can send that request.
  ControllerClass.prototype.changeChannel = async function prohibitChannelChange() {
    const error = new Error('Network channel changes are outside the Native Zigbee Runtime surface.');
    error.code = 'NETWORK_CHANGE_FORBIDDEN';
    throw error;
  };

  // A join can occur if another authority has temporarily opened the network.
  // Do not interview, configure, reject, or remove that device from this worker.
  ControllerClass.prototype.onDeviceJoined = async function ignoreJoinEvent() {};
  ControllerClass.prototype.onDeviceJoinedGreenPower = async function ignoreGreenPowerJoinEvent() {};
}
