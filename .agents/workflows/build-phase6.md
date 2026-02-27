---
description: "Phase 6: Infrastructure Services — Caching, File Storage, Email, Blob Storage, Background Jobs"
---

# Phase 6: Infrastructure Services

**Mục tiêu**: Tất cả infrastructure services hoạt động
**Prerequisites**: Phase 5 hoàn thành (Soft Delete + Auditing OK)

---

## Bước 21: Caching Services

```
Đọc file: docs/BUILD_21_Caching_Services.md
```

**Việc cần làm:**
- `ICacheService` interface
- `LocalCacheService` (IMemoryCache)
- `DistributedCacheService` (Redis/SQL Server)
- `CacheSettings` configuration
- Cache patterns (Cache-Aside, Write-Through)

**Verify:**
```bash
dotnet build
```

**Lưu ý:** Chạy workflow `/git-save` để commit code cho **Bước 21** trước khi sang bước tiếp theo.

---

## Bước 22: File Storage

```
Đọc file: docs/BUILD_22_File_Storage.md
```

**Việc cần làm:**
- `IFileStorageService` interface
- `LocalFileStorageService` implementation
- File upload/download/delete
- File validation (size, extension)
- `FileUploadRequest` DTO

**Verify:**
```bash
dotnet build
```

**Lưu ý:** Chạy workflow `/git-save` để commit code cho **Bước 22** trước khi sang bước tiếp theo.

---

## Bước 23: Email Service

```
Đọc file: docs/BUILD_23_Email_Service.md
```

**Việc cần làm:**
- `IMailService` interface
- `SmtpMailService` implementation (MailKit)
- `IEmailTemplateService` interface
- `EmailTemplateService` implementation (Razor templates)
- Email templates (WelcomeEmail.cshtml, ResetPasswordEmail.cshtml)
- `SMTPEmailSettings` configuration

**Verify:**
```bash
dotnet build
```

**Lưu ý:** Chạy workflow `/git-save` để commit code cho **Bước 23** trước khi sang bước tiếp theo.

---

## Bước 24: Azure Blob Storage

```
Đọc file: docs/BUILD_24_Blob_Storage.md
```

**Việc cần làm:**
- `IBlobStorageService` interface
- `AzureBlobStorageService` implementation
- Container management (create, delete, list)
- Blob operations (upload, download, delete, exists)
- SAS token generation
- `BlobStorageSettings` configuration
- `UploadBlobRequest`, `BlobModel` DTOs

**Verify:**
```bash
dotnet build
```

**Lưu ý:** Chạy workflow `/git-save` để commit code cho **Bước 24** trước khi sang bước tiếp theo.

---

## Bước 24-AWS (Optional): AWS S3 Storage

```
Đọc file: docs/BUILD_24_AWS_S3.md
```

**Việc cần làm:**
- `AwsS3StorageService` implementation (alternative cho Azure)
- AWS S3 bucket operations
- Pre-signed URL generation
- `AwsS3Settings` configuration

**⚠️ Optional**: Chỉ implement nếu user cần AWS thay vì Azure

**Verify:**
```bash
dotnet build
```

**Lưu ý:** Chạy workflow `/git-save` để commit code cho **Bước 24-AWS** trước khi sang bước tiếp theo.

---

## Bước 25: Background Jobs

```
Đọc file: docs/BUILD_25_Background_Jobs.md
```

**Việc cần làm:**
- `IJobService` interface
- `HangfireService` implementation
- Hangfire setup (SQL Server storage)
- Job scheduling (Fire-and-forget, Delayed, Recurring)
- `HangfireStorageSettings` configuration
- Hangfire dashboard (Basic Authentication)
- Job examples (Email sending, cleanup tasks)

**Verify:**
// turbo
```bash
dotnet build
dotnet run --project src/Host/Host/Host.csproj
```
Expect: Hangfire dashboard accessible tại https://localhost:7001/hangfire

**Lưu ý:** Chạy workflow `/git-save` để commit code cho **Bước 25** trước khi kết thúc phase.

---

## ✅ Phase 6 Checkpoint

**Kiểm tra:**
- [ ] `dotnet build` — 0 errors
- [ ] Caching: In-memory cache hoạt động
- [ ] File Storage: Upload/download logic compile OK
- [ ] Email: SMTP service compile OK
- [ ] Blob Storage: Azure/AWS service compile OK
- [ ] Hangfire: Dashboard accessible
- [ ] Thực hiện chỉnh sửa docs sau khi đã hoàn thiện phase này cho phù hợp (cập nhật docs nếu trong quá trình implement có thay đổi/tối ưu so với docs gốc).

**⏸️ DỪNG: Notify user review Phase 6 trước khi tiếp tục Phase 7 (`/build-phase7`)**
