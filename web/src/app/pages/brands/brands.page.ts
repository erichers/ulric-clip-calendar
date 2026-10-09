import { ChangeDetectionStrategy, Component, ElementRef, effect, inject, signal, viewChild } from '@angular/core';
import * as L from 'leaflet';
import { firstValueFrom } from 'rxjs';
import { ApiService, errorText } from '../../core/api.service';
import { CountUpDirective } from '../../core/motion';
import { Brand, BrandWrite, ShareLink } from '../../core/models';
import { ThemeService } from '../../core/theme.service';
import { brandTone, brandVars } from '../../core/brand-tone';

@Component({
  selector: 'app-brands',
  imports: [CountUpDirective],
  templateUrl: './brands.page.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BrandsPage {
  private readonly api = inject(ApiService);
  private readonly mapHost = viewChild<ElementRef<HTMLElement>>('mapHost');
  private readonly theme = inject(ThemeService);

  tone(color: string | null | undefined): string {
    return brandTone(color, this.theme.effective());
  }

  private map?: L.Map;

  readonly loading = signal(true);
  readonly error = signal('');
  readonly notice = signal('');
  readonly brands = signal<Brand[]>([]);
  readonly shares = signal<ShareLink[]>([]);
  readonly editingId = signal<string | null>(null);
  readonly copied = signal('');
  readonly draft = signal<BrandWrite>(this.blank());
  readonly shareBrand = signal('');
  readonly shareStart = signal('');
  readonly shareEnd = signal('');
  readonly shareLabel = signal('');

  constructor() {
    effect(() => {
      const host = this.mapHost()?.nativeElement;
      const brands = this.brands();
      if (!host || brands.length === 0) {
        return;
      }
      this.drawMap(host, brands);
    });
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    try {
      const [brands, shares] = await Promise.all([
        firstValueFrom(this.api.brands()),
        firstValueFrom(this.api.shares())
      ]);
      this.brands.set(brands);
      this.shares.set(shares);
    } catch (error) {
      this.error.set(errorText(error));
    } finally {
      this.loading.set(false);
    }
  }

  edit(brand: Brand): void {
    this.editingId.set(brand.id);
    this.draft.set({
      name: brand.name,
      color: brand.color,
      description: brand.description,
      instagram: brand.instagram,
      tikTok: brand.tikTok,
      youTube: brand.youTube,
      facebook: brand.facebook,
      defaultHashtags: brand.defaultHashtags,
      cadenceLabel: brand.cadenceLabel,
      cadenceDays: brand.cadenceDays,
      defaultPostTime: brand.defaultPostTime,
      latitude: brand.latitude,
      longitude: brand.longitude,
      locationLabel: brand.locationLabel
    });
  }

  reset(): void {
    this.editingId.set(null);
    this.draft.set(this.blank());
  }

  onText(
    key: 'name' | 'color' | 'description' | 'instagram' | 'tikTok' | 'youTube' | 'facebook' | 'defaultHashtags' | 'cadenceLabel' | 'cadenceDays' | 'defaultPostTime' | 'locationLabel',
    event: Event
  ): void {
    const value = (event.target as HTMLInputElement | HTMLTextAreaElement).value;
    this.draft.update((current) => ({ ...current, [key]: value }));
  }

  onCoord(key: 'latitude' | 'longitude', event: Event): void {
    const value = Number((event.target as HTMLInputElement).value);
    this.draft.update((current) => ({ ...current, [key]: value }));
  }

  async save(): Promise<void> {
    this.error.set('');
    this.notice.set('');
    try {
      await firstValueFrom(this.api.saveBrand(this.draft(), this.editingId()));
      this.notice.set('Brand saved.');
      this.reset();
      await this.load();
    } catch (error) {
      this.error.set(errorText(error));
    }
  }

  async shareThis(brandId: string): Promise<void> {
    await this.createShare(brandId, '', '', '');
  }

  async createShare(brandId = this.shareBrand(), start = this.shareStart(), end = this.shareEnd(), label = this.shareLabel()): Promise<void> {
    this.error.set('');
    try {
      const created = await firstValueFrom(
        this.api.createShare({
          brandId: brandId || null,
          rangeStart: start || null,
          rangeEnd: end || null,
          label
        })
      );
      this.shares.update((list) => [created, ...list]);
      this.notice.set('Share link ready.');
      await this.copy(created.url);
    } catch (error) {
      this.error.set(errorText(error));
    }
  }

  async copy(url: string): Promise<void> {
    const absolute = new URL(url, document.baseURI).href;
    await navigator.clipboard.writeText(absolute);
    this.copied.set(url);
  }

  onShare(key: 'brand' | 'start' | 'end' | 'label', event: Event): void {
    const value = (event.target as HTMLInputElement | HTMLSelectElement).value;
    if (key === 'brand') this.shareBrand.set(value);
    if (key === 'start') this.shareStart.set(value);
    if (key === 'end') this.shareEnd.set(value);
    if (key === 'label') this.shareLabel.set(value);
  }

  private drawMap(element: HTMLElement, brands: Brand[]): void {
    this.map?.remove();
    const map = L.map(element, { scrollWheelZoom: false });
    // Leaflet's default prefix carries a flag SVG in two extra hues; keep the credit as plain text.
    map.attributionControl.setPrefix('<a href="https://leafletjs.com">Leaflet</a>');
    L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '&copy; OpenStreetMap'
    }).addTo(map);
    const points: L.LatLngExpression[] = [];
    const markers: [L.Marker, string][] = [];
    for (const brand of brands) {
      if (brand.latitude === 0 && brand.longitude === 0) {
        continue;
      }
      const point: L.LatLngExpression = [brand.latitude, brand.longitude];
      points.push(point);
      const label = `${brand.name} studio pin`;
      const marker = L.marker(point, {
        icon: L.divIcon({
          className: 'map-pin',
          html: `<span style="${brandVars(brand.color)}"></span>`,
          iconSize: [44, 44],
          iconAnchor: [22, 22]
        })
      }).bindTooltip(`${brand.name}. ${brand.locationLabel}`);
      // The icon element only exists once the map has a view, so name it whenever it is (re)created.
      marker.on('add', () => marker.getElement()?.setAttribute('aria-label', label));
      marker.addTo(map);
      markers.push([marker, label]);
    }
    if (points.length > 0) {
      map.fitBounds(L.latLngBounds(points), { padding: [28, 28], maxZoom: 6 });
    } else {
      map.setView([39.8, -98.5], 3);
    }
    for (const [marker, label] of markers) {
      marker.getElement()?.setAttribute('aria-label', label);
    }
    this.map = map;
    setTimeout(() => map.invalidateSize(), 80);
  }

  private blank(): BrandWrite {
    return {
      name: '',
      color: '#3f6b4e',
      description: '',
      instagram: '',
      tikTok: '',
      youTube: '',
      facebook: '',
      defaultHashtags: '',
      cadenceLabel: '',
      cadenceDays: '',
      defaultPostTime: '11:00',
      latitude: 0,
      longitude: 0,
      locationLabel: ''
    };
  }
}
