import { ChangeDetectionStrategy, Component, ElementRef, computed, effect, inject, signal, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Chart, registerables } from 'chart.js';
import { firstValueFrom } from 'rxjs';
import { ApiService, errorText } from '../../core/api.service';
import { dayNumber, eachDay, monthGrid, monthTitle, parseIso, sameMonth, todayIso, weekdayIndex, weekdayShort, weekRange } from '../../core/dates';
import { Brand, Capabilities, Clip, Stats, platformLabel, shortTime, statusClass, statusLabel } from '../../core/models';
import { ThemeService } from '../../core/theme.service';

Chart.register(...registerables);

@Component({
  selector: 'app-calendar',
  imports: [RouterLink],
  templateUrl: './calendar.page.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CalendarPage {
  private readonly api = inject(ApiService);
  private readonly theme = inject(ThemeService);
  private readonly statusCanvas = viewChild<ElementRef<HTMLCanvasElement>>('statusChart');
  private readonly brandCanvas = viewChild<ElementRef<HTMLCanvasElement>>('brandChart');
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

  constructor() {
    effect(() => {
      const stats = this.stats();
      const statusEl = this.statusCanvas()?.nativeElement;
      const brandEl = this.brandCanvas()?.nativeElement;
      this.theme.effective();
      if (!stats || !statusEl || !brandEl) {
        return;
      }
      this.drawCharts(stats, statusEl, brandEl);
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
  pill = statusClass;
  time = shortTime;
  platform = platformLabel;

  private drawCharts(stats: Stats, statusEl: HTMLCanvasElement, brandEl: HTMLCanvasElement): void {
    const styles = getComputedStyle(document.documentElement);
    const ink = styles.getPropertyValue('--ink').trim();
    const line = styles.getPropertyValue('--line').trim();
    const reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    Chart.defaults.color = ink;
    Chart.defaults.borderColor = line;
    Chart.defaults.font.family = 'Inter, sans-serif';
    this.statusChart?.destroy();
    this.brandChart?.destroy();
    this.statusChart = new Chart(statusEl, {
      type: 'doughnut',
      data: {
        labels: stats.byStatus.map((row) => statusLabel(row.status)),
        datasets: [
          {
            data: stats.byStatus.map((row) => row.count),
            backgroundColor: ['#b7b1a6', '#d97757', '#3f6b4e', '#8d6b86'],
            borderWidth: 0
          }
        ]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        animation: reduce ? false : { duration: 400 },
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
            backgroundColor: stats.byBrand.map((row) => row.color),
            borderRadius: 6
          }
        ]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        animation: reduce ? false : { duration: 400 },
        plugins: { legend: { display: false } },
        scales: {
          x: { grid: { display: false } },
          y: { beginAtZero: true, ticks: { precision: 0 } }
        }
      }
    });
  }
}
