import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { environment } from '../../../../environments/environment';
import { TicketsApi } from './tickets.api';

describe('TicketsApi', () => {
  let api: TicketsApi;
  let http: HttpTestingController;
  const baseUrl = `${environment.apiBaseUrl}/tickets`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(TicketsApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('posts status changes with the resolution', () => {
    api.changeStatus('t1', 'Resolved', 'Replaced seal').subscribe();

    const req = http.expectOne({ method: 'POST', url: `${baseUrl}/t1/status` });
    expect(req.request.body).toEqual({ status: 'Resolved', resolution: 'Replaced seal' });
    req.flush({});
  });

  it('posts comments with the internal flag', () => {
    api.addComment('t1', 'Batch B-0412', true).subscribe();

    const req = http.expectOne({ method: 'POST', url: `${baseUrl}/t1/comments` });
    expect(req.request.body).toEqual({ body: 'Batch B-0412', isInternal: true });
    req.flush({});
  });

  it('requests AI summaries and insights for the chosen period', () => {
    api.summarize('t1').subscribe();
    http.expectOne({ method: 'POST', url: `${baseUrl}/t1/summary` }).flush({});

    api.insights(30).subscribe();
    const req = http.expectOne((r) => r.method === 'POST' && r.url === `${baseUrl}/insights`);
    expect(req.request.params.get('days')).toBe('30');
    req.flush({ content: '', ticketsAnalysed: 0, generatedAtUtc: '' });
  });
});
