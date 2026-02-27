---
description: Auto commit và push code lên GitHub sau khi hoàn thành 1 chức năng/module cụ thể
---

# Workflow: Git Commit & Push

## Khi nào chạy
- **Sau khi hoàn thành 1 chức năng/tính năng cụ thể** (ví dụ: code xong 1 BaseController, thiết lập xong Serilog, tạo xong 1 table trong database).
- **KHÔNG đợi** xong cả 1 phase dài mới commit. Phải commit chia nhỏ theo từng bước (ví dụ sau mỗi tài liệu `BUILD_xx.md`).
- Khi user yêu cầu save progress.

## Quy trình

// turbo-all

1. Stage tất cả changes
```bash
git add -A
```

2. Commit với message chuẩn
```bash
git commit -m "<type>(<scope>): <description>"
```
- **type**: `feat` (feature mới), `fix` (sửa lỗi), `refactor` (tối ưu code), `docs` (tài liệu), `chore` (cấu hình/build)
- **scope**: tên module hoặc chức năng nhỏ (auth, users, logging, db, pipeline...)
- **description**: mô tả ngắn bằng tiếng Anh, rõ ràng chức năng vừa thêm

Ví dụ cho commit chia nhỏ:
- `feat(auth): implement JWT token generation service`
- `chore(logging): configure Serilog with console and file sinks`
- `feat(domain): add custom Identity entities`

3. Push lên remote
```bash
git push origin develop
```

4. Confirm push thành công
```bash
git log --oneline -1
```

## Lưu ý
- **KHÔNG push code bị lỗi build** — verify `dotnet build` trước khi commit
- **KHÔNG commit secrets** — đảm bảo .gitignore đúng
- Commit message bằng **tiếng Anh**, ngắn gọn, rõ ràng
