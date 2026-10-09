export function isoDate(date: Date): string {
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${date.getFullYear()}-${month}-${day}`;
}

export function parseIso(iso: string): Date {
  const [year, month, day] = iso.split('-').map(Number);
  return new Date(year, (month ?? 1) - 1, day ?? 1);
}

export function addDays(iso: string, days: number): string {
  const date = parseIso(iso);
  date.setDate(date.getDate() + days);
  return isoDate(date);
}

export function monthGrid(anchorIso: string): { start: string; end: string } {
  const anchor = parseIso(anchorIso);
  const first = new Date(anchor.getFullYear(), anchor.getMonth(), 1);
  const delta = (first.getDay() + 6) % 7;
  const start = new Date(first);
  start.setDate(first.getDate() - delta);
  const end = new Date(start);
  end.setDate(start.getDate() + 41);
  return { start: isoDate(start), end: isoDate(end) };
}

export function weekRange(anchorIso: string): { start: string; end: string } {
  const anchor = parseIso(anchorIso);
  const delta = (anchor.getDay() + 6) % 7;
  const start = new Date(anchor);
  start.setDate(anchor.getDate() - delta);
  const end = new Date(start);
  end.setDate(start.getDate() + 6);
  return { start: isoDate(start), end: isoDate(end) };
}

export function eachDay(start: string, end: string): string[] {
  const days: string[] = [];
  let cursor = start;
  while (cursor <= end) {
    days.push(cursor);
    cursor = addDays(cursor, 1);
  }
  return days;
}

export function monthTitle(iso: string): string {
  return parseIso(iso).toLocaleDateString('en-US', { month: 'long', year: 'numeric' });
}

export function weekdayShort(iso: string): string {
  return parseIso(iso).toLocaleDateString('en-US', { weekday: 'short' });
}

export function dayNumber(iso: string): string {
  return String(parseIso(iso).getDate());
}

export function sameMonth(iso: string, anchor: string): boolean {
  const left = parseIso(iso);
  const right = parseIso(anchor);
  return left.getFullYear() === right.getFullYear() && left.getMonth() === right.getMonth();
}

export function weekdayIndex(iso: string): number {
  return parseIso(iso).getDay();
}

export function todayIso(): string {
  return isoDate(new Date());
}
