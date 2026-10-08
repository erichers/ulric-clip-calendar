import { Composition } from 'remotion';
import { Demo } from './Demo';

export const RemotionRoot = () => {
  return (
    <Composition
      id="ClipCalendar"
      component={Demo}
      durationInFrames={720}
      fps={30}
      width={1440}
      height={900}
    />
  );
};
