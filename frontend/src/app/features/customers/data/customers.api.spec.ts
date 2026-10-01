import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { environment } from '../../../../environments/environment';
import { CustomersApi } from './customers.api';

describe('CustomersApi', () => {
  let api: CustomersApi;
  let http: HttpTestingController;
  const baseUrl = `${environment.apiBaseUrl}/customers`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(CustomersApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('sends paging, search and sort as query params and drops empty values', () => {
    api.list({ page: 2, pageSize: 20, search: 'apex', sortBy: 'name', sortDirection: 'desc', status: '' }).subscribe();

    const req = http.expectOne((r) => r.url === baseUrl);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('page')).toBe('2');
    expect(req.request.params.get('pageSize')).toBe('20');
    expect(req.request.params.get('search')).toBe('apex');
    expect(req.request.params.get('sortBy')).toBe('name');
    expect(req.request.params.get('sortDirection')).toBe('desc');
    expect(req.request.params.has('status')).toBe(false);
    req.flush({ items: [], page: 2, pageSize: 20, totalCount: 0 });
  });

  it('targets the resource id for get and update', () => {
    api.get('abc').subscribe();
    http.expectOne({ method: 'GET', url: `${baseUrl}/abc` }).flush({});

    api.update('abc', {} as never).subscribe();
    http.expectOne({ method: 'PUT', url: `${baseUrl}/abc` }).flush({});
  });

  it('posts to the collection on create', () => {
    api.create({ code: 'CUST-1' } as never).subscribe();

    const req = http.expectOne({ method: 'POST', url: baseUrl });
    expect(req.request.body).toEqual({ code: 'CUST-1' });
    req.flush({});
  });
});
