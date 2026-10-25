# Kiểm thử Adaptive Practice trên branch hiện tại

Ngày kiểm tra: 25/10/2026 (Asia/Saigon).

- Branch kiểm tra thực tế: `origin/SkinnySnail/adaptive-practice`.
- HEAD: `dd47b89dc5133022f7cb298c9bdf0adde5dbab2a`.
- Working tree ban đầu sạch. Thay đổi trong lượt này chỉ thuộc test harness/báo cáo; không sửa source chức năng.
- Docker Desktop đã được khởi động. Hai image thử nghiệm dùng tag riêng, không thay image `edutwin-api:latest`/`edutwin-web:latest`.

**Kết quả: BLOCKED_BEFORE_E2E. Đã chạy hai bước build prerequisite, chưa thực thi được luồng E2E nào.**

## Bằng chứng chạy

| Bước | Lệnh đã thực hiện | Kết quả |
|---|---|---|
| Branch guard | `git branch --show-current` | Đúng branch yêu cầu |
| Build API | `docker build --progress=plain --label org.opencontainers.image.revision=dd47b89 -f src/EduTwin.API/Dockerfile -t edutwin-adaptive-e2e-api:dd47b89 .` | FAIL, Docker exit 1, 33 lỗi C# phân biệt |
| Build web | `docker build --progress=plain --label org.opencontainers.image.revision=dd47b89 -f web/edutwin-web/Dockerfile -t edutwin-adaptive-e2e-web:dd47b89 web/edutwin-web` | FAIL, Docker exit 1; bước `npm run build` exit 2, 3 lỗi TypeScript |
| Khởi tạo runtime/schema E2E | Chỉ cho phép sau khi có API và web từ HEAD build thành công | BLOCKED |
| HTTP/MySQL acceptance | 36 ca đã khai báo trong suite mới | BLOCKED, chưa gọi API/SQL |
| Browser E2E | 12 kịch bản trong checklist | BLOCKED, chưa điều khiển trình duyệt |

Docker in lại một số dòng lỗi khi tổng kết build. Số 33/3 ở trên là số lỗi đã loại dòng trùng, không phải số lần xuất hiện trong log. Bản JSON có nội dung từng lỗi.

Không dùng kết quả unit test, component mock hay image cũ để gắn nhãn PASS cho E2E của HEAD này. Không tạo database/user test vì bước build chưa qua. Không có dữ liệu học sinh được ghi từ lượt kiểm thử này.

## Nguyên nhân đang chặn

**API – lỗi trong module Adaptive Practice:**

- `AdaptivePracticeEngine.cs:48` dùng `KnowledgeNode.Status` và `NodeStatus`, trong khi model dùng `IsActive`.
- Engine và hint generator dùng `KnowledgeNode.Name`, trong khi model dùng `NodeName`.
- Engine dùng `Subject.Name`, trong khi model dùng `SubjectName`.
- Phần giáo viên dùng `User.FullName`, thuộc tính không tồn tại trong model này.
- `StudentQuestionOptionDto` mơ hồ giữa hai namespace; `QuestionOption.Label/Text` không tồn tại.
- Có nullable dereference được cấu hình thành build error.

**Web – lỗi trong `AdaptivePracticeOverviewPage.tsx`:**

- Dòng 13: biến `user` không dùng (`TS6133`).
- Dòng 116: `SubjectDto` không có `name` và `code` (`TS2339`); DTO dùng `subjectName` và `subjectCode`.

Các lỗi này được xác nhận bằng build mới ngày 25/10, không suy ra từ kết quả kiểm tra ngày 18/10.

## Tệp kết quả

- [Kết quả từng kịch bản](RESULTS.md).
- [Kết quả JSON, lỗi compiler đầy đủ và revision](e2e-results.json).
- [Log build API](api-build.txt).
- [Log build web](web-build.txt).

## Chạy lại sau khi sửa lỗi build

Tại root repository, Docker Desktop đang chạy:

```powershell
node scripts/e2e/adaptive_practice_e2e_gate.cjs
```

Gate tự kiểm tra branch chính xác trước mọi build. Nếu branch khác yêu cầu thì SKIPPED. Nếu build lỗi thì ghi BLOCKED, exit code 1 và không khởi tạo runtime. Nếu build qua thì ghi READY_FOR_E2E; trạng thái này chưa phải kết quả chạy test nghiệp vụ.

Chỉ tiếp tục khi gate exit 0. Dùng đúng image vừa build:

```powershell
$adaptiveRevision = (git rev-parse HEAD).Trim()
$adaptiveTag = $adaptiveRevision.Substring(0, 7)
$env:EDUTWIN_ACCEPTANCE_API_IMAGE = "edutwin-adaptive-e2e-api:$adaptiveTag"
$env:EDUTWIN_ACCEPTANCE_WEB_IMAGE = "edutwin-adaptive-e2e-web:$adaptiveTag"
$env:EDUTWIN_ACCEPTANCE_SOURCE_REVISION = $adaptiveRevision
node scripts/e2e/acceptance_stack.cjs start
```

Stack dùng schema `edutwin_e2e_<runId>`, user/JWT/storage riêng; web ở `http://127.0.0.1:3002`, API ở `http://127.0.0.1:5001`. Seed account thử nghiệm được cấu hình bởi `acceptance_stack.cjs`, không dùng mật khẩu tài khoản thực. Việc startup/migrate/seed cần được xác nhận xong trước khi chạy suite.

```powershell
node scripts/e2e/adaptive_practice_api_acceptance.cjs --results-dir docs/verification/adaptive-practice-e2e-2026-10-25
```

Suite kiểm tra HTTP thật và đọc MySQL của schema test, ghi `adaptive_api_results.json` với từng PASS/FAIL/BLOCKED; không phải browser E2E. Suite không gọi AI thật và giả định seed có câu hỏi đủ điều kiện chấm xác định. Các test follow-up hiện chỉ xác minh ID mới và giữ lịch sử; liên kết predecessor vẫn phải kiểm chứng riêng vì contract chưa có trường đó. Các luồng mở rộng như chấm AI bất đồng bộ, tải, cạnh tranh request, bảo vệ đề, evidence coverage và full policy kết thúc cần thêm fixture/test tương ứng trước khi nghiệm thu toàn bộ đặc tả.

Giữ stack trong lúc kiểm tra trình duyệt tại cổng 3002. Đăng nhập tài khoản seed, đi qua APWEB01..APWEB12 trong RESULTS.md bằng browser-control được cho phép; mỗi ca cần xác nhận trạng thái cuối và chụp bằng chứng. Không dùng ChromeClient cũ vì README E2E của repository đã loại nó khỏi bộ acceptance.

Sau khi hoàn tất hoặc có lỗi, dọn đúng môi trường thử nghiệm theo manifest:

```powershell
node scripts/e2e/acceptance_stack.cjs stop
Remove-Item Env:EDUTWIN_ACCEPTANCE_API_IMAGE -ErrorAction SilentlyContinue
Remove-Item Env:EDUTWIN_ACCEPTANCE_WEB_IMAGE -ErrorAction SilentlyContinue
Remove-Item Env:EDUTWIN_ACCEPTANCE_SOURCE_REVISION -ErrorAction SilentlyContinue
```

`stop` chỉ dọn schema/user/container có ownership khớp manifest. Lượt kiểm tra ngày 25/10 chưa tạo stack thử nghiệm nên không có bước cleanup database cần chạy.
