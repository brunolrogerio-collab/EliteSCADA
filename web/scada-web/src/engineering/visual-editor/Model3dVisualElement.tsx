import React, { useEffect, useRef, useState } from 'react';
import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import {
  clampModel3dPitch,
  DEFAULT_MODEL3D_PITCH_DEG,
  DEFAULT_MODEL3D_YAW_DEG,
  frameModel3dCamera,
  normalizeModel3d,
  wrapModel3dYaw
} from './model3dViewGeometry';

/** The same canonical model viewer is used in Engineering and published Runtime. */
export function Model3dVisualElement({
  src, label, viewYawDeg = DEFAULT_MODEL3D_YAW_DEG,
  viewPitchDeg = DEFAULT_MODEL3D_PITCH_DEG, runtimeInteractionEnabled = false
}: {
  src: string; label: string; viewYawDeg?: number; viewPitchDeg?: number;
  runtimeInteractionEnabled?: boolean;
}) {
  const host = useRef<HTMLDivElement>(null);
  const redraw = useRef<(() => void) | null>(null);
  const view = useRef({ yaw: viewYawDeg, pitch: viewPitchDeg });
  const dragging = useRef<{ pointerId: number; x: number; y: number; yaw: number; pitch: number } | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    view.current = { yaw: viewYawDeg, pitch: viewPitchDeg };
    dragging.current = null;
    redraw.current?.();
  }, [viewYawDeg, viewPitchDeg, src]);

  useEffect(() => {
    const element = host.current;
    if (!element || !src) return;
    let disposed = false;
    let model: THREE.Group | null = null;
    let bounds = new THREE.Box3();
    let renderer: THREE.WebGLRenderer;
    try {
      renderer = new THREE.WebGLRenderer({ alpha: true, antialias: true });
    } catch {
      setFailed(true);
      return;
    }
    setFailed(false);
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    renderer.setClearColor(0x000000, 0);
    element.replaceChildren(renderer.domElement);

    const scene = new THREE.Scene();
    const camera = new THREE.PerspectiveCamera(38, 1, 0.01, 1000);
    scene.add(new THREE.HemisphereLight(0xffffff, 0x556070, 2));
    const key = new THREE.DirectionalLight(0xffffff, 2.5);
    key.position.set(4, 6, 5);
    scene.add(key);

    const resize = () => {
      if (disposed) return;
      const width = Math.max(1, element.clientWidth);
      const height = Math.max(1, element.clientHeight);
      renderer.setSize(width, height, false);
      frameModel3dCamera(camera, bounds, width, height, view.current.yaw, view.current.pitch);
      renderer.render(scene, camera);
    };
    redraw.current = resize;
    const observer = new ResizeObserver(resize);
    observer.observe(element);
    resize();

    new GLTFLoader().load(src, gltf => {
      if (disposed) { disposeObject(gltf.scene); return; }
      const normalized = normalizeModel3d(gltf.scene);
      model = normalized.object;
      bounds = normalized.bounds;
      scene.add(model);
      resize();
    }, undefined, () => { if (!disposed) setFailed(true); });

    return () => {
      disposed = true;
      observer.disconnect();
      redraw.current = null;
      dragging.current = null;
      if (model) disposeObject(model);
      renderer.dispose();
      renderer.domElement.remove();
    };
  }, [src]);

  return <div ref={host} className="visual-editor-object-model3d" role="img" aria-label={label}
    data-model3d-runtime-interactive={runtimeInteractionEnabled}
    style={{ touchAction: runtimeInteractionEnabled ? 'none' : undefined, cursor: runtimeInteractionEnabled ? 'grab' : undefined }}
    onPointerDown={event => {
      if (!runtimeInteractionEnabled || event.button !== 0) return;
      event.preventDefault();
      event.stopPropagation();
      dragging.current = { pointerId: event.pointerId, x: event.clientX, y: event.clientY, yaw: view.current.yaw, pitch: view.current.pitch };
      event.currentTarget.setPointerCapture(event.pointerId);
    }}
    onPointerMove={event => {
      const drag = dragging.current;
      if (!runtimeInteractionEnabled || !drag || drag.pointerId !== event.pointerId) return;
      event.stopPropagation();
      view.current = {
        yaw: wrapModel3dYaw(drag.yaw + (event.clientX - drag.x) * 0.45),
        pitch: clampModel3dPitch(drag.pitch - (event.clientY - drag.y) * 0.45)
      };
      redraw.current?.();
    }}
    onPointerUp={event => {
      if (dragging.current?.pointerId !== event.pointerId) return;
      event.stopPropagation();
      dragging.current = null;
      if (event.currentTarget.hasPointerCapture(event.pointerId)) event.currentTarget.releasePointerCapture(event.pointerId);
    }}
    onPointerCancel={() => { dragging.current = null; }}
    onClick={event => { if (runtimeInteractionEnabled) event.stopPropagation(); }}
  >
    {failed ? <span>3D preview unavailable</span> : null}
  </div>;
}

function disposeObject(object: THREE.Object3D) {
  object.traverse(child => {
    const mesh = child as THREE.Mesh;
    mesh.geometry?.dispose();
    const materials = Array.isArray(mesh.material) ? mesh.material : mesh.material ? [mesh.material] : [];
    for (const material of materials) {
      for (const value of Object.values(material)) if (value instanceof THREE.Texture) value.dispose();
      material.dispose();
    }
  });
}
