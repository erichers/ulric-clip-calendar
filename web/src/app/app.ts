import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { ThemeService } from './core/theme.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class App {
  protected readonly theme = inject(ThemeService);
  private readonly router = inject(Router);
  protected readonly shareMode = signal(this.router.url.startsWith('/s/'));

  constructor() {
    this.router.events.pipe(filter((event) => event instanceof NavigationEnd)).subscribe(() => {
      this.shareMode.set(this.router.url.startsWith('/s/'));
    });
  }
}
