# Analyzer — Yapay Zekâ Destekli Doküman Karşılaştırma

Mergasoft bünyesinde Staj 1 kapsamında geliştirilen web uygulaması. Teknik şartnamelerin ve müşteriye özel gereksinim (CSR) dokümanlarının eski ve yeni PDF sürümlerini madde bazında karşılaştırır.

## Özellikler

- PDF metnini PdfPig ile çıkarma; açık numaralı başlıklarda kaynak metni doğrudan koruma, diğer düzenlerde Gemini ile ayrıştırma.
- Madde numarasına göre eşleştirme; eklenen, kaldırılan ve değişen maddeleri listeleme.
- Değişiklikler için Türkçe yapay zekâ açıklamaları.
- Klasörler altında belge saklama ve kayıtlı belgelerle karşılaştırma.
- SHA-256 ile aynı dosyayı tanıma ve önceki karşılaştırma sonuçlarını yeniden kullanma.
- Sonuçları Excel ile açılabilen CSV dosyası olarak indirme.

## Teknolojiler

Angular, TypeScript, ASP.NET Core (.NET 9), C#, Entity Framework Core, PostgreSQL, PdfPig ve Google Gemini API.

## Gereksinimler

- .NET 9 SDK
- Node.js 22.22.3+, 24.15.0+ veya 26+ ve npm (projede kurulu Angular paketinin sürüm koşulu).
- pgvector eklentisi kurulu PostgreSQL sunucusu. Mevcut migration geçmişi bu eklentiyi gerektirir; güncel karşılaştırma akışı embedding üretmez veya vektör araması yapmaz.
- Gemini API anahtarı ve seçilen modele erişim.

## Yerelde çalıştırma

### 1. Arka uç ayarları

Proje kökünde PowerShell ile:

```powershell
Copy-Item src/backend/appsettings.Development.json.example src/backend/appsettings.Development.json
```

Bu komutu yalnızca yerel ayar dosyanız henüz yoksa çalıştırın. Oluşan dosyada `ConnectionStrings:DefaultConnection` ve `Gemini:ApiKey` alanlarını doldurun. `Gemini:Model` varsayılanı `gemini-3.6-flash` değeridir; hesabınızın erişebildiği uyumlu bir metin modeliyle değiştirebilirsiniz. Anahtarları Git'e eklemeyin.

### 2. Veritabanı

PostgreSQL üzerinde `docsence_db` adlı boş veritabanını oluşturun. pgvector eklentisini sunucuya kurduktan sonra yetkili kullanıcıyla bu veritabanında çalıştırın:

```sql
CREATE EXTENSION IF NOT EXISTS vector;
```

Ardından proje kökünde:

```powershell
dotnet tool restore
dotnet restore src/backend/Analyzer.csproj
dotnet ef database update --project src/backend --startup-project src/backend -- --environment Development
```

Tablolar için EF Core migration dosyaları esas alınır. `database/scripts` altındaki dosyalar eski dışa aktarımlardır; migration kurulumu ile birlikte kullanılmamalıdır.

### 3. Arka ucu başlatma

```powershell
cd src/backend
dotnet run -- --environment Development
```

API adresi: `http://localhost:5000`.

### 4. Ön yüzü başlatma

İkinci bir terminalde:

```powershell
cd src/frontend
npm ci
npm start
```

Tarayıcıda `http://localhost:4200` adresini açın. `/api` istekleri `proxy.conf.json` aracılığıyla arka uca iletilir. API portunu değiştirirseniz bu dosyayı da güncelleyin. Üretim barındırmasında `/api` yolunun arka uca yönlendirilmesi gerekir.

## Kullanım

1. Eski ve yeni belgeyi yükleyin veya kayıtlı belgelerden seçin.
2. İsteğe bağlı belge ve klasör adlarını girin.
3. Analizi başlatıp madde bazındaki sonuçları inceleyin.
4. Sonuçları CSV olarak indirin.

## Kontroller

```powershell
dotnet build src/backend/Analyzer.csproj
dotnet run --project tests/backend-regression
cd src/frontend
npm run build
npm test -- --watch=false
```

Kaynak metin regresyon kontrollerini gerçek PDF sürümleriyle de çalıştırabilirsiniz:

```powershell
dotnet run --project tests/backend-regression -- "C:/path/Rev1.pdf" "C:/path/Rev2.pdf"
```

## Sınırlar

Metin içermeyen taranmış PDF'ler için OCR desteği yoktur. PDF metni analiz için Google Gemini servisine gönderilir. Yapay zekâ açıklamaları kullanıcı tarafından kontrol edilmelidir. Uygulama yerel staj/prototip kullanımı için hazırlanmıştır; kullanıcı kimlik doğrulaması içermez.

## Proje yapısı

- `src/backend`: API, belge işleme servisleri, veri modeli ve migration dosyaları.
- `src/frontend`: Angular kullanıcı arayüzü.
- `samples/pdf`: örnek PDF dosyaları.
- `database/scripts`: eski veritabanı dışa aktarımları.

Gemini model bilgisi: [Google AI dokümantasyonu](https://ai.google.dev/gemini-api/docs/models).
