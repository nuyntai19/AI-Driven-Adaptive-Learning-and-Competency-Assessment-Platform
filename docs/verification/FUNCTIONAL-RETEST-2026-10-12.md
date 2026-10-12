# Kiểm thử lại chức năng đã sửa và phạm vi ảnh hưởng

Lượt kiểm thử mới sau fetch nhánh answer, cùng các lỗi R04–R06 và một lượt SQL đầy đủ, được ghi tại [ANSWER-SYSTEM-RETEST-2026-10-12.md](ANSWER-SYSTEM-RETEST-2026-10-12.md). Hướng dẫn tự chạy: [SELF-TEST-GUIDE-2026-10-12.md](SELF-TEST-GUIDE-2026-10-12.md). Kết quả bên dưới giữ lịch sử đợt trước.

Ngày: 12/10/2026. Nhánh `student/answer`, working tree chứa các bản sửa đã mô tả trong [báo cáo sửa lỗi](FUNCTIONAL-FIXES-AND-VERIFICATION-2026-10-12.md). Đợt này phát hiện và sửa thêm R01/R02 trong browser và R03 khi rollback trên MySQL thật. Các ca đã chạy có kết quả cuối đạt yêu cầu; các lượt lỗi/aborted được giữ trong bằng chứng bên dưới.

## Phương pháp

- Chạy lại toàn bộ suite backend và frontend, lint, TypeScript/Vite production build và bundle budget.
- Khởi động lại API/web với schema và tài khoản MySQL disposable; không tạo/xóa fixture trong database chính.
- Chạy lại 21 regression HTTP chức năng, 24 acceptance bài tập và smoke bốn vai trò.
- Bổ sung 10 kiểm thử HTTP về các nhánh làm sai/làm lại, hoạt động offline, race import, lifecycle và ràng buộc trước ghi.
- Bật các suite relational bằng máy chủ MySQL **hoàn toàn riêng** ở loopback 3308, mật khẩu ngẫu nhiên, không dùng account root của database chính. Các suite tự tạo/migrate/drop schema riêng. Ca default-column cần database đã migrate được chạy riêng bằng VSTest với tài khoản của schema acceptance disposable.
- Kiểm tra qua trình duyệt thật cả luồng bài tập và lộ trình → luyện tập. Không dùng các script browser diagnostic cũ có thể báo PASS khi chỉ thấy loading.

## Kết quả các lượt đã hoàn tất

| Bộ kiểm thử | Kết quả |
|---|---|
| Backend toàn bộ, Release sau sửa R01/R02/R03 | 4165 passed, 102 skipped do thiếu connection trong lượt này, 0 failed; TRX `functional-retest-final-release-20261012.trx` |
| Frontend toàn bộ sau sửa R01/R02 | 696 passed, 0 failed, 0 skipped |
| Bundle budget | 3 passed, 0 failed |
| Lint / production build | 0 lint errors, 26 warnings; build thành công |
| HTTP regression F01–F21 | 21/21 passed |
| Assignment acceptance | 24/24 passed |
| HTTP phạm vi ảnh hưởng bổ sung | 10/10 passed |
| HTTP phiên bản kế hoạch và owner/subject/class boundary | 6/6 passed |
| Smoke bốn vai trò | 39/39 đúng HTTP status và JSON; có cả các nhánh bị từ chối quyền |
| Relational MySQL riêng | 112 ca riêng biệt có kết quả cuối Passed, gồm 101 ca skipped trong lượt backend thông thường |
| MySQL default column | VSTest 1/1 Passed trên schema đã migrate; default `questions.reasoning_required` = 0 |
| Docker API/web | Build thành công; 21 + 24 + 10 + 6 ca HTTP được chạy lại trên API image cuối |

Không cộng các ca trùng nhau giữa suite chính và relational. Cả 102 ca bị skip trong lượt thông thường đã được kiểm tra bổ sung: 101 trong relational và 1 default-column chạy riêng. `mysql-coverage.json` đối chiếu tên từng ca từ list-tests/TRX, kết quả mới nhất và lịch sử lượt chạy; đây là tổng hợp nhiều lượt, không phải một lượt SQL duy nhất không lỗi.

