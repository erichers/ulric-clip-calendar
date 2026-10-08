export type ApprovalStatus = 'draft' | 'needsReview' | 'approved' | 'hold';
export type Platform = 'instagramReels' | 'tikTok' | 'youTubeShorts' | 'facebook';
export type MediaState = 'notRequested' | 'pending' | 'running' | 'succeeded' | 'failed' | 'unavailable';

export interface Brand {
  id: string;
  name: string;
  slug: string;
  color: string;
  description: string;
  instagram: string;
  tikTok: string;
  youTube: string;
  facebook: string;
  defaultHashtags: string;
  cadenceLabel: string;
  cadenceDays: string;
  defaultPostTime: string;
  latitude: number;
  longitude: number;
  locationLabel: string;
  clipCount: number;
}

export interface Comment {
  id: string;
  author: string;
  body: string;
  createdAt: string;
}

export interface HistoryEvent {
  id: string;
  fromStatus: ApprovalStatus;
  toStatus: ApprovalStatus;
  note: string | null;
  actor: string;
  createdAt: string;
}

export interface Clip {
  id: string;
  brandId: string;
  brandName: string;
  brandColor: string;
  title: string;
  caption: string;
  hashtags: string;
  platforms: Platform[];
  series: string;
  seriesPart: number | null;
  postDate: string;
  postTime: string;
  storiesOk: boolean;
  status: ApprovalStatus;
  sourceKind: 'upload' | 'link';
  sourceLink: string | null;
  originalFileName: string | null;
  thumbnailUrl: string | null;
  previewUrl: string | null;
  durationSeconds: number | null;
  width: number | null;
  height: number | null;
  videoCodec: string | null;
  trimStartSeconds: number | null;
  trimEndSeconds: number | null;
  mediaState: MediaState;
  mediaMessage: string | null;
  comments: Comment[];
  history: HistoryEvent[];
}

export interface ClipWrite {
  brandId: string;
  title: string;
  caption: string;
  hashtags: string;
  platforms: Platform[];
  series: string;
  seriesPart: number | null;
  postDate: string;
  postTime: string;
  storiesOk: boolean;
  sourceLink: string | null;
  status: ApprovalStatus;
  actor: string;
}

export interface BrandWrite {
  name: string;
  color: string;
  description: string;
  instagram: string;
  tikTok: string;
  youTube: string;
  facebook: string;
  defaultHashtags: string;
  cadenceLabel: string;
  cadenceDays: string;
  defaultPostTime: string;
  latitude: number;
  longitude: number;
  locationLabel: string;
}

export interface ShareLink {
  id: string;
  token: string;
  url: string;
  brandId: string | null;
  brandName: string | null;
  rangeStart: string | null;
  rangeEnd: string | null;
  label: string;
  createdAt: string;
}

export interface PublicSchedule {
  label: string;
  brandName: string | null;
  brandColor: string | null;
  rangeStart: string | null;
  rangeEnd: string | null;
  clips: Clip[];
}

export interface Stats {
  byStatus: { status: ApprovalStatus; count: number }[];
  byBrand: { brand: string; color: string; count: number }[];
}

export interface Capabilities {
  ffmpeg: boolean;
  message: string;
}

export const PLATFORMS: { id: Platform; label: string }[] = [
  { id: 'instagramReels', label: 'IG Reels' },
  { id: 'tikTok', label: 'TikTok' },
  { id: 'youTubeShorts', label: 'YT Shorts' },
  { id: 'facebook', label: 'FB' }
];

export const STATUS_LABEL: Record<ApprovalStatus, string> = {
  draft: 'Draft',
  needsReview: 'Needs review',
  approved: 'Approved',
  hold: 'Hold'
};

export const NEXT_STATUS: Record<ApprovalStatus, ApprovalStatus[]> = {
  draft: ['needsReview', 'hold'],
  needsReview: ['approved', 'hold', 'draft'],
  approved: ['hold', 'needsReview'],
  hold: ['draft', 'needsReview']
};

export function statusLabel(status: string): string {
  return STATUS_LABEL[status as ApprovalStatus] ?? status;
}

export function statusClass(status: string): string {
  return status === 'needsReview' ? 'status-needs' : `status-${status}`;
}

export function canTransition(from: ApprovalStatus, to: ApprovalStatus): boolean {
  return from === to || NEXT_STATUS[from].includes(to);
}

export function platformLabel(platform: Platform): string {
  return PLATFORMS.find((item) => item.id === platform)?.label ?? platform;
}

export function shortTime(value: string): string {
  return value.slice(0, 5);
}
