import * as THREE from 'three';

/** The legacy fixed-camera direction, retained for projects without authored 3D view values. */
export const DEFAULT_MODEL3D_YAW_DEG = 40.1;
export const DEFAULT_MODEL3D_PITCH_DEG = 25.8;

export function clampModel3dPitch(value: number): number {
  return Math.max(-85, Math.min(85, value));
}

export function wrapModel3dYaw(value: number): number {
  return ((value + 180) % 360 + 360) % 360 - 180;
}

/**
 * Apply origin correction INSIDE the scaled parent, not to the same node on
 * which the model scale is set. This keeps off-origin imported assets centered.
 */
export function normalizeModel3d(source: THREE.Object3D): { object: THREE.Group; bounds: THREE.Box3 } {
  const original = new THREE.Box3().setFromObject(source);
  const center = original.isEmpty() ? new THREE.Vector3() : original.getCenter(new THREE.Vector3());
  const size = original.isEmpty() ? new THREE.Vector3(1, 1, 1) : original.getSize(new THREE.Vector3());
  const offset = new THREE.Group();
  offset.position.copy(center).multiplyScalar(-1);
  offset.add(source);
  const object = new THREE.Group();
  object.scale.setScalar(2.2 / Math.max(size.x, size.y, size.z, 0.001));
  object.add(offset);
  const bounds = new THREE.Box3().setFromObject(object);
  return { object, bounds };
}

/**
 * Fit a bounding sphere inside both horizontal and vertical frusta. A sphere
 * guarantees no clipping for any authored yaw/pitch and any resize aspect.
 * Frustum/near/far are refreshed together to avoid progressive drift.
 */
export function frameModel3dCamera(
  camera: THREE.PerspectiveCamera,
  bounds: THREE.Box3,
  width: number,
  height: number,
  yawDeg: number,
  pitchDeg: number
): void {
  camera.aspect = Math.max(1, width) / Math.max(1, height);
  const sphere = bounds.getBoundingSphere(new THREE.Sphere());
  const center = bounds.isEmpty() ? new THREE.Vector3() : sphere.center;
  const radius = Math.max(0.01, bounds.isEmpty() ? 1 : sphere.radius);
  const halfVertical = THREE.MathUtils.degToRad(camera.fov / 2);
  const halfHorizontal = Math.atan(Math.tan(halfVertical) * camera.aspect);
  const distance = radius * 1.15 / Math.sin(Math.min(halfVertical, halfHorizontal));
  const yaw = THREE.MathUtils.degToRad(yawDeg);
  const pitch = THREE.MathUtils.degToRad(clampModel3dPitch(pitchDeg));
  const direction = new THREE.Vector3(
    Math.sin(yaw) * Math.cos(pitch), Math.sin(pitch), Math.cos(yaw) * Math.cos(pitch)
  );
  camera.position.copy(center).addScaledVector(direction, distance);
  camera.near = Math.max(0.001, distance - radius * 1.6);
  camera.far = distance + radius * 1.6;
  camera.lookAt(center);
  camera.updateMatrixWorld();
  camera.updateProjectionMatrix();
}
