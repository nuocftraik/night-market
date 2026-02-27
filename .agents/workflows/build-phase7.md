---
description: "Phase 7: Business Modules — Export, PDF, Catalog, Notifications, DB Designs, Payments"
---

# Phase 7: Business Modules

**Mục tiêu**: Business features hoàn chỉnh
**Prerequisites**: Phase 6 hoàn thành (Infrastructure Services OK)
**⚠️ Phase này đang phát triển — một số docs có thể chưa hoàn thiện**

---

## Bước 26: Export Services

```
Đọc file: docs/BUILD_26_Export_Services.md
```

**Việc cần làm:**
- Excel export với ClosedXML
- CSV export
- Export templates, dynamic column mapping
- Batch export operations

**Verify:**
```bash
dotnet build
```

**Lưu ý:** Chạy workflow `/git-save` để commit code cho **Bước 26** trước khi sang bước tiếp theo.

---

## Bước 27: PDF Export

```
Đọc file: docs/BUILD_27_PDF_Export.md
```

**Việc cần làm:**
- PDF generation với QuestPDF/iTextSharp
- Invoice/Report templates
- Header/Footer customization
- Charts, images embedding
- Watermarks, digital signatures

**Verify:**
```bash
dotnet build
```

**Lưu ý:** Chạy workflow `/git-save` để commit code cho **Bước 27** trước khi sang bước tiếp theo.

---

## Bước 28: Catalog Module (4 parts)

```
Đọc theo thứ tự:
1. docs/BUILD_28_Catalog_Module.md (Overview)
2. docs/BUILD_28_Domain_Layer.md (Domain entities)
3. docs/BUILD_28_Application_Layer.md (CQRS patterns)
4. docs/BUILD_28_Infrastructure_Controllers.md (REST APIs)
```

**Việc cần làm:**
- Product/Category domain entities
- CQRS Commands/Queries separation
- FluentValidation rules, Mapster configuration
- Controllers + Swagger documentation

**Verify:**
```bash
dotnet build
```

**Lưu ý:** Chạy workflow `/git-save` để commit code cho **Bước 28** trước khi sang bước tiếp theo.

---

## Bước 29: Notifications

```
Đọc file: docs/BUILD_29_Notifications.md
```

**Việc cần làm:**
- SignalR hub setup
- Real-time push notifications
- Notification entity
- In-app notification center
- Event-Driven auto-send

**Verify:**
```bash
dotnet build
```

**Lưu ý:** Chạy workflow `/git-save` để commit code cho **Bước 29** trước khi sang bước tiếp theo.

---

## Bước 30: Identity Module Multi-Group DB Design

```
Đọc file: docs/BUILD_30_Database_Design_Identity_Module_MultiGroup.md
```

**Lưu ý:** Chạy workflow `/git-save` để commit code cho **Bước 30** trước khi sang bước tiếp theo.

---

## Bước 31: Catalog Module DB Design

```
Đọc theo thứ tự:
1. docs/BUILD_31_Database_Design_Catalog_Module.md
2. docs/BUILD_31_Part2.md
```

**Lưu ý:** Chạy workflow `/git-save` để commit code cho **Bước 31** trước khi sang bước tiếp theo.

---

## Bước 32: Order & Cart Module DB Design

```
Đọc theo thứ tự:
1. docs/BUILD_32_Database_Design_Order_Cart_Module.md
2. docs/BUILD_32_Part2.md
```

**Lưu ý:** Chạy workflow `/git-save` để commit code cho **Bước 32** trước khi sang bước tiếp theo.

---

## Bước 33: Inventory Module DB Design

```
Đọc file: docs/BUILD_33_Database_Design_Inventory_Module.md
```

**Lưu ý:** Chạy workflow `/git-save` để commit code cho **Bước 33** trước khi sang bước tiếp theo.

---

## Bước 34: Payment Gateway Integration

```
Đọc theo thứ tự:
1. docs/BUILD_34_Database_Design_Payment_Gateway_Integration.md
2. docs/BUILD_34_Part2_Webhooks_Refunds_Security.md
```

**Lưu ý:** Chạy workflow `/git-save` để commit code cho **Bước 34** trước khi sang bước tiếp theo.

---

## Bước 35-38: Payment Providers

```
Đọc theo thứ tự:
- docs/BUILD_35_Payment_Gateway_VNPay_Integration.md
- docs/BUILD_36_Payment_Gateway_Momo_Integration.md
- docs/BUILD_37_Payment_Gateway_ZaloPay_Integration.md
- docs/BUILD_38_Payment_Gateway_VietQR_Integration.md
```

**Lưu ý:** Chạy workflow `/git-save` để commit code cho **Bước 35-38** trước khi kết thúc phase.

---

## ✅ Phase 7 Checkpoint

**Kiểm tra:**
- [ ] `dotnet build` — 0 errors
- [ ] Export services compile OK
- [ ] Catalog CRUD hoạt động
- [ ] Notifications SignalR hoạt động
- [ ] Database designs applied thành công
- [ ] Payment integrations compile OK
- [ ] Thực hiện chỉnh sửa docs sau khi đã hoàn thiện phase này cho phù hợp (cập nhật docs nếu trong quá trình implement có thay đổi/tối ưu so với docs gốc).

**⏸️ DỪNG: Notify user review Phase 7 — Project base HOÀN THÀNH! 🎉**
