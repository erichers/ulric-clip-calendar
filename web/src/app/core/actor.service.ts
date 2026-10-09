import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class ActorService {
  readonly name = signal(localStorage.getItem('ulric-actor') || 'Avery Chen');

  set(name: string): void {
    this.name.set(name);
    localStorage.setItem('ulric-actor', name);
  }
}
