import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { App, LearningSession } from './app';
const session: LearningSession = {
  id: 1,
  title: 'C# practice',
  description: 'Build a REST API',
  topic: 'Development',
  instructor: 'Demo Instructor',
  startsAtUtc: '2026-12-01T18:00:00Z',
  durationMinutes: 60,
  capacity: 12,
  registeredCount: 2,
  seatsRemaining: 10,
};
describe('Session explorer', () => {
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  it('renders API sessions and combines search with a topic filter', () => {
    const f = TestBed.createComponent(App);
    f.detectChanges();
    expect(f.nativeElement.textContent).toContain('Loading sessions');
    http
      .expectOne('/api/learning-sessions')
      .flush([session, { ...session, id: 2, title: 'Linux basics', topic: 'Infrastructure' }]);
    f.detectChanges();
    expect(f.nativeElement.querySelectorAll('article').length).toBe(2);
    const input: HTMLInputElement = f.nativeElement.querySelector('input');
    input.value = ' c# ';
    input.dispatchEvent(new Event('input'));
    f.detectChanges();
    expect(f.nativeElement.querySelectorAll('article').length).toBe(1);
    expect(f.nativeElement.textContent).toContain('10 of 12 seats remaining');
    const select: HTMLSelectElement = f.nativeElement.querySelector('select');
    select.value = 'Infrastructure';
    select.dispatchEvent(new Event('change'));
    f.detectChanges();
    expect(f.nativeElement.textContent).toContain('No sessions found');
  });
  it('recovers from HTTP failure through the rendered retry button', () => {
    const f = TestBed.createComponent(App);
    f.detectChanges();
    http
      .expectOne('/api/learning-sessions')
      .flush('Unavailable', { status: 503, statusText: 'Unavailable' });
    f.detectChanges();
    expect(f.nativeElement.querySelector('[role=alert]').textContent).toContain(
      'could not be loaded',
    );
    f.nativeElement.querySelector('[role=alert] button').click();
    f.detectChanges();
    http.expectOne('/api/learning-sessions').flush([session]);
    f.detectChanges();
    expect(f.nativeElement.querySelector('[role=alert]')).toBeNull();
    expect(f.nativeElement.querySelector('article').textContent).toContain('C# practice');
  });
  it('clears stale sessions when refreshed into an empty programme', () => {
    const f = TestBed.createComponent(App);
    http.expectOne('/api/learning-sessions').flush([session]);
    f.componentInstance.reload();
    http.expectOne('/api/learning-sessions').flush([]);
    f.detectChanges();
    expect(f.nativeElement.querySelector('article')).toBeNull();
    expect(f.nativeElement.textContent).toContain('No sessions found');
  });
});
