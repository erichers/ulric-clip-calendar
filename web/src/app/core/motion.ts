import { DestroyRef, Directive, ElementRef, effect, inject, input } from '@angular/core';

@Directive({
  selector: '[appCount]'
})
export class CountUpDirective {
  readonly appCount = input.required<number>();
  private readonly el = inject(ElementRef<HTMLElement>);

  constructor() {
    const host = this.el.nativeElement;
    const reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    let seen = false;
    let raf = 0;

    const paint = (value: number) => {
      host.textContent = String(Math.round(value));
    };

    const animateTo = (target: number) => {
      cancelAnimationFrame(raf);
      if (reduce) {
        paint(target);
        return;
      }
      const start = performance.now();
      const duration = 560;
      const tick = (now: number) => {
        const t = Math.min(1, (now - start) / duration);
        const eased = 1 - Math.pow(1 - t, 3);
        paint(target * eased);
        if (t < 1) {
          raf = requestAnimationFrame(tick);
        }
      };
      raf = requestAnimationFrame(tick);
    };

    const io = new IntersectionObserver(
      (entries) => {
        if (!entries.some((entry) => entry.isIntersecting)) {
          return;
        }
        seen = true;
        io.disconnect();
        animateTo(this.appCount());
      },
      { threshold: 0.35 }
    );
    io.observe(host);

    effect(() => {
      const target = this.appCount();
      host.style.minWidth = `${Math.max(1, String(target).length)}ch`;
      if (reduce) {
        paint(target);
        return;
      }
      if (!seen) {
        paint(0);
        return;
      }
      animateTo(target);
    });

    inject(DestroyRef).onDestroy(() => {
      cancelAnimationFrame(raf);
      io.disconnect();
    });
  }
}

export function watchInview(element: HTMLElement, onSee: () => void): () => void {
  const reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  if (reduce) {
    element.classList.add('play');
    onSee();
    return () => undefined;
  }
  const observer = new IntersectionObserver(
    (entries) => {
      if (!entries.some((entry) => entry.isIntersecting)) {
        return;
      }
      element.classList.add('play');
      onSee();
      observer.disconnect();
    },
    { threshold: 0.28 }
  );
  observer.observe(element);
  return () => observer.disconnect();
}
