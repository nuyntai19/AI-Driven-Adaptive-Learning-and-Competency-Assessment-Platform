# Báo cáo sửa lỗi chức năng và kiểm thử hồi quy

Ngày hoàn tất: 12/10/2026. Nhánh làm việc: `student/answer`, nền code `d6688b4fb6ebcf9c79229006661e1462fb68d8c1`. Các thay đổi đang nằm trong working tree; chưa commit/push.

Kết quả kiểm thử lại theo yêu cầu tiếp theo của người dùng, bao gồm lỗi R01 bổ sung và các suite MySQL riêng, được ghi ở [FUNCTIONAL-RETEST-2026-10-12.md](FUNCTIONAL-RETEST-2026-10-12.md). Các số liệu bên dưới là lịch sử lượt nghiệm thu đầu.

## Phạm vi

Đợt này sửa 21 lỗi chức năng F01–F21 trong [báo cáo trước](FEATURE-BUG-AUDIT-2026-10-11.md), bổ sung đồng bộ điểm giáo viên với tiến độ buổi học và khắc phục giới hạn upload nginx S06. Hai báo cáo audit cũ được giữ nguyên làm lịch sử trước khi sửa.

Không coi các đề xuất kiến trúc và kiểm thử chưa chạy trong [báo cáo toàn hệ thống](SYSTEM-FLOW-CONSTRAINT-AUDIT-2026-10-11.md) là đã giải quyết hết. Không có kết luận hệ thống tuyệt đối không còn lỗi.

## Đối chiếu lỗi và thay đổi

| ID | Thay đổi | Bằng chứng kiểm thử |
|---|---|---|
| F01 | Chờ hydrate đúng scope trước hiển thị/persist/submit; hợp nhất draft server với dữ liệu local; chặn persist input của câu chưa hydrate. | Trình duyệt thật mở bài 2 câu, đổi câu, refresh rồi quay lại. Đáp án, lập luận và confidence trên server giữ nguyên; draftVersion tăng từ 1 lên 4. |
| F02 | Buổi học có route đọc riêng, gắn `sessionId` và `classId` với luyện tập; chỉ lấy câu trong buổi đã bắt đầu. Chuyển phạm vi học bỏ session cũ. | Unit ownership/navigation; HTTP next-question trả ID trong lịch đã giao. |
| F03 | Chỉ lập lịch từ câu Active, cùng tenant/môn/chủ đề; đọc schedule cũng lọc ID câu không còn Active. | Unit; HTTP chuyển câu sang Draft và generate lại không chứa câu đó. |
| F04 | Revalidate câu được đề xuất; nếu Archived/deleted/sai scope thì chọn lại câu Active. | HTTP archive câu đang được đề xuất, lần đọc sau không trả câu đó. |
| F05 | Validation trước mutation; đầu vào sai 400, môn không tồn tại 404, prerequisite/no candidate không khả dụng 409. | Unit; HTTP weeks/subject sai trả JSON 400/404. |
| F06 | Client gửi version đã đọc; cả stale version và race DB trả 409 thay vì 500. | Unit; hai PATCH cùng version trên MySQL trả một 204, một 409. |
| F07 | Server kiểm tra đã bắt đầu, đủ attempt của chính học sinh sau thời điểm start, không skipped, đúng các câu trong buổi và đạt tỷ lệ yêu cầu. Tiến độ tuần/phase và checkpoint cập nhật cùng schedule. Không biến task acknowledgement thành mastery. | Unit; HTTP chặn Completed chưa làm bài; nộp các câu được giao rồi Evaluate hoàn thành và kiểm tra checkpoint trên path gốc. |
| F08 | Whitelist enum, giới hạn note 1000 ký tự và danh sách topic; lỗi hiện rõ ngoài bảng hỏi; UI phân biệt lỗi đọc với chưa có path. | Unit validation; HTTP enum/note sai trả 400. |
| F09 | Self level, mode, pace, goal và weak/focus ảnh hưởng loại hoạt động, số câu/capacity, phân bổ và buổi kiểm tra. Giữ thứ tự prerequisite của engine. | Unit so sánh TheoryHeavy/PracticeHeavy/ExamPrep và câu Active. Đây là quy tắc lập lịch, không chứng minh hiệu quả sư phạm ngoài thực tế. |
| F10 | CSV đọc logical record nhiều dòng và escaped quotes, phát hiện quote chưa đóng. | Unit; HTTP CSV có newline trong ô cho đúng một hàng hợp lệ. |
| F11 | XLSX hỗ trợ inlineStr; tìm sheet qua workbook/relationships thay vì giả định sheet1. | Unit; HTTP workbook dùng custom sheet path và inline strings. |
| F12 | Chuẩn hóa dấu tiếng Việt và đ/Đ trước khi nhận diện header. | Unit; HTTP header tiếng Việt. |
| F13 | InvalidCount tính hàng lỗi distinct thay vì tổng lỗi trường. | Unit; HTTP dữ liệu boolean sai có một hàng lỗi. |
| F14 | Preview, tạo và sửa câu dùng chung ràng buộc MaxScore: >0, <=999.99, tối đa hai chữ số thập phân. | Unit biên decimal; HTTP preview score 1000 bị đánh dấu không hợp lệ. |
| F15 | Boolean không nhận diện được trở thành lỗi hàng, không âm thầm đổi thành false. | Unit; HTTP `tru` bị từ chối. |
| F16 | Hỗ trợ DOCX dạng bảng câu hỏi; UI nhận DOCX; hợp đồng API phản ánh quyền Teacher, không mở rộng quyền CenterManager. | Unit; HTTP DOCX header tiếng Việt parse thành công; Student/Manager preview 403. |
| F17 | Thống kê lớp của trung tâm dùng curriculum application hiện hành, không dùng liên kết chương trình kế hoạch hoặc fallback toàn môn. | Unit các application active/ended/archived/missing. |
| F18 | CSV báo cáo dùng trạng thái điểm như UI, phân biệt điểm tạm thời/đã chốt. | Frontend regression cho provisional/final. |
| F19 | Serializer CSV trung hòa chuỗi có tiền tố công thức và escape dấu nháy; số âm dạng numeric vẫn là số. | Frontend regression cho =,+,-,@ và whitespace/control prefix. Chưa thử mở file bằng Excel thực tế. |
| F20 | Ghi chú local tách khóa theo center + actor; sanitize JSON; không gán ghi chú legacy không biết tác giả cho giáo viên mới. | Frontend regression phân tách actor/tenant và JSON sai. Ghi chú legacy không bị xóa, nhưng không tự nhập sang scope mới. |
| F21 | Dashboard bỏ số 25 phút/3 câu hardcode, dùng mô tả không giả tạo số liệu. | Frontend build/regression toàn bộ. |

