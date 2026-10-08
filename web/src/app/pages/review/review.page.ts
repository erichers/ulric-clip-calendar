import { ChangeDetectionStrategy, Component, HostListener, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ActorService } from '../../core/actor.service';
import { ApiService, errorText } from '../../core/api.service';
import {
  ApprovalStatus,
  Clip,
  canTransition,
  platformLabel,
  shortTime,
  statusClass,
  statusLabel
} from '../../core/models';

@Component({
  selector: 'app-review',
  templateUrl: './review.page.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ReviewPage {
  private readonly api = inject(ApiService);
  protected readonly actor = inject(ActorService);

  readonly loading = signal(true);
  readonly error = signal('');
  readonly queue = signal<Clip[]>([]);
  readonly index = signal(0);
  readonly current = signal<Clip | null>(null);
  readonly includeApproved = signal(false);
  readonly commentBody = signal('');

  constructor() {
    void this.reload(null);
  }

  label = statusLabel;
  pill = statusClass;
  time = shortTime;
  platform = platformLabel;
  allowed = canTransition;

  async reload(preferId: string | null): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const queue = await firstValueFrom(this.api.review(this.includeApproved()));
      this.queue.set(queue);
      const found = preferId ? queue.findIndex((clip) => clip.id === preferId) : 0;
      this.index.set(found >= 0 ? found : 0);
      await this.openCurrent();
    } catch (error) {
      this.error.set(errorText(error));
    } finally {
      this.loading.set(false);
    }
  }

  async go(delta: number): Promise<void> {
    const next = this.index() + delta;
    if (next < 0 || next >= this.queue().length) {
      return;
    }
    this.index.set(next);
    await this.openCurrent();
  }

  async setStatus(status: ApprovalStatus): Promise<void> {
    const clip = this.current();
    if (!clip || !canTransition(clip.status, status) || clip.status === status) {
      return;
    }
    const following = this.queue()[this.index() + 1]?.id ?? this.queue()[this.index() - 1]?.id ?? null;
    try {
      await firstValueFrom(this.api.setStatus(clip.id, status, this.actor.name(), ''));
      await this.reload(following);
    } catch (error) {
      this.error.set(errorText(error));
    }
  }

  async addComment(): Promise<void> {
    const clip = this.current();
    if (!clip) {
      return;
    }
    try {
      const updated = await firstValueFrom(this.api.comment(clip.id, this.actor.name(), this.commentBody()));
      this.commentBody.set('');
      this.current.set(updated);
    } catch (error) {
      this.error.set(errorText(error));
    }
  }

  toggleApproved(event: Event): void {
    this.includeApproved.set((event.target as HTMLInputElement).checked);
    void this.reload(this.current()?.id ?? null);
  }

  onActor(event: Event): void {
    this.actor.set((event.target as HTMLInputElement).value);
  }

  onComment(event: Event): void {
    this.commentBody.set((event.target as HTMLTextAreaElement).value);
  }

  @HostListener('window:keydown', ['$event'])
  onKey(event: KeyboardEvent): void {
    const target = event.target as HTMLElement | null;
    const tag = target?.tagName;
    if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT' || target?.isContentEditable) {
      return;
    }
    if (event.metaKey || event.ctrlKey || event.altKey) {
      return;
    }
    const key = event.key.toLowerCase();
    if (key === 'j' || key === 'arrowdown') {
      event.preventDefault();
      void this.go(1);
    } else if (key === 'k' || key === 'arrowup') {
      event.preventDefault();
      void this.go(-1);
    } else if (key === 'a') {
      void this.setStatus('approved');
    } else if (key === 'h') {
      void this.setStatus('hold');
    } else if (key === 'r') {
      void this.setStatus('needsReview');
    } else if (key === 'd') {
      void this.setStatus('draft');
    }
  }

  private async openCurrent(): Promise<void> {
    const item = this.queue()[this.index()];
    if (!item) {
      this.current.set(null);
      return;
    }
    this.current.set(await firstValueFrom(this.api.clip(item.id)));
  }
}
