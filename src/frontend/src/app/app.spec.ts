import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { AppComponent } from './app.component';

describe('AppComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
  });
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  it('loads saved folders on startup', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    TestBed.inject(HttpTestingController).expectOne('/api/Folder/list')
      .flush([{ id: 1, folderName: 'CSR' }]);
    expect(fixture.componentInstance.folders).toEqual([{ id: 1, folderName: 'CSR' }]);
  });
  it('requires both documents before analysis', () => {
    const app = TestBed.createComponent(AppComponent).componentInstance;
    expect(app.canAnalyze).toBe(false);
    app.oldFile = new File(['old'], 'old.pdf');
    expect(app.canAnalyze).toBe(false);
    app.newMode = 'existing';
    app.newSelectedDocumentId = 2;
    expect(app.canAnalyze).toBe(true);
  });
});
