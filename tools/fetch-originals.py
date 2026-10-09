#!/usr/bin/env python3
"""Fetch full-resolution sources for the week thumbnails into seed-media/.originals/ (gitignored).

Sources come from CREDITS.md: Mixkit clips (the 1080p rendition; one frame, the one that best matches
the committed poster's picture area), Pexels and Unsplash photos (1600 px wide). make-thumbs.py cuts
the 4:5 thumbnails from these, so no thumbnail is upscaled.
usage: python3 tools/fetch-originals.py
"""
import os, re, subprocess, sys, tempfile, urllib.error, urllib.request
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.join(HERE, '..', 'seed-media')
OUT = os.path.join(ROOT, '.originals')
os.makedirs(OUT, exist_ok=True)
credits = open(os.path.join(HERE, '..', 'CREDITS.md'), encoding='utf8').read()
UA = {'User-Agent': 'Mozilla/5.0 (thumb fetch)'}

def get(url, dest):
    req = urllib.request.Request(url, headers=UA)
    with urllib.request.urlopen(req, timeout=60) as r, open(dest, 'wb') as f:
        f.write(r.read())

def picture(path):
    """Picture band of a 720x1280 seed poster, as a grey array (the 16:9 frame sits mid-poster)."""
    a = np.asarray(Image.open(path).convert('L'), dtype=float)
    return a[437:843]

jobs = []
for m in re.finditer(r'`([a-z-]+)\.mp4` \|[^|]*\| https://mixkit\.co/free-stock-video/[a-z0-9-]*?-(\d+)/', credits):
    jobs.append(('clip', m.group(1), m.group(2)))
for m in re.finditer(r'`([a-z-]+)\.jpg`, `[a-z-]+\.mp4` \| https://www\.pexels\.com/photo/(\d+)/', credits):
    jobs.append(('photo', m.group(1), f'https://images.pexels.com/photos/{m.group(2)}/pexels-photo-{m.group(2)}.jpeg?w=1600'))
for m in re.finditer(r'`([a-z-]+)\.jpg`, `[a-z-]+\.mp4` \| (https://images\.unsplash\.com/photo-[0-9a-f-]+)', credits):
    jobs.append(('photo', m.group(1), m.group(2) + '?w=1600&q=90&fm=jpg'))
print(len(jobs), 'sources'); fails = 0
for kind, name, ref in jobs:
    dest = os.path.join(OUT, name + '.jpg')
    if os.path.exists(dest):
        continue
    try:
        if kind == 'photo':
            get(ref, dest)
        else:
            with tempfile.TemporaryDirectory() as td:
                mp4 = os.path.join(td, 'src.mp4')
                try:
                    get(f'https://assets.mixkit.co/videos/{ref}/{ref}-1080.mp4', mp4)
                except urllib.error.HTTPError:  # some clips only have the 720p rendition (1280x720)
                    get(f'https://assets.mixkit.co/videos/{ref}/{ref}-720.mp4', mp4)
                dur = float(subprocess.check_output(['ffprobe', '-v', 'error', '-show_entries', 'format=duration', '-of', 'csv=p=0', mp4]).strip())
                want = picture(os.path.join(ROOT, name + '.jpg'))
                best = None
                for t in sorted({0.0, 0.5, 1.0, 1.5, 2.0, 3.0, 4.0, dur / 2}):
                    if t >= dur - 0.05:
                        continue
                    fr = os.path.join(td, f'f{t:.2f}.png')
                    subprocess.run(['ffmpeg', '-v', 'error', '-y', '-ss', f'{t}', '-i', mp4, '-frames:v', '1', fr], check=True)
                    im = Image.open(fr).convert('RGB')
                    g = np.asarray(im.convert('L').resize((720, 406)), dtype=float)
                    err = float(np.abs(g - want[:406]).mean())
                    if best is None or err < best[0]:
                        best = (err, t, im)
                best[2].save(dest, quality=94)
                print(name, f'frame {best[1]:.2f}s err {best[0]:.1f}')
        print(name, Image.open(dest).size)
    except Exception as e:  # report and keep going; make-thumbs.py refuses to run on a missing source
        fails += 1
        print('FAIL', name, e, file=sys.stderr)
sys.exit(1 if fails else 0)