## Phạm vi chức năng có thể bị ảnh hưởng

| Nhóm | Kiểm tra |
|---|---|
| Bản nháp | Draft chỉ có trên server trước lần mở browser; sửa lập luận, đổi câu, refresh, quay lại câu đầu; giữ đáp án, lập luận mới và confidence. |
| Nộp / xem lại | Nộp toàn bộ bài 2 câu qua UI; kết quả đúng 2/2; cả hai câu chỉ đọc; lập luận mới vẫn có trong submitted attempt; kết quả AI fallback được phân biệt rõ với điểm chốt. |
| Lộ trình / luyện tập | Nút Bắt đầu gắn đúng sessionId, classId, subjectId; câu chỉ thuộc buổi; tiếp tục sau feedback không lấy đề xuất chung hoặc mang đáp án cũ sang câu mới. HTTP làm đủ câu cập nhật Completed/checkpoint trên path gốc. |
| Nhánh không đạt | Tất cả câu trả lời sai hợp lệ → NeedsReview; không cho Completed. Start lại cần attempt sau timestamp mới, không tái dùng bằng chứng cũ. |
| Hoạt động offline | Fixture buổi không có câu: Evaluate giữ InProgress; Completed thiếu acknowledgement bị từ chối; acknowledgement hoàn thành được, retry completion idempotent. |
| Import / lifecycle | CSV multiline, header Việt, XLSX inline/custom worksheet, DOCX, decimal, boolean, row count, role boundary và replay. Confirm đồng thời một winner; subject/topic inactive không lưu câu. Trigger giữ topic đang được dùng Active. |
| Báo cáo / thống kê | Suite dashboard/academic coverage, provisional/final CSV, formula escaping và note tenant/actor isolation; relational suites kiểm tra ledger/lifecycle và report score trên SQL thật. |
| Phân quyền / chấm bài / worker | Ownership, foreign center/student, timer authoritative, late submit, duplicate submit, version conflict, void, teacher override, attachments, notification/outbox và worker retry/fencing trong các suite có liên quan. |

## Ghi nhận khi chuẩn bị test

### R01 — Đã sửa: trạng thái câu trước còn trong luyện tự do

Browser buổi 5 câu ghi nhận các lượt attempt 18–21 có `answerChanges` lần lượt 1, 2, 3, 4 dù mỗi câu chỉ có một thao tác đổi đáp án. Handler Luyện câu tiếp theo trước đó chỉ reset answer/reasoning/confidence/time, không reset counter/ref, `answerDisplayLatex`, ảnh nháp/blob/time/modal/upload token và tracker. Counter là lỗi runtime đã tái hiện; nguy cơ gửi lại ảnh/token và công thức cũ được xác định từ handler và test trạng thái có dữ liệu cũ.

Đã gom thao tác chuyển câu vào `handleNextPracticeQuestion`, reset đầy đủ dữ liệu của câu trước và vô hiệu hóa tracker upload cũ. Test hồi quy chạy chính callback production với counter = 4, token đã dùng, ảnh và công thức cũ; kiểm tra cả luyện thường, session, và read-only. Ba test mới đều passed. Không thay logic tính điểm hay nới quyền.

Docker/browser sau sửa: câu đầu đổi B → A ghi counter 2; câu sau nhập đáp án một lần ghi counter 1. Buổi hai câu chuyển Completed; checkpoint 1 và progress tuần 33.3% được kiểm tra trực tiếp trên SQL (`functional-retest-counter-proof.json`).

### R02 — Đã sửa: quay lại mở nhầm phiên bản kế hoạch

Sau hai lượt làm, kế hoạch v7 vẫn giữ đúng buổi Completed/progress 33.3%, nhưng recommendation tự tạo bản v9 mới. Nút Quay lại trước đó mở bản hiện tại v9 với progress 0%, khiến buổi vừa học không hiện trong lịch đang xem.

