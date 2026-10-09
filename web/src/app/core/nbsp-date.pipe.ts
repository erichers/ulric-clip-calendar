import { Pipe, PipeTransform } from '@angular/core';

const MONTH_DAY = /\b(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Sept|Oct|Nov|Dec)\.? (\d{1,2})\b/g;

/** Join "Oct 6" with U+00A0 so a month never ends a line without its day. */
export function nbspDate(text: string | null | undefined): string {
  return (text ?? '').replace(MONTH_DAY, (_m, mon: string, day: string) => `${mon}\u00a0${day}`);
}

@Pipe({ name: 'nbspDate' })
export class NbspDatePipe implements PipeTransform {
  transform(text: string | null | undefined): string {
    return nbspDate(text);
  }
}
