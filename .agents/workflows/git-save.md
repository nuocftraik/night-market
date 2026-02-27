---
description: Auto commit và push code lên GitHub sau mỗi phase/task hoàn thành
---

# Workflow: Git Commit & Push

## Khi nào chạy
- Sau mỗi phase hoàn thành (Phase 1, Phase 2, ...)
- Sau mỗi task lớn có checkpoint verify thành công
- Khi user yêu cầu save progress

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
- **type**: `feat` (feature mới), `fix` (sửa lỗi), `refactor`, `docs`, `chore`
- **scope**: phase hoặc module (phase1, shared, domain, infrastructure...)
- **description**: mô tả ngắn bằng tiếng Anh

Ví dụ:
- `feat(phase1): foundation setup - solution, 5 layers, build config`
- `feat(phase2): core infrastructure - logging, DB, repository`
- `fix(domain): correct entity relationship`

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
