import { ChangeDetectionStrategy, Component, OnDestroy, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ActorService } from '../../core/actor.service';
import { ApiService, errorText } from '../../core/api.service';
import { todayIso } from '../../core/dates';
import {
  ApprovalStatus,
  Brand,
  Capabilities,
  Clip,
  ClipWrite,
  NEXT_STATUS,
  PLATFORMS,
  Platform,
  statusClass,
  statusLabel
} from '../../core/models';

interface Draft {
  brandId: string;
  title: string;
  caption: string;
  hashtags: string;
  platforms: Platform[];
  series: string;
  seriesPart: string;
  postDate: string;
  postTime: string;
  storiesOk: boolean;
  sourceLink: string;
  status: ApprovalStatus;
}

@Component({
  selector: 'app-editor',
  imports: [RouterLink],
  templateUrl: './editor.page.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EditorPage implements OnDestroy {
  private readonly api = inject(ApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly actor = inject(ActorService);
  private timer?: ReturnType<typeof setInterval>;

  readonly platforms = PLATFORMS;
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly clip = signal<Clip | null>(null);
  readonly brands = signal<Brand[]>([]);
  readonly ffmpeg = signal<Capabilities | null>(null);
  readonly file = signal<File | null>(null);
  readonly commentBody = signal('');
  readonly trimStart = signal(0);
  readonly trimEnd = signal(1);
  readonly draft = signal<Draft>(this.blank());

  constructor() {
    void this.loadBrands();
    this.route.paramMap.subscribe((params) => {
      const id = params.get('id');
      if (id) {
        void this.load(id);
      } else {
        this.clip.set(null);
        this.draft.set(this.blank());
        this.applyBrandDefaults();
      }
    });
  }

  ngOnDestroy(): void {
    if (this.timer) {
      clearInterval(this.timer);
    }
  }

  label = statusLabel;
  pill = statusClass;

  options(): ApprovalStatus[] {
    const current = this.draft().status;
    return [current, ...NEXT_STATUS[current]];
  }

  onText(
    key: 'title' | 'caption' | 'hashtags' | 'series' | 'seriesPart' | 'postDate' | 'postTime' | 'sourceLink',
    event: Event
  ): void {
    const value = (event.target as HTMLInputElement | HTMLTextAreaElement).value;
    this.draft.update((current) => ({ ...current, [key]: value }));
  }

  onBrand(event: Event): void {
    const brandId = (event.target as HTMLSelectElement).value;
    const brand = this.brands().find((item) => item.id === brandId);
    this.draft.update((current) => ({
      ...current,
      brandId,
      hashtags: current.hashtags || brand?.defaultHashtags || '',
      postTime: current.postTime || brand?.defaultPostTime || '11:00'
    }));
  }

  onStatus(event: Event): void {
    this.draft.update((current) => ({ ...current, status: (event.target as HTMLSelectElement).value as ApprovalStatus }));
  }

  onStories(event: Event): void {
    this.draft.update((current) => ({ ...current, storiesOk: (event.target as HTMLInputElement).checked }));
  }

  onPlatform(platform: Platform, event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    this.draft.update((current) => ({
      ...current,
      platforms: checked
        ? current.platforms.includes(platform)
          ? current.platforms
          : [...current.platforms, platform]
        : current.platforms.filter((item) => item !== platform)
    }));
  }

  onFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.file.set(input.files && input.files.length > 0 ? input.files[0] : null);
  }

  onActor(event: Event): void {
    this.actor.set((event.target as HTMLInputElement).value);
  }

  onNumber(which: 'start' | 'end', event: Event): void {
    const value = Number((event.target as HTMLInputElement).value);
    if (which === 'start') {
      this.trimStart.set(value);
    } else {
      this.trimEnd.set(value);
    }
  }

  onComment(event: Event): void {
    this.commentBody.set((event.target as HTMLTextAreaElement).value);
  }

  async save(): Promise<void> {
    this.saving.set(true);
    this.error.set('');
    this.notice.set('');
    try {
      const body = this.toWrite();
      let clip = this.clip();
      if (!clip) {
        clip = await firstValueFrom(this.api.createClip(body));
        const file = this.file();
        if (file) {
          clip = await firstValueFrom(this.api.upload(clip.id, file));
        }
        await this.router.navigate(['/clips', clip.id]);
      } else {
        clip = await firstValueFrom(this.api.updateClip(clip.id, body));
        const file = this.file();
        if (file) {
          clip = await firstValueFrom(this.api.upload(clip.id, file));
        }
      }
      this.file.set(null);
      this.applyClip(clip);
      this.notice.set('Saved.');
      this.watch(clip);
    } catch (error) {
      this.error.set(errorText(error));
    } finally {
      this.saving.set(false);
    }
  }

  async queueTrim(): Promise<void> {
    const clip = this.clip();
    if (!clip) {
      return;
    }
    this.error.set('');
    try {
      const updated = await firstValueFrom(this.api.trim(clip.id, this.trimStart(), this.trimEnd()));
      this.applyClip(updated);
      this.notice.set('Trim queued. The original file stays put.');
      this.watch(updated);
    } catch (error) {
      this.error.set(errorText(error));
    }
  }

  async addComment(): Promise<void> {
    const clip = this.clip();
    if (!clip) {
      return;
    }
    try {
      const updated = await firstValueFrom(this.api.comment(clip.id, this.actor.name(), this.commentBody()));
      this.commentBody.set('');
      this.applyClip(updated);
    } catch (error) {
      this.error.set(errorText(error));
    }
  }

  private async loadBrands(): Promise<void> {
    try {
      const [brands, ffmpeg] = await Promise.all([
        firstValueFrom(this.api.brands()),
        firstValueFrom(this.api.capabilities())
      ]);
      this.brands.set(brands);
      this.ffmpeg.set(ffmpeg);
      if (!this.clip()) {
        this.applyBrandDefaults();
      }
    } catch (error) {
      this.error.set(errorText(error));
    }
  }

  private async load(id: string): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      this.applyClip(await firstValueFrom(this.api.clip(id)));
      this.watch(this.clip()!);
    } catch (error) {
      this.error.set(errorText(error));
    } finally {
      this.loading.set(false);
    }
  }

  private applyClip(clip: Clip): void {
    this.clip.set(clip);
    this.draft.set({
      brandId: clip.brandId,
      title: clip.title,
      caption: clip.caption,
      hashtags: clip.hashtags,
      platforms: [...clip.platforms],
      series: clip.series,
      seriesPart: clip.seriesPart == null ? '' : String(clip.seriesPart),
      postDate: clip.postDate,
      postTime: clip.postTime.slice(0, 5),
      storiesOk: clip.storiesOk,
      sourceLink: clip.sourceLink ?? '',
      status: clip.status
    });
    if (clip.trimStartSeconds != null) {
      this.trimStart.set(clip.trimStartSeconds);
    }
    if (clip.trimEndSeconds != null) {
      this.trimEnd.set(clip.trimEndSeconds);
    }
  }

  private watch(clip: Clip): void {
    if (this.timer) {
      clearInterval(this.timer);
    }
    if (clip.mediaState === 'pending' || clip.mediaState === 'running') {
      this.timer = setInterval(() => {
        void this.refresh(clip.id);
      }, 2000);
    }
  }

  private async refresh(id: string): Promise<void> {
    try {
      const clip = await firstValueFrom(this.api.clip(id));
      if (this.clip()?.id !== id) {
        return;
      }
      this.clip.set(clip);
      if (clip.mediaState !== 'pending' && clip.mediaState !== 'running' && this.timer) {
        clearInterval(this.timer);
      }
    } catch {
      if (this.timer) {
        clearInterval(this.timer);
      }
    }
  }

  private applyBrandDefaults(): void {
    const brand = this.brands()[0];
    if (!brand || this.draft().brandId) {
      return;
    }
    this.draft.update((current) => ({
      ...current,
      brandId: brand.id,
      hashtags: brand.defaultHashtags,
      postTime: brand.defaultPostTime
    }));
  }

  private blank(): Draft {
    return {
      brandId: '',
      title: '',
      caption: '',
      hashtags: '',
      platforms: ['instagramReels'],
      series: '',
      seriesPart: '',
      postDate: todayIso(),
      postTime: '11:00',
      storiesOk: false,
      sourceLink: '',
      status: 'draft'
    };
  }

  private toWrite(): ClipWrite {
    const draft = this.draft();
    const part = draft.seriesPart.trim();
    return {
      brandId: draft.brandId,
      title: draft.title.trim(),
      caption: draft.caption.trim(),
      hashtags: draft.hashtags.trim(),
      platforms: draft.platforms,
      series: draft.series.trim(),
      seriesPart: part ? Number(part) : null,
      postDate: draft.postDate,
      postTime: draft.postTime,
      storiesOk: draft.storiesOk,
      sourceLink: draft.sourceLink.trim() || null,
      status: draft.status,
      actor: this.actor.name()
    };
  }
}
