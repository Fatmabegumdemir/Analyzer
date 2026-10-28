import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { CsrAiItem } from '../models/csr-item.model';

export interface FolderDto { id: number; folderName: string; }
export interface DocumentDto {
  id: number;
  originalFileName: string;
  customFileName: string;
  uploadedAt: string;
}

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly apiUrl = '/api';
  constructor(private http: HttpClient) {}
  getFolders(): Observable<FolderDto[]> {
    return this.http.get<FolderDto[]>(`${this.apiUrl}/Folder/list`);
  }
  getDocuments(folderId: number): Observable<DocumentDto[]> {
    return this.http.get<DocumentDto[]>(`${this.apiUrl}/Folder/${folderId}/documents`);
  }
  analyzePdfs(formData: FormData): Observable<CsrAiItem[]> {
    return this.http.post<CsrAiItem[]>(`${this.apiUrl}/Document/analyze`, formData);
  }
}
