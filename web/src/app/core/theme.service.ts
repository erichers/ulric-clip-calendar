import { Injectable, signal } from '@angular/core';

type Choice = 'light' | 'dark' | 'system';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly key = 'ulric-theme';
  readonly choice = signal<Choice>(this.read());
  readonly effective = signal<'light' | 'dark'>(this.resolve(this.read()));

  constructor() {
    window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', () => {
      if (this.choice() === 'system') {
        this.apply();
      }
    });
    this.apply();
  }

  toggle(): void {
    const next = this.effective() === 'dark' ? 'light' : 'dark';
    this.choice.set(next);
    localStorage.setItem(this.key, next);
    this.apply();
  }

  private read(): Choice {
    const stored = localStorage.getItem(this.key);
    return stored === 'light' || stored === 'dark' ? stored : 'system';
  }

  private resolve(choice: Choice): 'light' | 'dark' {
    if (choice === 'system') {
      return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    }
    return choice;
  }

  private apply(): void {
    const mode = this.resolve(this.choice());
    this.effective.set(mode);
    document.documentElement.setAttribute('data-theme', mode);
  }
}
