/**
 * Brand colors are data, not a second accent (rubric R1). Each brand color is shown in a per-scheme tone that is
 * at least 3:1 on the card surface and at least dE76 20 away from the UI accent (#d97757 / #8f4630, dark #e08a6c / #f0c2b0).
 * The two shipped brands have hand-picked tones; any other color is nudged until it passes.
 */
type Mode = 'light' | 'dark';

const KNOWN: Record<string, Record<Mode, string>> = {
  '#3f6b4e': { light: '#3f6b4e', dark: '#5a8a6a' },
  '#8c4a32': { light: '#8a7259', dark: '#8a7259' }
};

const SURFACE: Record<Mode, [number, number, number]> = { light: [255, 252, 248], dark: [28, 27, 24] };
const ACCENTS: Record<Mode, [number, number, number][]> = {
  light: [[217, 119, 87], [143, 70, 48]],
  dark: [[224, 138, 108], [240, 194, 176]]
};

function rgb(hex: string): [number, number, number] | null {
  const m = /^#?([0-9a-f]{6})$/i.exec(hex.trim());
  if (!m) return null;
  const n = parseInt(m[1], 16);
  return [(n >> 16) & 255, (n >> 8) & 255, n & 255];
}

function hex([r, g, b]: [number, number, number]): string {
  return '#' + [r, g, b].map((v) => Math.round(Math.max(0, Math.min(255, v))).toString(16).padStart(2, '0')).join('');
}

function lum([r, g, b]: [number, number, number]): number {
  const f = (c: number) => {
    const v = c / 255;
    return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4);
  };
  return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b);
}

function ratio(a: [number, number, number], b: [number, number, number]): number {
  const x = lum(a);
  const y = lum(b);
  return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05);
}

function lab([r, g, b]: [number, number, number]): [number, number, number] {
  const f = (c: number) => {
    const v = c / 255;
    return v <= 0.04045 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4);
  };
  const R = f(r), G = f(g), B = f(b);
  const t = (v: number) => (v > 0.008856 ? Math.cbrt(v) : 7.787 * v + 16 / 116);
  const X = t((R * 0.4124 + G * 0.3576 + B * 0.1805) / 0.95047);
  const Y = t(R * 0.2126 + G * 0.7152 + B * 0.0722);
  const Z = t((R * 0.0193 + G * 0.1192 + B * 0.9505) / 1.08883);
  return [116 * Y - 16, 500 * (X - Y), 200 * (Y - Z)];
}

function dE(a: [number, number, number], b: [number, number, number]): number {
  const A = lab(a), B = lab(b);
  return Math.hypot(A[0] - B[0], A[1] - B[1], A[2] - B[2]);
}

function mix(a: [number, number, number], b: [number, number, number], t: number): [number, number, number] {
  return [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t];
}

const cache = new Map<string, string>();

export function brandTone(color: string | null | undefined, mode: Mode): string {
  const key = (color || '').toLowerCase() + '|' + mode;
  const hit = cache.get(key);
  if (hit) return hit;
  const known = KNOWN[(color || '').toLowerCase()];
  if (known) {
    cache.set(key, known[mode]);
    return known[mode];
  }
  let c = rgb(color || '') ?? [106, 100, 92];
  // Too close to the accent: pull toward a warm gray until it reads as its own color.
  for (let i = 0; i < 10 && Math.min(...ACCENTS[mode].map((a) => dE(a, c))) < 20; i++) {
    c = mix(c, [128, 124, 116], 0.2);
  }
  // Under 3:1 on the card: lighten in dark mode, darken in light mode.
  const target: [number, number, number] = mode === 'dark' ? [255, 255, 255] : [0, 0, 0];
  for (let i = 0; i < 20 && ratio(c, SURFACE[mode]) < 3; i++) {
    c = mix(c, target, 0.08);
  }
  const out = hex(c);
  cache.set(key, out);
  return out;
}

/** CSS custom properties for markup that cannot re-render on a theme change (Leaflet pin HTML). */
export function brandVars(color: string | null | undefined): string {
  return `--brand-l:${brandTone(color, 'light')};--brand-d:${brandTone(color, 'dark')}`;
}
