import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient, HttpClientModule } from '@angular/common/http';

interface FolderDto {
  id: number;
  folderName: string;
}

interface DocumentDto{
  id:number;
  originalFileName: string;
  customFileName: string;
  uploadedAt: string;
}

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule, HttpClientModule], // 👈 Hataları çözen kritik modül içe aktarımları!
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css']
})

export class AppComponent implements OnInit {
  folders: FolderDto[] = [];

  oldMode: 'upload' | 'existing' ='upload';
  oldFile: File | null = null;
  oldCustomName: string = '';
  oldFolderName: string = '';
  oldSelectedFolderId : number | null = null;
  oldFolderDocuments: DocumentDto[] = [];
  oldSelectedDocumentId:number| null = null;


  newMode:  'upload' | 'existing' = 'upload';
  newFile: File|null= null;
  newCustomName: string = '';
  newFolderName:string = '';
  newSelectedFolderId: number| null= null;
  newFolderDocuments: DocumentDto[] = [];
  newSelectedDocumentId: number | null = null;

  isLoading:boolean = false
  results: any[] = [];

  constructor(
    private http : HttpClient,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadFolders();
  }
  loadFolders(): void {
    this.http.get<FolderDto[]>('http://localhost:5000/api/Folder/list')
      .subscribe({
        next: (folders) => {
          this.folders = folders;
          this.cdr.detectChanges();
        },
        error: (err) =>console.error('Klasörler yüklenmedi: ',  err)
      });
  }
  onOldFileSelected(event:any):void{
    const file = event.target.files[0];
    if(file){
      this.oldFile = file;
      this.oldCustomName = file.name.replace('.pdf','');
    }
  }
  onOldFolderChange():void{
    this.oldSelectedDocumentId = null;
    this.oldFolderDocuments  = [];
    if(this.oldSelectedFolderId){
      this.http.get<DocumentDto[]>(`http://localhost:5000/api/Folder/${this.oldSelectedFolderId}/documents`)
       .subscribe({
        next: (docs)=>{
          this.oldFolderDocuments = docs;
          this.cdr.detectChanges();
        },
        error:(err) => console.error('Belgeler yüklenmedi:',err)
       });
    }
  }
  onNewFileSelected(event:any) : void{
    const file = event.target.files[0];
    if(file) {
      this.newFile = file;
      this.newCustomName= file.name.replace('.pdf', '');
    }

    
  }
  onNewFolderChange():void{
    this.newSelectedDocumentId = null;
    this.newFolderDocuments = [];
    if(this.newSelectedFolderId){
      this.http.get<DocumentDto[]>(`http://localhost:5000/api/Folder/${this.newSelectedFolderId}/documents`)
      .subscribe({
        next: (docs) => {
          this.newFolderDocuments = docs;
          this.cdr.detectChanges();
        },
        error : (err) => console.error('Belgeler yüklenmedi:',err)
      });
    }
   }
   get canAnalyze():boolean{
    const oldReady = this.oldMode =='upload' ? !!this.oldFile:!!this.oldSelectedDocumentId;
    const newReady = this.newMode == 'upload'? !!this.newFile:!!this.newSelectedDocumentId;
    return oldReady && newReady;
    }

    analyzePdfs():void {
      if(!this.canAnalyze){
        alert('Lütfen eski ve yeni belge için gerekli  seçimleri tamamlayın.');
        return;
      }
      this.isLoading = true;
      this.results = [];

      const formData = new FormData();

      if(this.oldMode== 'upload'){
        formData.append('oldPdf',this.oldFile!);
        formData.append('oldCustomName',this.oldCustomName);
        formData.append('oldFolderName',this.oldFolderName);
      }else{
        formData.append('oldDocumentId',String(this.oldSelectedDocumentId));
      }
      if(this.newMode == 'upload'){
        formData.append('newPdf',this.newFile!);
        formData.append('newCustomName',this.newCustomName);
        formData.append('newFolderName',this.newFolderName);
      }else
      {
        formData.append('newDocumentId',String(this.newSelectedDocumentId));

      }

      this.http.post<any[]>('http://localhost:5000/api/Document/analyze', formData)
        .subscribe({
          next: (response) => {
            this.results = response;
            this.isLoading =false;
            this.loadFolders();
            this.cdr.detectChanges();
          },
          error : (err) => {
            console.error('Analiz hatası:',err);
            alert('Analiz sırasında bir hata oluştu: '+ (err.error?.detail || err.message));
            this.isLoading = false;
            this.cdr.detectChanges();

          }
        });
    }
    downloadCsv(): void {
    if (!this.results || this.results.length === 0) return;

    let csvContent = "data:text/csv;charset=utf-8,Madde No,Ana Baslik,Alt Baslik,Durum,Ai Analiz\n";
    this.results.forEach(row => {
      const madde = `"${row.MaddeNo || row.maddeNo || ''}"`;
      const anaBaslik = `"${(row.AnaBaslik || row.anaBaslik || '').replace(/"/g, '""')}"`;
      const altBaslik = `"${(row.AltBaslik || row.altBaslik || '').replace(/"/g, '""')}"`;
      const durum = `"${row.Durum || row.durum || ''}"`;
      const aiAnaliz = `"${(row.AiAnaliz || row.aiAnaliz || '').replace(/"/g, '""')}"`;
      csvContent += `${madde},${anaBaslik},${altBaslik},${durum},${aiAnaliz}\n`;
    });

    const encodedUri = encodeURI(csvContent);
    const link = document.createElement("a");
    link.setAttribute("href", encodedUri);
    link.setAttribute("download", "analiz_sonuclari.csv");
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  }
  
}
