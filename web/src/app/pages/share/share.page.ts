import { NbspDatePipe } from '../../core/nbsp-date.pipe';
import { ChangeDetectionStrategy, Component, ElementRef, OnDestroy, inject, signal, viewChild } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { ActivatedRoute } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiService, errorText } from '../../core/api.service';
import { dayNumber, eachDay, monthGrid, monthTitle, parseIso, sameMonth, todayIso, weekdayShort, weekRange } from '../../core/dates';
import { Clip, PublicSchedule, shortTime, statusClass, statusLabel } from '../../core/models';
import { ThemeService } from '../../core/theme.service';
import { brandTone } from '../../core/brand-tone';

@Component({
  selector: 'app-share',
  imports: [NbspDatePipe],
  templateUrl: './share.page.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SharePage implements OnDestroy {
  private readonly api = inject(ApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly meta = inject(Meta);
  private readonly title = inject(Title);
  private readonly preview = viewChild<ElementRef<HTMLDialogElement>>('preview');
  private readonly theme = inject(ThemeService);

  tone(color: string | null | undefined): string {
    return brandTone(color, this.theme.effective());
  }

  brandList(clips: Clip[]): string {
    return Array.from(new Set(clips.map((clip) => clip.brandName))).join(', ');
  }

  readonly loading = signal(true);
  readonly error = signal('');
  readonly schedule = signal<PublicSchedule | null>(null);
  readonly view = signal<'month' | 'week'>('month');
  readonly cursor = signal(todayIso());
  readonly selectedDay = signal(todayIso());
  readonly active = signal<Clip | null>(null);
  readonly weekdays = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
  readonly today = todayIso();

  readonly days = signal<{ date: string; number: string; weekday: string; inMonth: boolean; clips: Clip[] }[]>([]);

  constructor() {
    this.meta.updateTag({ name: 'robots', content: 'noindex, nofollow' });
    this.route.paramMap.subscribe((params) => {
      const token = params.get('token');
      if (token) {
        void this.load(token);
      }
    });
  }

  ngOnDestroy(): void {
    this.meta.removeTag('name="robots"');
    this.title.setTitle('Ulric Clip Calendar');
  }

  label = statusLabel;
  pill = statusClass;
  time = shortTime;

  setView(view: 'month' | 'week'): void {
    this.view.set(view);
    this.rebuild();
  }

  shift(direction: number): void {
    const date = parseIso(this.cursor());
    if (this.view() === 'week') {
      date.setDate(date.getDate() + direction * 7);
    } else {
      date.setMonth(date.getMonth() + direction);
    }
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    this.cursor.set(`${date.getFullYear()}-${month}-${day}`);
    this.rebuild();
  }

  heading(): string {
    return this.view() === 'month'
      ? monthTitle(this.cursor())
      : `Week of ${parseIso(weekRange(this.cursor()).start).toLocaleDateString('en-US', { month: 'long', day: 'numeric' })}`;
  }

  open(clip: Clip): void {
    this.active.set(clip);
    this.preview()?.nativeElement.showModal();
  }

  close(): void {
    this.preview()?.nativeElement.close();
    this.active.set(null);
  }

  onBackdrop(event: Event): void {
    if (event.target === this.preview()?.nativeElement) {
      this.close();
    }
  }

  private async load(token: string): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const schedule = await firstValueFrom(this.api.publicSchedule(token));
      this.schedule.set(schedule);
      this.title.setTitle(`${schedule.label} | Ulric Clip Calendar`);
      const anchor = schedule.rangeStart || schedule.clips[0]?.postDate || todayIso();
      this.cursor.set(anchor);
      this.selectedDay.set(anchor);
      this.rebuild();
    } catch (error) {
      this.error.set(errorText(error));
    } finally {
      this.loading.set(false);
    }
  }

  private rebuild(): void {
    const schedule = this.schedule();
    if (!schedule) {
      return;
    }
    const range = this.view() === 'week' ? weekRange(this.cursor()) : monthGrid(this.cursor());
    this.days.set(
      eachDay(range.start, range.end).map((date) => ({
        date,
        number: dayNumber(date),
        weekday: weekdayShort(date),
        inMonth: this.view() === 'week' || sameMonth(date, this.cursor()),
        clips: schedule.clips
          .filter((clip) => clip.postDate === date)
          .slice()
          .sort((left, right) => left.postTime.localeCompare(right.postTime))
      }))
    );
  }
}