Phát hiện bổ sung trong kiểm thử: câu Essay/Manual có correctness ban đầu null nhưng giáo viên không được can thiệp vào free-practice theo Constitution. Schedule mới và reader session cũ loại các câu này khỏi bài luyện tự do, tránh trạng thái chờ chấm không thể giải quyết. Không mở rộng quyền Teacher; Essay vẫn dùng trong assignment có owner lớp. Nếu không còn câu tự chấm được, buổi chuyển sang hoạt động thay thế có acknowledgement tường minh. Tiến độ cũng đọc `ReasoningAnalysis.OverrideIsCorrect ?? Attempt.IsCorrect` thay vì bỏ qua correctness hiệu lực. Kết quả chưa chấm không tự coi là đúng.

S06: nginx `/api` tăng body limit từ 4m lên 11m để phù hợp import 10 MiB cộng multipart overhead và attachment 5 MiB. API vẫn áp dụng giới hạn riêng theo chức năng. Kiểm thử ảnh PNG hợp lệ lớn hơn 4 MiB qua proxy đã thành công.

## Kết quả kiểm thử

Không cộng số test targeted vào tổng suite vì chúng trùng nhau. Log và JSON đã khử thông tin xác thực được lưu ở `docs/verification/evidence/functional_fixes_20261012/` (evidence directory được gitignore).

- Backend toàn bộ ở code cuối cùng: **4161 passed, 102 skipped, 0 failed**, tổng 4263 test; kết quả TRX `functional-fixes-final-20261012.trx`.
- Regression backend scope/session sau bổ sung: **18 passed, 0 skipped, 0 failed**.
- Frontend: **692 passed, 0 failed, 0 skipped**.
- Bundle production: **3 passed, 0 failed**. Build TypeScript/Vite thành công.
- ESLint: **0 errors, 26 warnings**. Vite còn cảnh báo bundle lớn; chưa xử lý hiệu năng trong đợt này.
- Assignment API trên MySQL: **24/24 passed** (ownership, tenant, timer, version, conflict, submit idempotency, hết giờ, void, worker fallback).
- HTTP functional regression trên ảnh API cuối cùng và schema mới: **21/21 passed**, gồm lọc câu Manual, nộp đủ các câu trong buổi, Evaluate thành Completed và checkpoint tăng đúng trên path gốc.
- Role smoke: **39/39 đúng trạng thái mong đợi**. Request twin thiếu subject có chủ đích trả 400; route khác vai trò bị chặn 403.
- Browser draft: **passed** với fixture bài tập riêng; đối chiếu cả hai đáp án trên server trước/sau, không chỉ nhìn UI.

