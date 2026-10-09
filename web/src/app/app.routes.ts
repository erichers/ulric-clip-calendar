import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', loadComponent: () => import('./pages/calendar/calendar.page').then((m) => m.CalendarPage) },
  { path: 'clips/new', loadComponent: () => import('./pages/editor/editor.page').then((m) => m.EditorPage) },
  { path: 'clips/:id', loadComponent: () => import('./pages/editor/editor.page').then((m) => m.EditorPage) },
  { path: 'review', loadComponent: () => import('./pages/review/review.page').then((m) => m.ReviewPage) },
  { path: 'brands', loadComponent: () => import('./pages/brands/brands.page').then((m) => m.BrandsPage) },
  { path: 's/:token', loadComponent: () => import('./pages/share/share.page').then((m) => m.SharePage) },
  { path: '**', redirectTo: '' }
];
