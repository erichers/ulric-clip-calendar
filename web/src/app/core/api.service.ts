import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import {
  Brand,
  BrandWrite,
  Capabilities,
  Clip,
  ClipWrite,
  PublicSchedule,
  ShareLink,
  Stats
} from './models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);

  capabilities() {
    return this.http.get<Capabilities>('/api/capabilities');
  }

  brands() {
    return this.http.get<Brand[]>('/api/brands');
  }

  saveBrand(body: BrandWrite, id?: string | null) {
    return id ? this.http.put<Brand>(`/api/brands/${id}`, body) : this.http.post<Brand>('/api/brands', body);
  }

  clips(params: { brandId?: string | null; from?: string; to?: string }) {
    return this.http.get<Clip[]>('/api/clips', { params: this.query(params) });
  }

  clip(id: string) {
    return this.http.get<Clip>(`/api/clips/${id}`);
  }

  createClip(body: ClipWrite) {
    return this.http.post<Clip>('/api/clips', body);
  }

  updateClip(id: string, body: ClipWrite) {
    return this.http.put<Clip>(`/api/clips/${id}`, body);
  }

  upload(id: string, file: File) {
    const data = new FormData();
    data.append('file', file, file.name);
    return this.http.post<Clip>(`/api/clips/${id}/file`, data);
  }

  reschedule(id: string, postDate: string, postTime: string) {
    return this.http.post<Clip>(`/api/clips/${id}/reschedule`, { postDate, postTime });
  }

  setStatus(id: string, status: string, actor: string, note: string) {
    return this.http.post<Clip>(`/api/clips/${id}/status`, { status, actor, note });
  }

  comment(id: string, author: string, body: string) {
    return this.http.post<Clip>(`/api/clips/${id}/comments`, { author, body });
  }

  trim(id: string, start: number, end: number) {
    return this.http.post<Clip>(`/api/clips/${id}/trim`, { start, end });
  }

  review(includeApproved: boolean) {
    return this.http.get<Clip[]>('/api/review', {
      params: { includeApproved: String(includeApproved) }
    });
  }

  stats(params: { brandId?: string | null; from?: string; to?: string }) {
    return this.http.get<Stats>('/api/stats', { params: this.query(params) });
  }

  shares() {
    return this.http.get<ShareLink[]>('/api/shares');
  }

  createShare(body: { brandId?: string | null; rangeStart?: string | null; rangeEnd?: string | null; label?: string }) {
    return this.http.post<ShareLink>('/api/shares', {
      brandId: body.brandId || null,
      rangeStart: body.rangeStart || null,
      rangeEnd: body.rangeEnd || null,
      label: body.label || ''
    });
  }

  publicSchedule(token: string) {
    return this.http.get<PublicSchedule>(`/api/public/${token}`);
  }

  private query(params: { brandId?: string | null; from?: string; to?: string }): HttpParams {
    let query = new HttpParams();
    if (params.brandId) {
      query = query.set('brandId', params.brandId);
    }
    if (params.from) {
      query = query.set('from', params.from);
    }
    if (params.to) {
      query = query.set('to', params.to);
    }
    return query;
  }
}

export function errorText(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    const body = error.error as { title?: string } | string | null;
    if (body && typeof body === 'object' && typeof body.title === 'string' && body.title.trim()) {
      return body.title;
    }
    if (typeof body === 'string' && body.trim()) {
      return body;
    }
    return `Request failed (${error.status}).`;
  }
  return 'Something went wrong.';
}