Thêm tham số đọc `pathId` trên detailed endpoint, lọc theo tenant/student/subject và kiểm tra class/topic scope. Link quay lại lấy pathId từ session; cache tách theo bản; UI ghi rõ kế hoạch đã lưu v7 và có nút Xem lộ trình hiện tại. Đổi môn/lớp bỏ pathId cũ; generate mới trở về view hiện tại. Nút của buổi Completed mở xem lại mà không PATCH về InProgress. Không chép progress của v7 vào lịch v9 vì đây là hai kế hoạch khác nhau, không thay thuật toán recommendation.

API đã kiểm tra bản gốc giữ Completed/33.3%, bản hiện tại có identity khác, sai student/subject/class hoặc ID không tồn tại trả 404. Browser quay từ buổi Completed về bản v7 hiển thị **Đã xong**, **33.3%**, gợi ý buổi kế tiếp và nhãn phiên bản đúng.

### R03 — Đã sửa: rollback migration quyền vi phạm FK

Ca `Migration_Down_RollsBackPlatformAdmin_WithoutFkViolation` tái hiện lỗi thực: migration lịch sử xóa `permission_account_types` khi `role_permissions` vẫn tham chiếu. Khi sửa thứ tự xóa, provider còn thiếu metadata của seed entity trong một target model cũ. Đây là lỗi rollback, không phải lỗi assertion.

Không chỉnh sửa migration đã merge theo `CONSTITUTION.md`. Thêm `AuthorizationMigrationsSqlGenerator` cho provider MySQL: trước khi xóa cặp permission/account applicability, xóa chính các grant con tương ứng; cung cấp kiểu SQL của key cho các seed deletion cũ thiếu metadata rồi delegate cho provider. Không tắt FK và không thay query runtime. Bốn test generator Passed; rollback → migrate forward trên MySQL thật Passed, tập permission trước/sau bằng nhau và không có pending model changes.

Wrapper dùng tên implementation nội bộ của Oracle MySQL provider đang pin trong repo. Khi nâng cấp provider cần chạy lại các ca generator và migration vì tên/constructor có thể thay đổi. Kết quả này xác nhận rollback trên fixture, không chứng nhận recovery database production.

### Kết quả relational ban đầu và sửa harness

Lượt relational đầu hoàn tất 51 test passed, một test failed vì assertion hardcode số quyền 32 trong khi catalog hiện tại có 40, rồi bị treo ở cleanup fixture của `FinalReview_ObsoleteApprovalSuppressed_ReopenedVersionDelivered_RealSql`. Lượt này được ghi **aborted**, không tính là toàn suite passed.

Stack + async state machine cho thấy `MySql.Data` mắc trong timeout/drain của `DROP DATABASE` tại `NotificationMySqlTests.Fixture.DisposeAsync`; database đã biến mất, không có SQL đang chạy. Sửa cleanup dùng admin command đồng bộ với timeout 120s, vẫn ném lỗi nếu cleanup thất bại. Test quyền được đổi sang so sánh exact set với catalog authoritative để phát hiện cả quyền thiếu/thừa; không đổi quyền của ứng dụng. Hai case đã được chạy lại và passed.

Các 51 kết quả passed được giữ, 61 case chưa hoàn tất hoặc failed được chọn từ TRX/list-tests và chạy tiếp bằng Release trên MySQL server mới, timeout mỗi test 3 phút, không tạo memory dump tự động. Lượt nối tiếp dùng tmpfs cho dữ liệu database tạm để giảm chi phí DDL; vẫn kiểm tra constraint/transaction/concurrency SQL thật, không dùng kết quả này để chứng nhận phục hồi sau crash MySQL/power loss. Dump chẩn đoán riêng được lưu tạm ngoài repo và xóa sau phân tích.

Hai lượt đầu của kịch bản mở rộng cần sửa fixture/kỳ vọng, không phải sửa ứng dụng: inactive subject/topic trả **404** đúng contract, không phải 400; không thể deactivate topic đang có dependency vì trigger `EDUTWIN_ACADEMIC_DEPENDENCY` chặn. Kịch bản cuối dùng topic inactive chưa được sử dụng và kiểm tra riêng guard giữ topic đang dùng. Không tắt trigger/FK để làm test xanh.

