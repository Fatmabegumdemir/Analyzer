import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { CsrAiItem } from '../models/csr-item.model';

@Injectable({
  providedIn: 'root'
})
export class ApiService {
  
  private apiUrl = 'https://localhost:7001/api/analysis'; 

  constructor(private http: HttpClient) { }

  analyzePdfs(oldPdf: File, newPdf: File): Observable<CsrAiItem[]> {
    const formData = new FormData();
    formData.append('oldPdf', oldPdf);
    formData.append('newPdf', newPdf);

    
    return this.http.post<CsrAiItem[]>(`${this.apiUrl}/analyze`, formData);
  }
}