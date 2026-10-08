import { AbsoluteFill, Easing, interpolate, spring, useCurrentFrame, useVideoConfig } from 'remotion';

const paper = '#faf9f5';
const ink = '#1c1b19';
const muted = '#6a645c';
const line = '#e4ddd3';
const accent = '#d97757';
const fern = '#6d8f71';
const night = '#c46b4a';

const days = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
const clips = [
  { day: 0, title: 'New bag, same grinder', color: night },
  { day: 2, title: 'What we leave behind', color: fern },
  { day: 4, title: 'Steam before the door', color: night },
  { day: 6, title: 'Quiet surface', color: fern }
];

export const Demo = () => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const enter = spring({ frame, fps, config: { damping: 16, stiffness: 90 } });
  const scene = frame < 200 ? 0 : frame < 400 ? 1 : frame < 560 ? 2 : 3;
  const local = scene === 0 ? frame : scene === 1 ? frame - 200 : scene === 2 ? frame - 400 : frame - 560;

  return (
    <AbsoluteFill style={{ background: paper, color: ink, fontFamily: 'Georgia, serif' }}>
      <div style={{ padding: '54px 72px 0', opacity: enter }}>
        <div style={{ fontFamily: 'Helvetica, Arial, sans-serif', fontSize: 13, letterSpacing: 3, color: muted }}>ULRIC</div>
        <div style={{ fontSize: 42, marginTop: 4 }}>Clip Calendar</div>
      </div>
      {scene === 0 && <Month local={local} fps={fps} />}
      {scene === 1 && <Move local={local} fps={fps} />}
      {scene === 2 && <Review local={local} fps={fps} />}
      {scene === 3 && <Close local={local} fps={fps} />}
    </AbsoluteFill>
  );
};

const Month = ({ local, fps }: { local: number; fps: number }) => {
  return (
    <div style={{ padding: '36px 72px' }}>
      <h1 style={{ fontSize: 84, fontWeight: 500, margin: '0 0 28px' }}>October 2026</h1>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(7, 1fr)', borderTop: `1px solid ${line}`, borderLeft: `1px solid ${line}` }}>
        {days.map((name) => (
          <div key={name} style={{ padding: '8px 10px', color: muted, fontStyle: 'italic' }}>{name}</div>
        ))}
        {Array.from({ length: 28 }, (_, index) => {
          const rise = spring({ frame: local - index * 0.7, fps, config: { damping: 18, stiffness: 120 } });
          const clip = clips.find((item) => item.day === index % 7 && index > 6 && index < 20);
          return (
            <div key={index} style={{ minHeight: 92, borderRight: `1px solid ${line}`, borderBottom: `1px solid ${line}`, padding: 8, opacity: rise, transform: `translateY(${(1 - rise) * 12}px)` }}>
              <div style={{ color: muted }}>{index + 1}</div>
              {clip && (
                <div style={{ marginTop: 8, borderLeft: `2px solid ${clip.color}`, paddingLeft: 6, fontFamily: 'Helvetica, Arial, sans-serif', fontSize: 13 }}>
                  {clip.title}
                </div>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
};

const Move = ({ local, fps }: { local: number; fps: number }) => {
  const x = interpolate(local, [24, 90], [180, 620], { extrapolateLeft: 'clamp', extrapolateRight: 'clamp', easing: Easing.inOut(Easing.cubic) });
  const y = spring({ frame: local - 24, fps, config: { damping: 12, stiffness: 80 } });
  return (
    <div style={{ padding: '48px 72px' }}>
      <h1 style={{ fontSize: 64, fontWeight: 500, margin: 0 }}>Week of October 5</h1>
      <p style={{ color: muted, fontFamily: 'Helvetica, Arial, sans-serif' }}>Drag a clip onto another day.</p>
      <div style={{ position: 'relative', marginTop: 48, height: 280, borderTop: `1px solid ${line}`, borderBottom: `1px solid ${line}` }}>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(7, 1fr)', height: '100%' }}>
          {days.map((name) => (
            <div key={name} style={{ borderRight: `1px solid ${line}`, padding: 12, color: muted, fontStyle: 'italic' }}>{name}</div>
          ))}
        </div>
        <div style={{ position: 'absolute', left: x, top: 90 + (1 - y) * 16, width: 180, background: '#f7f2ea', borderLeft: `3px solid ${fern}`, padding: '12px 14px', fontFamily: 'Helvetica, Arial, sans-serif' }}>
          <strong>What we leave behind</strong>
          <div style={{ color: muted, marginTop: 4 }}>Thu 11:00</div>
        </div>
      </div>
    </div>
  );
};

const Review = ({ local, fps }: { local: number; fps: number }) => {
  const rise = spring({ frame: local, fps, config: { damping: 16, stiffness: 100 } });
  const press = interpolate(local, [70, 86, 100], [1, 0.96, 1], { extrapolateLeft: 'clamp', extrapolateRight: 'clamp' });
  return (
    <div style={{ display: 'grid', gridTemplateColumns: '320px 1fr', gap: 48, padding: '48px 72px', opacity: rise, transform: `translateY(${(1 - rise) * 12}px)` }}>
      <div style={{ background: '#141311', height: 520 }} />
      <div>
        <div style={{ letterSpacing: 2, color: muted, fontFamily: 'Helvetica, Arial, sans-serif', fontSize: 13 }}>FERN & FIELD</div>
        <h1 style={{ fontSize: 64, fontWeight: 500, margin: '8px 0 12px' }}>What we leave behind, Oct 8</h1>
        <p style={{ maxWidth: 520, fontFamily: 'Helvetica, Arial, sans-serif', color: muted }}>The path after rain. Approve it, or hold it.</p>
        <button style={{ marginTop: 28, background: accent, color: ink, border: 0, padding: '12px 18px', fontSize: 16, transform: `scale(${press})` }}>Approve</button>
      </div>
    </div>
  );
};

const Close = ({ local, fps }: { local: number; fps: number }) => {
  const rise = spring({ frame: local, fps, config: { damping: 18, stiffness: 90 } });
  return (
    <div style={{ padding: '120px 72px', opacity: rise, transform: `translateY(${(1 - rise) * 14}px)` }}>
      <div style={{ fontFamily: 'Helvetica, Arial, sans-serif', letterSpacing: 3, color: muted, fontSize: 13 }}>READ ONLY</div>
      <h1 style={{ fontSize: 92, fontWeight: 500, margin: '12px 0' }}>This month, all brands</h1>
      <p style={{ fontFamily: 'Helvetica, Arial, sans-serif', color: muted, fontSize: 22 }}>A schedule for clips. Nothing here is published.</p>
    </div>
  );
};
