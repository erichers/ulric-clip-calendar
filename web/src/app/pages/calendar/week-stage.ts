export interface WeekCard {
  dayIndex: number;
  stack: number;
  color: string;
  title: string;
  time: string;
  status: string;
  thumb: string | null;
}

export interface WeekStageHandle {
  dispose: () => void;
}

const STATUS_COLOR: Record<string, string> = {
  Draft: '#6a645c',
  'Needs review': '#8f4630',
  Approved: '#2f5a40',
  Hold: '#6d4a62'
};

const STATUS_COLOR_DARK: Record<string, string> = {
  Draft: '#b7b0a6',
  'Needs review': '#f0c2b0',
  Approved: '#9dccb0',
  Hold: '#d4b4cc'
};

export async function mountWeekStage(
  canvas: HTMLCanvasElement,
  cards: WeekCard[],
  mode: 'light' | 'dark'
): Promise<WeekStageHandle> {
  await document.fonts.ready;
  await Promise.all([
    document.fonts.load('400 13px Inter'),
    document.fonts.load('500 15px Inter'),
    document.fonts.load('500 17px Newsreader')
  ]).catch(() => undefined);

  await waitForBox(canvas);
  const THREE = await import('three');
  const dpr = Math.min(window.devicePixelRatio || 1, 2);
  const renderer = new THREE.WebGLRenderer({
    canvas,
    antialias: true,
    alpha: true,
    powerPreference: 'high-performance'
  });
  renderer.setClearColor(0x000000, 0);
  renderer.outputColorSpace = THREE.SRGBColorSpace;
  renderer.toneMapping = THREE.NoToneMapping;
  renderer.setPixelRatio(dpr);

  const scene = new THREE.Scene();
  const camera = new THREE.OrthographicCamera(-3.6, 3.6, 1, -1, 0.1, 30);
  camera.position.set(0, 0, 8);
  camera.lookAt(0, 0, 0);

  const span = 7.05;
  const cardW = 0.9;
  const cardH = 1.48;
  const textures: InstanceType<typeof THREE.CanvasTexture>[] = [];
  const materials: InstanceType<typeof THREE.MeshBasicMaterial>[] = [];
  const images: HTMLImageElement[] = [];
  let running = true;

  const paint = (target: HTMLCanvasElement, card: WeekCard, photo: HTMLImageElement | null) => {
    const ctx = target.getContext('2d');
    if (!ctx) {
      return;
    }
    const width = target.width / dpr;
    const height = target.height / dpr;
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.clearRect(0, 0, width, height);
    const paper = mode === 'dark' ? '#1c1b18' : '#fffdf8';
    const ink = mode === 'dark' ? '#f3f0e8' : '#1c1b19';
    const muted = mode === 'dark' ? '#b7b0a6' : '#6a645c';
    const line = mode === 'dark' ? '#3a3631' : '#e4ddd3';
    ctx.fillStyle = paper;
    ctx.fillRect(0, 0, width, height);
    ctx.fillStyle = card.color || '#d97757';
    ctx.fillRect(0, 0, 5, height);

    const posterH = Math.round(height * 0.56);
    ctx.fillStyle = mode === 'dark' ? '#2a2622' : '#f0e7dc';
    ctx.fillRect(5, 0, width - 5, posterH);
    if (photo && photo.naturalWidth > 0) {
      const frameW = width - 5;
      const scale = Math.max(frameW / photo.naturalWidth, posterH / photo.naturalHeight);
      const dw = photo.naturalWidth * scale;
      const dh = photo.naturalHeight * scale;
      ctx.save();
      ctx.beginPath();
      ctx.rect(5, 0, frameW, posterH);
      ctx.clip();
      ctx.imageSmoothingEnabled = true;
      ctx.imageSmoothingQuality = 'high';
      ctx.drawImage(photo, 5 + (frameW - dw) / 2, (posterH - dh) / 2, dw, dh);
      ctx.restore();
    }

    const textX = 14;
    const textW = width - 26;
    let y = posterH + 12;
    ctx.fillStyle = muted;
    ctx.font = '500 13px Inter, Helvetica, sans-serif';
    ctx.textBaseline = 'top';
    ctx.fillText(card.time, textX, y);
    y += 20;
    ctx.fillStyle = ink;
    ctx.font = '500 16px Newsreader, Georgia, serif';
    for (const lineText of wrap(ctx, card.title, textW, 2)) {
      ctx.fillText(lineText, textX, y);
      y += 20;
    }
    y += 4;
    ctx.fillStyle = (mode === 'dark' ? STATUS_COLOR_DARK : STATUS_COLOR)[card.status] ?? muted;
    ctx.font = '500 11px Inter, Helvetica, sans-serif';
    const status = card.status.toUpperCase();
    ctx.fillText(status, textX, y);

    ctx.strokeStyle = line;
    ctx.lineWidth = 1;
    ctx.strokeRect(0.5, 0.5, width - 1, height - 1);
  };

  const bodies = cards.slice(0, 14).map((card, index) => {
    const screenW = (cardW / 7.35) * canvas.clientWidth;
    const screenH = screenW * (cardH / cardW);
    const plate = document.createElement('canvas');
    plate.width = Math.max(2, Math.round(screenW * dpr));
    plate.height = Math.max(2, Math.round(screenH * dpr));
    paint(plate, card, null);
    const texture = new THREE.CanvasTexture(plate);
    texture.colorSpace = THREE.SRGBColorSpace;
    texture.generateMipmaps = false;
    texture.minFilter = THREE.LinearFilter;
    texture.magFilter = THREE.LinearFilter;
    texture.anisotropy = renderer.capabilities.getMaxAnisotropy();
    texture.needsUpdate = true;
    textures.push(texture);
    const material = new THREE.MeshBasicMaterial({ map: texture, toneMapped: false });
    materials.push(material);
    const mesh = new THREE.Mesh(new THREE.PlaneGeometry(cardW, cardH), material);
    const x = -span / 2 + (span / 7) * (card.dayIndex + 0.5);
    const restY = card.stack * 0.12;
    mesh.position.set(x, restY + 1.15, -card.stack * 0.04);
    mesh.rotation.z = 0.08;
    scene.add(mesh);

    if (card.thumb) {
      const img = new Image();
      images.push(img);
      img.onload = () => {
        if (!running) {
          return;
        }
        paint(plate, card, img);
        texture.needsUpdate = true;
      };
      img.src = new URL(card.thumb, document.baseURI).href;
    }

    return { mesh, restY, y: restY + 1.15, vy: 0, rot: 0.08, vr: 0, index, hover: false };
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

  let visible = true;
  let raf = 0;
  let last = performance.now();

  const resize = () => {
    const width = canvas.clientWidth;
    const height = canvas.clientHeight;
    if (width < 2 || height < 2) {
      return;
    }
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    renderer.setSize(width, height, false);
    const viewW = 7.35;
    const viewH = viewW * (height / width);
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
    velocity += (target - value) * 70 * dt;
    velocity *= Math.exp(-12 * dt);
    return [value + velocity * dt, velocity];
  };

  const frame = (now: number) => {
    if (!running || !visible || document.visibilityState === 'hidden') {
      return;
    }
    raf = requestAnimationFrame(frame);
    const dt = Math.min(0.033, (now - last) / 1000);
    last = now;
    raycaster.setFromCamera(pointer, camera);
    const hits = new Set(raycaster.intersectObjects(bodies.map((body) => body.mesh)).map((hit) => hit.object));
    for (const body of bodies) {
      body.hover = hits.has(body.mesh);
      const targetY = body.restY + (body.hover ? 0.14 : 0);
      const targetRot = body.hover ? 0 : 0;
      [body.y, body.vy] = step(body.y, body.vy, targetY, dt);
      [body.rot, body.vr] = step(body.rot, body.vr, targetRot, dt);
      body.mesh.position.y = body.y;
      body.mesh.rotation.z = body.rot;
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
      for (const img of images) {
        img.onload = null;
      }
      canvas.removeEventListener('pointermove', onMove);
      canvas.removeEventListener('pointerleave', onLeave);
      document.removeEventListener('visibilitychange', onVisibility);
      intersection.disconnect();
      resizeObserver.disconnect();
      bodies.forEach((body) => body.mesh.geometry.dispose());
      materials.forEach((material) => material.dispose());
      textures.forEach((texture) => texture.dispose());
      renderer.dispose();
    }
  };
}

function waitForBox(canvas: HTMLCanvasElement): Promise<void> {
  if (canvas.clientWidth > 2 && canvas.clientHeight > 2) {
    return Promise.resolve();
  }
  return new Promise((resolve) => {
    let frames = 0;
    const tick = () => {
      frames += 1;
      if ((canvas.clientWidth > 2 && canvas.clientHeight > 2) || frames > 40) {
        resolve();
        return;
      }
      requestAnimationFrame(tick);
    };
    requestAnimationFrame(tick);
  });
}

function wrap(ctx: CanvasRenderingContext2D, text: string, maxWidth: number, maxLines: number): string[] {
  const words = text.split(/\s+/).filter(Boolean);
  const lines: string[] = [];
  let current = '';
  for (const word of words) {
    const next = current ? `${current} ${word}` : word;
    if (ctx.measureText(next).width > maxWidth && current) {
      lines.push(current);
      current = word;
      if (lines.length === maxLines) {
        break;
      }
    } else {
      current = next;
    }
  }
  if (lines.length < maxLines && current) {
    lines.push(current);
  }
  if (lines.length === maxLines) {
    const last = lines[maxLines - 1];
    const used = lines.slice(0, -1).join(' ').length;
    const rest = text.slice(used).trim();
    if (rest.length > last.length) {
      let trimmed = last;
      while (trimmed.length > 1 && ctx.measureText(`${trimmed}…`).width > maxWidth) {
        trimmed = trimmed.slice(0, -1);
      }
      lines[maxLines - 1] = `${trimmed}…`;
    }
  }
  return lines;
}
