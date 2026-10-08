export interface WeekCard {
  dayIndex: number;
  stack: number;
  color: string;
}

export interface WeekStageHandle {
  dispose: () => void;
}

export async function mountWeekStage(
  canvas: HTMLCanvasElement,
  cards: WeekCard[],
  mode: 'light' | 'dark'
): Promise<WeekStageHandle> {
  const THREE = await import('three');
  const renderer = new THREE.WebGLRenderer({
    canvas,
    antialias: true,
    alpha: true,
    powerPreference: 'high-performance'
  });
  renderer.setClearColor(0x000000, 0);
  renderer.outputColorSpace = THREE.SRGBColorSpace;
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = THREE.PCFSoftShadowMap;
  renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));

  const scene = new THREE.Scene();
  const camera = new THREE.OrthographicCamera(-3.6, 3.6, 1, -1, 0.1, 30);
  camera.position.set(0, 0.52, 8);
  camera.lookAt(0, 0.52, 0);

  const hemi = new THREE.HemisphereLight(mode === 'dark' ? 0xc8beb2 : 0xfff6ee, mode === 'dark' ? 0x1a1816 : 0xe7dfd4, 0.85);
  scene.add(hemi);
  const key = new THREE.DirectionalLight(mode === 'dark' ? 0xf0e6da : 0xfffaf4, 1.35);
  key.position.set(3.2, 6.4, 4.2);
  key.castShadow = true;
  key.shadow.mapSize.set(1024, 1024);
  key.shadow.camera.near = 0.5;
  key.shadow.camera.far = 22;
  key.shadow.camera.left = -6;
  key.shadow.camera.right = 6;
  key.shadow.camera.top = 5;
  key.shadow.camera.bottom = -3;
  scene.add(key);
  scene.add(new THREE.AmbientLight(mode === 'dark' ? 0x3a342e : 0xfff3ea, 0.35));

  const paper = mode === 'dark' ? '#3a342e' : '#fffdf8';
  const line = mode === 'dark' ? '#3a3631' : '#e4ddd3';
  const floorColor = mode === 'dark' ? '#141311' : '#faf9f5';

  const floor = new THREE.Mesh(
    new THREE.PlaneGeometry(14, 6),
    new THREE.MeshStandardMaterial({ color: floorColor, roughness: 1, metalness: 0 })
  );
  floor.rotation.x = -Math.PI / 2;
  floor.position.y = 0;
  floor.receiveShadow = true;
  scene.add(floor);

  const baseline = new THREE.Mesh(
    new THREE.BoxGeometry(7.7, 0.015, 0.02),
    new THREE.MeshStandardMaterial({ color: line, roughness: 1, metalness: 0 })
  );
  baseline.position.set(0, 0.02, 0.15);
  baseline.receiveShadow = true;
  scene.add(baseline);

  const bodyGeo = new THREE.BoxGeometry(0.86, 1.02, 0.07);
  const edgeGeo = new THREE.BoxGeometry(0.055, 1.02, 0.082);
  const bodyMat = new THREE.MeshStandardMaterial({ color: paper, roughness: 0.9, metalness: 0 });
  const edgeMats = new Map<string, InstanceType<typeof THREE.MeshStandardMaterial>>();
  const edgeMaterial = (color: string) => {
    const cached = edgeMats.get(color);
    if (cached) {
      return cached;
    }
    const material = new THREE.MeshStandardMaterial({ color, roughness: 0.48, metalness: 0.06 });
    edgeMats.set(color, material);
    return material;
  };

  const span = 7.05;
  const bodies = cards.slice(0, 21).map((card, index) => {
    const group = new THREE.Group();
    const body = new THREE.Mesh(bodyGeo, bodyMat);
    const edge = new THREE.Mesh(edgeGeo, edgeMaterial(card.color || '#d97757'));
    body.castShadow = true;
    body.receiveShadow = true;
    edge.castShadow = true;
    edge.position.x = -0.4;
    group.add(body, edge);
    const x = -span / 2 + (span / 7) * (card.dayIndex + 0.5);
    const restY = 0.52 + card.stack * 0.14;
    group.position.set(x, restY + 1.35, -card.stack * 0.16);
    group.rotation.y = 0.7;
    scene.add(group);
    return { group, restY, y: restY + 1.35, vy: 0, rot: 0.7, vr: 0, index, hover: false };
  });

  const raycaster = new THREE.Raycaster();
  const pointer = new THREE.Vector2(4, 4);
  const onMove = (event: PointerEvent) => {
    const rect = canvas.getBoundingClientRect();
    pointer.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
    pointer.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;
  };
  const onLeave = () => pointer.set(4, 4);
  canvas.addEventListener('pointermove', onMove);
  canvas.addEventListener('pointerleave', onLeave);

  let running = true;
  let visible = true;
  let raf = 0;
  let last = performance.now();
  let elapsed = 0;

  const resize = () => {
    const width = canvas.clientWidth;
    const height = canvas.clientHeight;
    if (width < 2 || height < 2) {
      return;
    }
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    renderer.setSize(width, height, false);
    const aspect = width / height;
    const viewW = 7.35;
    const viewH = viewW / aspect;
    camera.left = -viewW / 2;
    camera.right = viewW / 2;
    camera.top = viewH / 2;
    camera.bottom = -viewH / 2;
    camera.updateProjectionMatrix();
  };
  resize();
  const resizeObserver = new ResizeObserver(resize);
  resizeObserver.observe(canvas);

  const intersection = new IntersectionObserver((entries) => {
    visible = entries.some((entry) => entry.isIntersecting);
    if (visible && running && document.visibilityState !== 'hidden') {
      last = performance.now();
      schedule();
    }
  });
  intersection.observe(canvas);

  const onVisibility = () => {
    if (document.visibilityState === 'hidden') {
      cancelAnimationFrame(raf);
      return;
    }
    last = performance.now();
    schedule();
  };
  document.addEventListener('visibilitychange', onVisibility);

  const step = (value: number, velocity: number, target: number, dt: number): [number, number] => {
    velocity += (target - value) * 72 * dt;
    velocity *= Math.exp(-11.5 * dt);
    return [value + velocity * dt, velocity];
  };

  const frame = (now: number) => {
    if (!running || !visible || document.visibilityState === 'hidden') {
      return;
    }
    raf = requestAnimationFrame(frame);
    const dt = Math.min(0.033, (now - last) / 1000);
    last = now;
    elapsed += dt;

    raycaster.setFromCamera(pointer, camera);
    const hits = new Set(raycaster.intersectObjects(bodies.map((body) => body.group), true).map((hit) => hit.object.parent));
    for (const body of bodies) {
      body.hover = hits.has(body.group);
      const bob = Math.sin(elapsed * 1.15 + body.index * 0.7) * 0.02;
      const targetY = body.restY + bob + (body.hover ? 0.16 : 0);
      const targetRot = body.hover ? -0.04 : -0.28;
      [body.y, body.vy] = step(body.y, body.vy, targetY, dt);
      [body.rot, body.vr] = step(body.rot, body.vr, targetRot, dt);
      body.group.position.y = body.y;
      body.group.rotation.y = body.rot;
    }
    renderer.render(scene, camera);
  };

  const schedule = () => {
    cancelAnimationFrame(raf);
    if (running && visible && document.visibilityState !== 'hidden') {
      raf = requestAnimationFrame(frame);
    }
  };
  schedule();

  return {
    dispose: () => {
      running = false;
      cancelAnimationFrame(raf);
      canvas.removeEventListener('pointermove', onMove);
      canvas.removeEventListener('pointerleave', onLeave);
      document.removeEventListener('visibilitychange', onVisibility);
      intersection.disconnect();
      resizeObserver.disconnect();
      bodyGeo.dispose();
      edgeGeo.dispose();
      bodyMat.dispose();
      floor.geometry.dispose();
      (floor.material as { dispose: () => void }).dispose();
      baseline.geometry.dispose();
      (baseline.material as { dispose: () => void }).dispose();
      edgeMats.forEach((material) => material.dispose());
      renderer.dispose();
    }
  };
}