Fixture unit mới cho bản kế hoạch lưu cũng được bổ sung hai JSON bắt buộc WeakTopicNodeIds/FocusTopicNodeIds trước khi chạy lại toàn suite Release; không bỏ assertion hoặc tắt nullability để làm test xanh.

Hai fixture SQL khác được sửa: ca upgrade database cũ bootstrap bằng catalog hiện tại nên vô tình chèn các permission của migration tương lai; fixture nay ghi lại tập lịch sử và loại các seed tương lai trước khi kiểm tra upgrade. Ca no-candidate từng có câu hỏi orphan không có giáo viên và deactivate topic đang chứa câu Active; fixture nay có giáo viên thật, archive câu trước khi deactivate và khôi phục Active khi topic đã active. Assertion stale watermark vẫn giữ nguyên. Test rollback thay số quyền hardcode 71 bằng exact set trước/sau.

Lịch sử: lượt đầu 51 Passed/1 Failed rồi aborted; lượt nối tiếp 58 Passed/3 Failed; lượt repair đầu 3 Failed; lượt repair sau 1 Passed/2 Failed; lượt cuối sau sửa fixture **3/3 Passed**. Đối chiếu tên từng test cho thấy **112/112** ca lựa chọn đã có kết quả cuối Passed. Ca default-column riêng **1/1 Passed**. Không che các lượt thất bại hoặc cộng lặp các ca chạy lại.

## Bằng chứng và chạy lại

Log/JSON được lưu dưới `docs/verification/evidence/functional_retest_20261012/` (gitignored, không chứa access token hoặc mật khẩu). TRX nằm trong `tests/EduTwin.BLL.Tests/TestResults/`.

```powershell
dotnet test tests/EduTwin.BLL.Tests --no-restore
cd web/edutwin-web
npm run verify
cd ../..
node scripts/e2e/functional_retest_mysql.cjs
node scripts/e2e/acceptance_stack.cjs start
try {
    node scripts/e2e/functional_audit_regression.cjs
    node scripts/e2e/assignment_api_acceptance.cjs
    node scripts/e2e/functional_retest_impact.cjs
    # Thực hiện kiểm tra browser trước khi stop.
} finally {
    node scripts/e2e/acceptance_stack.cjs stop
}
```

MySQL riêng cần port 3308 trống và image `mysql:8.0.35`. Harness không in credentials; cleanup chỉ xóa container có nhãn owner trùng runId và volume anonymous của chính container đó.

`scratch/functional-retest-coverage.py` kiểm tra 112 tên test trong list-tests đều có kết quả cuối Passed, đối chiếu 102 test skipped với relational/default-column, và assert status mong đợi của 39 route smoke. Ca student twin trả 400 khi fixture chưa có dữ liệu twin; đây là nhánh đã được kiểm tra, không tính là kết quả dashboard thành công.

Cleanup đã xác minh: API/web acceptance, schema/user disposable, server MySQL riêng và volume anonymous đã được xóa; manifest acceptance không còn. Các container chính API 5000, web 3000, MySQL 3307 và Adminer 8081 vẫn chạy; MySQL healthy. Image mới đã build và kiểm tra trên stack riêng, các container chính không được redeploy trong đợt retest này. `git diff --check` không phát hiện lỗi whitespace.

## Giới hạn kết luận

Test xanh chứng minh các kịch bản đã chạy, không chứng minh tuyệt đối mọi tổ hợp đều không có lỗi. Chưa kiểm thử live Gemini, nhiều API replica, stress tải lớn, mọi trình duyệt/mobile, fault injection offline/network, khôi phục backup hay upgrade database production. Các rủi ro kiến trúc và advisory dependency đã nêu trong báo cáo trước vẫn cần xử lý riêng. 26 lint warnings và cảnh báo bundle lớn chưa được coi là đã sửa.
