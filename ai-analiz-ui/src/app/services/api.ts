import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { CsrAiItem } from '../../csr-item.model';

@Injectable({
  providedIn: 'root'
})
export class ApiService {
  // C# Web API adresi (Port 7001)
  private apiUrl = 'https://localhost:7001/api/analysis'; 

  constructor(private http: HttpClient) { }

  analyzePdfs(oldPdf: File, newPdf: File): Observable<CsrAiItem[]> {
    const formData = new FormData();
    formData.append('oldPdf', oldPdf);
    formData.append('newPdf', newPdf);

    // 🎯 DİKKAT: Burada çift tırnak değil, backtick ( ` ) kullanıyoruz!
    return this.http.post<CsrAiItem[]>(`${this.apiUrl}/analyze`, formData);
  }
}