import { NbspDatePipe } from '../../core/nbsp-date.pipe';
import { ChangeDetectionStrategy, Component, ElementRef, computed, effect, inject, signal, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Chart, registerables } from 'chart.js';
import { firstValueFrom } from 'rxjs';
import { ApiService, errorText } from '../../core/api.service';
import { dayNumber, eachDay, monthGrid, monthTitle, parseIso, sameMonth, todayIso, weekdayIndex, weekdayShort, weekRange } from '../../core/dates';
import { CountUpDirective, watchInview } from '../../core/motion';
import { Brand, Capabilities, Clip, Stats, platformLabel, shortTime, statusClass, statusLabel } from '../../core/models';
import { ThemeService } from '../../core/theme.service';
import { brandTone } from '../../core/brand-tone';
import { WeekCard, mountWeekStage } from './week-stage';

Chart.register(...registerables);

@Component({
  selector: 'app-calendar',
  imports: [NbspDatePipe, RouterLink, CountUpDirective],
  templateUrl: './calendar.page.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CalendarPage {
  private readonly api = inject(ApiService);
  private readonly theme = inject(ThemeService);
  private readonly statusCanvas = viewChild<ElementRef<HTMLCanvasElement>>('statusChart');
  private readonly brandCanvas = viewChild<ElementRef<HTMLCanvasElement>>('brandChart');
  private readonly weekCanvas = viewChild<ElementRef<HTMLCanvasElement>>('weekCanvas');
  private readonly chartsRoot = viewChild<ElementRef<HTMLElement>>('chartsRoot');
  private readonly flowRoot = viewChild<ElementRef<HTMLElement>>('flowRoot');
  private statusChart?: Chart;
  private brandChart?: Chart;
  private dragId: string | null = null;
  readonly draggingId = signal<string | null>(null);

  readonly bones = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14];
  readonly today = todayIso();
  readonly weekdays = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
  readonly loading = signal(true);
  readonly error = signal('');
  readonly view = signal<'month' | 'week'>('month');
  readonly cursor = signal(todayIso());
  readonly brandId = signal<string | null>(null);
  readonly brands = signal<Brand[]>([]);
  readonly clips = signal<Clip[]>([]);
  readonly stats = signal<Stats | null>(null);
  readonly ffmpeg = signal<Capabilities | null>(null);
  readonly selectedDay = signal(todayIso());
  readonly overDate = signal<string | null>(null);
  readonly range = signal(monthGrid(todayIso()));
  readonly stageOn = signal(false);
  readonly chartsSeen = signal(false);

  readonly heading = computed(() =>
    this.view() === 'month'
      ? monthTitle(this.cursor())
      : `Week of ${parseIso(this.range().start).toLocaleDateString('en-US', { month: 'long', day: 'numeric' })}`
  );

  readonly selectedBrand = computed(() => this.brands().find((brand) => brand.id === this.brandId()) ?? null);

  readonly days = computed(() => {
    const cadence = new Set(
      (this.selectedBrand()?.cadenceDays ?? '')
        .split(',')
        .map((part) => part.trim())
        .filter(Boolean)
    );
    return eachDay(this.range().start, this.range().end).map((date) => ({
      date,
      number: dayNumber(date),
      weekday: weekdayShort(date),
      inMonth: this.view() === 'week' || sameMonth(date, this.cursor()),
      cadence: this.selectedBrand() ? cadence.has(String(weekdayIndex(date))) : false,
      clips: this.clips()
        .filter((clip) => clip.postDate === date)
        .slice()
        .sort((left, right) => left.postTime.localeCompare(right.postTime))
    }));
  });

  readonly agenda = computed(() => this.days().find((day) => day.date === this.selectedDay()) ?? this.days()[0]);

  readonly clipTotal = computed(() => this.clips().length);

  readonly weekCards = computed<WeekCard[]>(() => {
    const cards: WeekCard[] = [];
    this.days().forEach((day, dayIndex) => {
      day.clips.slice(0, 3).forEach((clip, stack) => {
        cards.push({
          dayIndex,
          stack,
          color: brandTone(clip.brandColor, this.theme.effective()),
          title: clip.title,
          time: shortTime(clip.postTime),
          status: statusLabel(clip.status),
          thumb: clip.thumbnailUrl
        });
      });
    });
    return cards;
  });

  readonly spark = computed(() => {
    // Running total across the range, so a steady one-a-day plan reads as a rising line, not a flat one.
    let total = 0;
    const counts = this.days().map((day) => (total += day.clips.length));
    const width = 640;
    const height = 72;
    const max = Math.max(1, ...counts);
    const step = counts.length > 1 ? width / (counts.length - 1) : 0;
    const points = counts.map((count, index) => {
      const x = index * step;
      const y = height - 8 - (count / max) * (height - 22);
      return `${index === 0 ? 'M' : 'L'}${x.toFixed(1)} ${y.toFixed(1)}`;
    });
    const line = points.join(' ');
    const area = counts.length ? `${line} L${width} ${height} L0 ${height} Z` : '';
    return { line, area };
  });

  constructor() {
    const compactQuery = window.matchMedia('(max-width: 1023px)');
    compactQuery.addEventListener('change', () => this.compact.set(compactQuery.matches));
    effect(() => {
      const stats = this.stats();
      const seen = this.chartsSeen();
      const statusEl = this.statusCanvas()?.nativeElement;
      const brandEl = this.brandCanvas()?.nativeElement;
      this.theme.effective();
      if (!seen || !stats || !statusEl || !brandEl) {
        return;
      }
      this.drawCharts(stats, statusEl, brandEl);
    });
    effect((onCleanup) => {
      const root = this.chartsRoot()?.nativeElement;
      if (!root) {
        return;
      }
      onCleanup(watchInview(root, () => this.chartsSeen.set(true)));
    });
    effect((onCleanup) => {
      const root = this.flowRoot()?.nativeElement;
      if (!root) {
        return;
      }
      onCleanup(watchInview(root, () => undefined));
    });
    effect((onCleanup) => {
      const canvas = this.weekCanvas()?.nativeElement;
      const show = this.view() === 'week' && !this.loading();
      const mode = this.theme.effective();
      const cards = this.weekCards();
      let cancelled = false;
      let dispose: () => void = () => undefined;
      onCleanup(() => {
        cancelled = true;
        dispose();
        this.stageOn.set(false);
      });
      const narrow = window.matchMedia('(max-width: 800px)').matches;
      if (!show || !canvas || narrow || window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
        return;
      }
      void mountWeekStage(canvas, cards, mode)
        .then((handle) => {
          if (cancelled) {
            handle.dispose();
            return;
          }
          dispose = handle.dispose;
          this.stageOn.set(true);
        })
        .catch(() => {
          if (!cancelled) {
            this.stageOn.set(false);
          }
        });
    });
    void this.boot();
  }

  async boot(): Promise<void> {
    try {
      const [brands, ffmpeg] = await Promise.all([
        firstValueFrom(this.api.brands()),
        firstValueFrom(this.api.capabilities())
      ]);
      this.brands.set(brands);
      this.ffmpeg.set(ffmpeg);
    } catch (error) {
      this.error.set(errorText(error));
    }
    await this.load();
  }

  async load(): Promise<void> {
    const range = this.view() === 'week' ? weekRange(this.cursor()) : monthGrid(this.cursor());
    this.range.set(range);
    this.loading.set(true);
    try {
      const [clips, stats] = await Promise.all([
        firstValueFrom(this.api.clips({ brandId: this.brandId(), from: range.start, to: range.end })),
        firstValueFrom(this.api.stats({ brandId: this.brandId(), from: range.start, to: range.end }))
      ]);
      this.clips.set(clips);
      this.stats.set(stats);
      const selected = this.selectedDay();
      if (selected < range.start || selected > range.end) {
        const today = todayIso();
        this.selectedDay.set(today >= range.start && today <= range.end ? today : range.start);
      }
    } catch (error) {
      this.error.set(errorText(error));
    } finally {
      this.loading.set(false);
    }
  }

  setView(view: 'month' | 'week'): void {
    this.view.set(view);
    void this.load();
  }

  setBrand(id: string | null): void {
    this.brandId.set(id);
    void this.load();
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
    void this.load();
  }

  goToday(): void {
    this.cursor.set(todayIso());
    this.selectedDay.set(todayIso());
    void this.load();
  }

  exportHref(kind: 'csv' | 'pdf'): string {
    const params = new URLSearchParams({ from: this.range().start, to: this.range().end });
    const brand = this.brandId();
    if (brand) {
      params.set('brandId', brand);
    }
    return `api/export/${kind}?${params.toString()}`;
  }

  onDragStart(event: DragEvent, id: string): void {
    this.dragId = id;
    this.draggingId.set(id);
    event.dataTransfer?.setData('text/plain', id);
    if (event.dataTransfer) {
      event.dataTransfer.effectAllowed = 'move';
    }
    const card = event.currentTarget;
    if (card instanceof HTMLElement) {
      card.classList.add('dragging');
    }
  }

  onDragEnd(event: DragEvent): void {
    this.draggingId.set(null);
    this.overDate.set(null);
    const card = event.currentTarget;
    if (card instanceof HTMLElement) {
      card.classList.remove('dragging');
    }
  }

  onDragOver(event: DragEvent, date: string): void {
    event.preventDefault();
    this.overDate.set(date);
  }

  async onDrop(event: DragEvent, date: string): Promise<void> {
    event.preventDefault();
    this.overDate.set(null);
    const id = event.dataTransfer?.getData('text/plain') || this.dragId;
    this.dragId = null;
    if (!id) {
      return;
    }
    const clip = this.clips().find((item) => item.id === id);
    if (!clip || clip.postDate === date) {
      return;
    }
    this.clips.update((list) => list.map((item) => (item.id === id ? { ...item, postDate: date } : item)));
    try {
      await firstValueFrom(this.api.reschedule(id, date, clip.postTime));
    } catch (error) {
      this.error.set(errorText(error));
      await this.load();
    }
  }

  async move(clip: Clip, event: Event, field: 'date' | 'time'): Promise<void> {
    const value = (event.target as HTMLInputElement).value;
    const date = field === 'date' ? value : clip.postDate;
    const time = field === 'time' ? value : shortTime(clip.postTime);
    try {
      await firstValueFrom(this.api.reschedule(clip.id, date, time));
      await this.load();
    } catch (error) {
      this.error.set(errorText(error));
    }
  }

  label = statusLabel;
  /** Month cells act as day pickers below 1024px, where the grid shows dots and the agenda shows the cards. */
  readonly compact = signal(window.matchMedia('(max-width: 1023px)').matches);

  tone(color: string | null | undefined): string {
    return brandTone(color, this.theme.effective());
  }

  brandList(clips: Clip[]): string {
    return Array.from(new Set(clips.map((clip) => clip.brandName))).join(', ');
  }

  dayLabel(day: { date: string; weekday: string; number: number | string; clips: Clip[] }): string {
    const count = day.clips.length;
    const what = count === 0 ? 'nothing scheduled' : `${count} ${count === 1 ? 'clip' : 'clips'}: ${this.brandList(day.clips)}`;
    return `${day.weekday} ${day.number}, ${what}`;
  }

  brandChartLabel(): string {
    const stats = this.stats();
    return stats ? 'Clips by brand: ' + stats.byBrand.map((row) => `${row.brand} ${row.count}`).join(', ') : 'Clips by brand';
  }

  pill = statusClass;
  time = shortTime;
  platform = platformLabel;

  private drawCharts(stats: Stats, statusEl: HTMLCanvasElement, brandEl: HTMLCanvasElement): void {
    const styles = getComputedStyle(document.documentElement);
    const ink = styles.getPropertyValue('--ink').trim();
    const line = styles.getPropertyValue('--field-line').trim() || styles.getPropertyValue('--line').trim();
    const reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    const dark = document.documentElement.dataset['theme'] === 'dark';
    // Draft, Needs review, Approved, Hold: every slice 3:1+ on the card, split by 2px card-color separators.
    const statusRamp = dark ? ['#7a746b', '#f3f0e8', '#b7b0a6', '#958e84'] : ['#8c857a', '#1c1b19', '#4f4a44', '#6e685f'];
    const raised = styles.getPropertyValue('--raised').trim() || (dark ? '#1c1b18' : '#fffcf8');
    Chart.defaults.color = ink;
    Chart.defaults.borderColor = line;
    Chart.defaults.font.family = 'Inter, sans-serif';
    Chart.defaults.font.size = 14;
    this.statusChart?.destroy();
    this.brandChart?.destroy();
    this.statusChart = new Chart(statusEl, {
      type: 'doughnut',
      data: {
        labels: stats.byStatus.map((row) => statusLabel(row.status)),
        datasets: [
          {
            data: stats.byStatus.map((row) => row.count),
            backgroundColor: statusRamp,
            borderColor: raised,
            borderWidth: 2
          }
        ]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        animation: reduce ? false : { duration: 640, easing: 'easeOutCubic' },
        plugins: { legend: { position: 'bottom' } }
      }
    });
    this.brandChart = new Chart(brandEl, {
      type: 'bar',
      data: {
        labels: stats.byBrand.map((row) => row.brand),
        datasets: [
          {
            label: 'Clips',
            data: stats.byBrand.map((row) => row.count),
            backgroundColor: stats.byBrand.map((row) => brandTone(row.color, dark ? 'dark' : 'light')),
            borderRadius: 0,
            borderSkipped: false
          }
        ]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        animation: reduce ? false : { duration: 680, easing: 'easeOutCubic' },
        plugins: { legend: { display: false } },
        scales: {
          x: { grid: { display: false } },
          y: { beginAtZero: true, ticks: { precision: 0 } }
        }
      }
    });
  }
}
