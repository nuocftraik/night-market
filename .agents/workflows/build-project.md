---
description: Xây dựng ECO.WebApi project base từng bước theo tài liệu build docs
---

# Workflow: Build ECO.WebApi Project Base

## Quy tắc chung

1. **Đọc trước, code sau**: Trước mỗi bước, đọc KỸ tài liệu BUILD tương ứng trong `src/Host/Host/docs/`
2. **Học hỏi**: Hiểu TẠI SAO code được viết như vậy, không copy-paste mù quáng
3. **Verify sau mỗi bước**: `dotnet build` phải pass trước khi chuyển bước tiếp
4. **Ghi nhận bài học**: Nếu gặp lỗi hoặc sửa đổi khác docs, ghi vào `tasks/lessons.md`
5. **Dừng sau mỗi phase**: Notify user review trước khi tiếp tục phase mới
6. **Không skip bước**: Mỗi bước có prerequisites rõ ràng, PHẢI làm theo thứ tự

## Cấu trúc docs

Tất cả tài liệu nằm tại: `docs/`
- `BUILD_INDEX.md` — Mục lục tổng quan
- `BUILD_XX_*.md` — Từng bước chi tiết
- `SETUP_GUIDE.md` — Hướng dẫn setup

---

## Lộ trình 7 Phases

Mỗi phase có workflow riêng. **Chạy tuần tự, KHÔNG skip.**

| Phase | Workflow | Nội dung | Kết quả |
|-------|----------|----------|---------|
| 1 | `/build-phase1` | Foundation Setup (6 bước) | Solution build + API chạy |
| 2 | `/build-phase2` | Core Infrastructure (7 bước) | Logging + DB + Repository |
| 3 | `/build-phase3` | Core Services (3 bước) | CurrentUser + Exceptions + Validation |
| 4 | `/build-phase4` | Auth & Authorization (7 bước) | JWT + Permissions + OAuth2 |
| 5 | `/build-phase5` | Data Integrity (2 bước) | Soft Delete + Auditing |
| 6 | `/build-phase6` | Infrastructure Services (6 bước) | Caching + Email + Jobs |
| 7 | `/build-phase7` | Business Modules (13+ bước) | Export + Catalog + Payments |

---

## Quy trình thực hiện mỗi bước

```
1. Đọc BUILD doc tương ứng (view_file)
2. Hiểu: WHAT (làm gì), WHY (tại sao), HOW (thế nào)
3. Implement code theo docs
4. dotnet build → fix errors nếu có
5. Verify theo hướng dẫn trong docs
6. Mark bước hoàn thành trong tasks/todo.md
7. Chuyển bước tiếp
```

## Xử lý lỗi

- `dotnet build` fail → Đọc lại docs, check dependencies, fix errors
- Docs không rõ → Hỏi user trước khi giả định
- Conflict code hiện tại → Ghi vào `tasks/lessons.md`, hỏi user

## Lưu ý quan trọng

- **KHÔNG tạo project mới** nếu đã tồn tại — chỉ thêm/sửa code
- **KHÔNG skip bước** — mỗi bước phụ thuộc bước trước
- **KHÔNG tự ý thay đổi architecture** — follow docs chính xác
- **GHI NHẬN bài học** sau mỗi lỗi vào `tasks/lessons.md`