Docker build API/web thành công. Các kiểm thử HTTP dùng container riêng ở 5001/3002, schema MySQL disposable và attachment storage riêng; không sử dụng database chính để tạo/xóa fixture. AI key để trống: worker fallback được kiểm thử, không gọi dịch vụ AI thật.

Sau nghiệm thu, harness đã xóa container/schema/user thử và xác minh cleanup; database chính được giữ nguyên. `git diff --check` không báo lỗi whitespace. Bộ Assignment API cuối có evidence tại `docs/verification/evidence/assignment_submission_acceptance/daac44b21dc545e19a5267d3091e4ac0/`.

## Những điểm chưa được chứng nhận

| Audit hệ thống | Trạng thái sau đợt sửa |
|---|---|
| S01 – path chung theo học sinh/môn, scope hành động theo lớp | Session mới gắn lớp, owner và topic scope; chuyển scope bỏ session cũ. Chưa chuyển schema sang path độc lập theo curriculum application, chưa chứng nhận hai lớp cùng môn có lịch độc lập. |
| S02 – tính nguyên tử generate | Preference được lưu sau engine thành công và cùng phần save schedule. Chưa có transaction bao trọn mọi mutation bên trong engine và toàn luồng generate. |
| S03 – nhiều API replica | PATCH dùng DB concurrency. Generate semaphore vẫn theo process; chưa stress hai replica hay thay bằng distributed lock. |
| S04 – preview cache RAM | Token vẫn tenant/actor bound, one-shot; replay bị chặn. Preview còn phụ thuộc process/restart, chưa chuyển sang kho chia sẻ bền vững. |
| S05 – tài nguyên import | Thêm 10 MiB file, 5000 rows, 1 MiB logical CSV record, giới hạn số ZIP entries/tổng declared expanded length; revalidate subject/topic Active khi confirm. Chưa chứng nhận chống mọi ZIP bomb hoặc stress file ác ý; metadata ZIP không thay thế giới hạn đọc giải nén thực tế. |
| S06 | Đã sửa proxy cap và kiểm thử upload thực tế. |
| S07/S08 | Đã sửa các lỗi tiến độ/checkpoint, nguồn curriculum, trạng thái CSV, error mapping và hiển thị lỗi đã tái hiện. Chưa chuẩn hóa mọi metric/endpoint toàn hệ thống. |
| S09 – dependency advisories | Chưa nâng/thay dependency trong đợt sửa chức năng; baseline audit 11 findings (9 high, 2 moderate) vẫn cần triage. |
| S10 | Bổ sung HTTP trên MySQL và browser thật. 102 relational tests trong suite còn skipped do thiếu cấu hình riêng; không tính chúng là passed. |
| S11 | Fresh schema migrate/seed trên Docker đã chạy được; harness tăng thời gian readiness. Chưa diễn tập upgrade DB cũ, partial migration, backup/restore hoặc nhiều instance migrate đồng thời. |

Chưa chạy load test, mọi trình duyệt/mobile, offline/reconnect và mạng chậm có fault injection, AI provider thật, quét image hoặc penetration test. Không suy rộng unit test thành chứng nhận các trường hợp này.

## Cách chạy lại

```powershell
dotnet test tests/EduTwin.BLL.Tests
cd web/edutwin-web
npm test
npm run lint
npm run build
npm run test:bundle
cd ../..
docker compose build api web
node scripts/e2e/acceptance_stack.cjs start
node scripts/e2e/functional_audit_regression.cjs
node scripts/e2e/assignment_api_acceptance.cjs
node scripts/e2e/acceptance_stack.cjs stop
```

Harness cần Docker/MySQL đang sẵn sàng và quyền tạo schema tạm. Luôn stop để dọn fixture kể cả khi test fail. Không chạy các SQL fixture lên DB production.

Ảnh Docker đã build không tự thay code trong các container chính đang chạy. Đợt này kiểm chứng bằng stack cô lập; việc triển khai lại stack chính và kiểm chứng upgrade trên dữ liệu thật là bước riêng.
