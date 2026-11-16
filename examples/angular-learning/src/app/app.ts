import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { finalize, timeout } from 'rxjs';
export interface LearningSession {
  id: number;
  title: string;
  description: string;
  topic: string;
  instructor: string;
  startsAtUtc: string;
  durationMinutes: number;
  capacity: number;
  registeredCount: number;
  seatsRemaining: number;
}
@Component({
  selector: 'app-root',
  imports: [DatePipe],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {
  private readonly http = inject(HttpClient);
  readonly sessions = signal<LearningSession[]>([]);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly query = signal('');
  readonly topic = signal('');
  readonly topics = computed(() => [...new Set(this.sessions().map((s) => s.topic))].sort());
  readonly filtered = computed(() => {
    const q = this.query().trim().toLocaleLowerCase();
    return this.sessions().filter(
      (s) =>
        (!this.topic() || s.topic === this.topic()) &&
        `${s.title} ${s.description} ${s.instructor}`.toLocaleLowerCase().includes(q),
    );
  });
  constructor() {
    this.reload();
  }
  reload(): void {
    if (this.loading()) return;
    this.loading.set(true);
    this.error.set('');
    this.sessions.set([]);
    this.http
      .get<LearningSession[]>('/api/learning-sessions')
      .pipe(
        timeout(10000),
        finalize(() => this.loading.set(false)),
      )
      .subscribe({
        next: (sessions) => {
          this.sessions.set(sessions);
          if (this.topic() && !this.topics().includes(this.topic())) this.topic.set('');
        },
        error: () =>
          this.error.set(
            'Sessions could not be loaded. Check that the local API is running, then retry.',
          ),
      });
  }
}
