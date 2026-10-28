import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { ApiService } from './api';

describe('ApiService', () => {
  beforeEach(() => TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting()],
  }));
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  it('submits uploaded and saved documents to the backend analysis endpoint', () => {
    const form = new FormData();
    form.append('oldPdf', new File(['old'], 'old.pdf'));
    form.append('newDocumentId', '2');
    TestBed.inject(ApiService).analyzePdfs(form).subscribe(result => expect(result).toEqual([]));
    const request = TestBed.inject(HttpTestingController).expectOne('/api/Document/analyze');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toBe(form);
    request.flush([]);
  });
});
