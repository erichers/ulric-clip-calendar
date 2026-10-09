#!/usr/bin/env python3
"""Cut the 4:5 week thumbnails (680x850) into seed-media/thumbs/.

Default: cut from the full-resolution sources in seed-media/.originals/ (fetch them with
tools/fetch-originals.py). The 4:5 box is centred and must be at least 680 px wide in the source,
so a thumbnail is never upscaled (they show at up to 340 CSS px, 680 device px at 2x).

--from-posters: the old path, cutting from the picture area between the blurred letterbox bands of
each 720x1280 poster (only 720 px wide, so the 680 rule fails for most). Band detection that comes
back weak is an error unless the poster is listed in FIXED; it never falls back silently.

Every cut then goes through a seam check: no row whose median row-to-row contrast is over 1.15:1 and
more than 10% above every other row within +-24 rows. Rows listed in EDGES were looked at and are
edges in the photo (a horizon, a counter), not seams. Any failure exits non-zero.
usage: python3 tools/make-thumbs.py [--from-posters] [--check-only]
"""
import glob, hashlib, os, sys
import numpy as np
from PIL import Image

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'seed-media')
SRC = os.path.join(ROOT, '.originals')
OUT = os.path.join(ROOT, 'thumbs')
W, H, MIN_W = 680, 850, 680
FIXED = {'fern-tea.jpg': (439, 838), 'night-still-cafe.jpg': (0, 1280), 'fern-still-forest.jpg': (0, 1280)}
EDGES = {}  # name -> rows (in the 680x850 output) reviewed as photo edges
# Mixkit only serves these at 1280x720, so the widest 4:5 box is 576x720. They are written at that
# native size (never upscaled), so the browser only scales them at 2x on the widest cards.
LOWRES = {'fern-forest-floor.jpg', 'fern-tea.jpg', 'fern-fungus.jpg'}

def lum(a):
    c = a / 255.0
    c = np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
    return 0.2126 * c[..., 0] + 0.7152 * c[..., 1] + 0.0722 * c[..., 2]

def seams(im):
    """Rows (between r and r+1) that look like a seam: median contrast > 1.15 and > 1.1x every neighbour within 24."""
    L = lum(np.asarray(im.convert('RGB'), dtype=float)) + 0.05
    ratio = np.maximum(L[1:], L[:-1]) / np.minimum(L[1:], L[:-1])
    med = np.median(ratio, axis=1)
    bad = []
    for r, v in enumerate(med):
        if v <= 1.15:
            continue
        nb = np.concatenate([med[max(0, r - 24):r], med[r + 1:r + 25]])
        if nb.size and v > 1.1 * nb.max():
            bad.append((r, round(float(v), 3)))
    return bad

def box_from_poster(name, im):
    a = np.asarray(im.convert('L'), dtype=float)
    d = np.abs(np.diff(a, axis=0)).mean(axis=1)
    base = max(np.median(d), 0.3)
    if name in FIXED:
        return FIXED[name]
    t = 150 + int(np.argmax(d[150:640])); b = 640 + int(np.argmax(d[640:1130]))
    if not (d[t] > 8 * base and d[t] > 4 and d[b] > 8 * base and d[b] > 4):
        raise SystemExit(f'FAIL {name}: weak letterbox edges (top {d[t]:.1f}, bottom {d[b]:.1f}, base {base:.2f}); add it to FIXED after checking it by eye')
    return t + 4, b - 3

def cut(im, top=0, bot=None, native_ok=False):
    w0, h0 = im.size
    bot = h0 if bot is None else bot
    h = bot - top
    w = min(w0, int(h * 0.8)); h2 = int(w * 1.25)
    x0 = (w0 - w) // 2; y0 = top + (h - h2) // 2
    box = im.crop((x0, y0, x0 + w, y0 + h2))
    if w < MIN_W:
        if not native_ok:
            raise ValueError(f'4:5 box is {w}px wide, below {MIN_W}')
        return box, w
    return box.resize((W, H), Image.LANCZOS), w

def main():
    args = set(sys.argv[1:])
    os.makedirs(OUT, exist_ok=True)
    posters = sorted(glob.glob(os.path.join(ROOT, '*.jpg')))
    if not posters:
        raise SystemExit('FAIL no posters in seed-media/')
    failures = []
    prev = {}
    for path in posters:
        name = os.path.basename(path)
        dest = os.path.join(OUT, name)
        try:
            if '--check-only' not in args:
                if os.path.exists(dest):
                    prev[hashlib.md5(open(dest, 'rb').read()).hexdigest()] = name
                if '--from-posters' in args:
                    im = Image.open(path).convert('RGB')
                    top, bot = box_from_poster(name, im)
                    out, w = cut(im, top, bot)
                else:
                    src = os.path.join(SRC, name)
                    if not os.path.exists(src):
                        raise ValueError(f'no source {os.path.relpath(src, ROOT)} (run tools/fetch-originals.py)')
                    out, w = cut(Image.open(src).convert('RGB'), native_ok=name in LOWRES)
                out.save(dest, quality=86, optimize=True, progressive=True)
            else:
                out, w = Image.open(dest), None
            bad = [r for r in seams(out) if r[0] not in EDGES.get(name, [])]
            if bad:
                raise ValueError(f'seam rows {bad[:4]}')
            print(name, 'ok', f'src {w}px' if w else '')
        except ValueError as e:
            failures.append(f'{name}: {e}')
    if prev:
        # md5 of the thumbnails being replaced, so tools/swap-thumbs.sh can find stored copies of the old cut.
        with open(os.path.join(OUT, 'PREV-MD5'), 'a') as f:
            for h, n in sorted(prev.items(), key=lambda kv: kv[1]):
                f.write(f'{h} {n}\n')
    if failures:
        print('\n'.join('FAIL ' + f for f in failures), file=sys.stderr)
        raise SystemExit(1)
    print(len(posters), 'thumbnails ok')

main()
