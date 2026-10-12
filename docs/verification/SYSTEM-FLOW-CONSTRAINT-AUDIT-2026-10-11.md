# Báo cáo rà soát toàn hệ thống: chức năng, luồng, xung đột và ràng buộc

Ngày kiểm tra: 11/10/2026. Nhánh `student/answer`, commit `d6688b4fb6ebcf9c79229006661e1462fb68d8c1`.

## Kết luận

Các kiểm tra tự động hiện có vượt qua, và những đường biên quyền/tenant đã thử hoạt động đúng. Tuy nhiên, kiểm thử bổ sung phát hiện mất draft qua giao diện, lịch chứa câu Draft, cập nhật lộ trình trả 500 và nhiều lỗi import. Hệ thống cần xử lý các lỗi này trước khi coi luồng học chính đủ ổn định.

Báo cáo này tổng hợp khả năng xảy ra xung đột giữa các phân hệ và các ràng buộc còn thiếu. Chi tiết 21 lỗi chức năng nằm trong [FEATURE-BUG-AUDIT-2026-10-11.md](FEATURE-BUG-AUDIT-2026-10-11.md); các mã F bên dưới dẫn tới báo cáo đó. Không sửa mã ứng dụng trong đợt kiểm tra.

## 1. Phạm vi và độ tin cậy

| Hình thức | Kết quả | Giới hạn |
|---|---|---|
| Build Docker | API/web thành công từ nhánh mới nhất | Build thành công không chứng minh các luồng runtime đúng |
| Backend Release | 4.138 passed, 102 skipped, 0 failed | Các MySQL integration cần connection riêng bị skip |
| Frontend unit | 688 passed, 0 failed | Không thay thế kiểm thử browser hydration/autosave |
| Frontend lint | 0 errors, 26 warnings | Có cảnh báo hook dependency/fast refresh; chưa coi mỗi warning là bug |
| Acceptance HTTP + MySQL | 24 passed, 0 failed | Tập trung assignment/draft/submit, chưa phủ mọi phân hệ |
| Smoke API | 39 request qua Student/Teacher/CenterManager/PlatformAdmin | Kiểm tra response/quyền trên dữ liệu fixture; không phải test toàn bộ API |
| Probe bổ sung | Import, schedule, trạng thái buổi, câu Archived/Draft, export | Một số gọi use case/serializer trực tiếp, không phải toàn bộ UI |
| Browser | Đăng nhập, mở lại bài có draft, đổi câu, kiểm tra autosave | Đã chứng minh F01; chưa chạy ma trận browser/mobile đầy đủ |

Đọc các quy định liên quan trong `CONSTITUTION.md`, `PROJECT_REQUIREMENTS.md`, `API_CONTRACTS.md`, `UI_UX_SPEC.md` và đối chiếu code. Các quy định có thể khác nhau theo thời điểm; những chỗ lệch được báo để chốt chính sách, không tự mở thêm quyền.

Các thao tác ghi dữ liệu diễn ra trong schema disposable của harness, API `127.0.0.1:5001`, web `127.0.0.1:3002`. Không gọi AI provider thật, không sửa dữ liệu học tập chính. Stack chính Docker vẫn giữ nguyên. Bằng chứng được giữ tại [evidence/system_audit_20261011](evidence/system_audit_20261011).

## 2. Ma trận chức năng và luồng

| Phân hệ | Đã kiểm tra | Kết quả/vấn đề | Còn cần xác minh |
|---|---|---|---|
| Đăng nhập, vai trò | auth/me bốn vai trò, UI login, policy API | Đường đã thử đúng; cache clear khi login/logout | Refresh/token expiry, revoke, đổi membership khi đang có phiên |
| Tenant và platform | Tenant gọi platform; platform gọi academic; foreign student/center truy cập assignment | 403 theo phạm vi đã thử | Audit đầy đủ mọi endpoint/export/background job; đây chưa phải pentest |
| Quản lý trung tâm/lớp | GET students/teachers/classes/subjects và scope lớp trong use case | Smoke thành công | Chuyển lớp, ngừng học, nghỉ giáo viên khi có assignment/path đang chạy |
| Curriculum/prerequisite | Resolver/phạm vi, application ledger, generate với graph bị chặn | Lệch nguồn thống kê F17; graph blocked trả 500 F05 | Publish/switch/unpublish cạnh tranh và phạm vi lịch sử |
| Ngân hàng câu hỏi | Active/Draft/Archived, điều kiện submit | Lộ trình chọn Draft F03; đề xuất cũ vẫn trả Archived F04 | Archive trong lúc preview import/publish assignment/start practice |
| Import | CSV hợp lệ, confirm lại, role; các input biên; XLSX parser | F10–F16 | File lớn, nhiều sheet, zip bomb, nhiều replica, retry đồng thời |
| Bài tập | Clock, start, draft version, submit replay, foreign questions, void/score | 24 acceptance đạt; UI làm mất draft F01 | Browser offline/reconnect, ảnh/LaTeX hydrate, nhiều tab và hết giờ khi mạng chậm |
| Luyện tập và Twin | next-question, status revalidation, submit validator; code AI processing | F04 và thiếu nối buổi F02 | AI provider thật, thay đổi mastery/prerequisite qua chuỗi attempt lâu dài |
| Lộ trình | Generate hợp lệ/âm tính, PATCH đồng thời, completion, preferences | F02–F09; rủi ro scope/transaction ở phần 3 | Đa lớp cùng môn, replan nhiều tab, lịch cũ sau đổi curriculum |
| Báo cáo/export | Quyền report, normalization score; CSV production serializer | F17–F21 | Dữ liệu lớn, Excel thực tế, provisional → final → override xuyên các màn |
| Inbox/outbox/workers | Smoke inbox, đọc lease/fencing và revalidation audience/tenant | Chưa tìm được lỗi cụ thể ở phần đã đọc | Fault injection crash/retry, hết lease, account đổi quyền giữa enqueue/delivery |
| Vận hành platform | GET centers/operations/AI usage/storage | Smoke thành công, academic bị chặn | Chưa thử purge/restore/destructive operations hay chốt số liệu billing |
| Upload/storage | Giới hạn ở API và nginx, lifecycle temp/permanent/cleanup qua code | Mâu thuẫn dung lượng S06 | Upload/download thực tế sát biên, nonce replay, storage outage/cleanup race |
| CSDL/migration | MySQL thật với fixture; cấu hình decimal/string/concurrency | Test đã tìm lỗi không xuất hiện trong InMemory | Migration từ bản cũ có dữ liệu, restore backup, nhiều process migration cùng lúc |
| Triển khai/dependencies | Docker build, npm audit | Build đạt; 11 dependency findings | Scan image/.NET dependencies, TLS, secrets, production error handling/load |

## 3. Xung đột và ràng buộc cần bổ sung

### S01 — P1/P2 — Lộ trình được lưu theo học sinh + môn nhưng hành động có phạm vi lớp

**Căn cứ mã nguồn:** `GenerateLearningPathUseCase` dùng lock/supersede theo center + student + subject; `LearningPath`/preference không lưu ClassId tương ứng. Resolver lại xác định nội dung khả dụng từ lớp/curriculum đang chọn.

**Kịch bản:** một học sinh thuộc hai lớp cùng môn nhưng khác curriculum/topic. Generate từ lớp B có thể thay thế active path môn đó của lớp A; khi mở lại từ lớp A, lịch có thể thuộc phạm vi B hoặc bị scope chặn. Chưa dựng hoàn chỉnh hai lớp để tái hiện nên đây là rủi ro có căn cứ, không ghi là lỗi API đã chứng minh.

**Ràng buộc cần chốt:** một path chung cho môn hay riêng cho từng lớp/curriculum application? Unique key, supersede, cache key và resolver phải cùng nghĩa. Test hai lớp cùng môn, chuyển application và mở hai tab trước khi chọn schema sửa.

### S02 — P2 — Lưu preference trước khi generate xong, thiếu tính nguyên tử toàn luồng

[GenerateLearningPathUseCase.cs](../../src/EduTwin.BLL/Recommendations/UseCases/GenerateLearningPathUseCase.cs) SaveChanges preference khoảng 93–94 trước gọi engine; tạo/publish schedule ở các bước sau khoảng 97–113. Engine có thể throw, đã quan sát request blocked trả 500.

**Rủi ro:** preference mới được lưu nhưng path cũ vẫn tồn tại hoặc path/schedule chưa hoàn chỉnh. Đợt này chưa query để xác nhận preference tồn tại sau từng failure, vì vậy kết luận về trạng thái dở dựa trên thứ tự mutation trong code.

**Đề xuất:** transaction cho phần mutation có thể gom; nếu engine có tác vụ ngoài DB, dùng draft/staging và publish atomically sau thành công. Failure không được làm trạng thái active trở nên khó diễn giải. Test exception trước/sau từng SaveChanges và retry cùng input.

### S03 — P2 — Lock trong bộ nhớ không bảo vệ giữa nhiều API instance

Generate dùng static ConcurrentDictionary cho semaphore theo phạm vi; chưa loại key sau dùng. Nó chỉ serialize trong một process.

**Rủi ro:** nhiều replica cùng generate có thể supersede/publish cạnh tranh; danh sách key tăng theo số student/subject. Chưa chạy stress hoặc nhiều replica, nên không kết luận đã có active path trùng trong DB.

**Đề xuất:** invariant DB cho active path, concurrency/transaction theo scope, hoặc distributed lock có owner/expiry nếu thật sự cần. Cleanup lock entry có cơ chế an toàn. Test hai process generate cùng scope, response xác định và tối đa một active path hợp lệ.

### S04 — P2 — Preview import gắn với RAM của một process

`QuestionImportUseCase.PreviewCache` là static dictionary; token gắn tenant/actor và có TTL, là điểm bảo vệ tốt. Nhưng restart mất preview; replica khác không có token. Cleanup entry hết hạn chỉ chạy khi có preview mới.

**Ảnh hưởng khi triển khai:** preview thành công rồi confirm có thể lỗi nếu request tới node khác hoặc vừa rollout. Bộ nhớ giữ entry hết hạn khi không có thêm preview. Đây là nhận xét kiến trúc, chưa thử multi-replica/restart.

**Đề xuất:** cache dùng chung hoặc persistence ngắn hạn với owner, TTL, checksum và consumed state; confirm phải atomically consume nếu yêu cầu tránh replay đồng thời. Đã kiểm tra confirm lặp tuần tự bị chặn; chưa kết luận confirm đồng thời cũng được bảo vệ đầy đủ.

### S05 — P2 — Import chưa đủ giới hạn tài nguyên và schema invariant

Parser tải XML/XLSX/rows; chưa thấy cap rõ ràng về số hàng, tổng dung lượng giải nén, cell/string length. HTTP body cap không chặn file ZIP nhỏ có nội dung giải nén rất lớn. Confirm SaveChanges theo từng question rồi options khoảng 636/661, tạo nhiều round trip trong transaction.

**Rủi ro chưa stress-test:** CPU/RAM/transaction kéo dài, timeout và rollback khi một chuỗi vượt schema; tác động tăng theo số hàng. Không tuyên bố DoS đã khai thác.

**Đề xuất:** giới hạn compressed/uncompressed bytes, entry count, rows, field lengths và decimal ngay preview; trả lỗi hàng cụ thể. Batch insert hợp lý và test rollback toàn file. Revalidate subject/topic đang active và cùng scope tại confirm, đặc biệt khi quản trị thay đổi sau preview.

### S06 — P2 — nginx cho 4 MiB nhưng attachment API cho 5 MiB

**Xác định chắc chắn từ cấu hình, chưa upload sát biên trong deployment production:** [nginx.conf](../../web/edutwin-web/nginx.conf) dòng 25 đặt `client_max_body_size 4m` cho `/api/`; [AttemptAttachmentsController.cs](../../src/EduTwin.Api/Controllers/AttemptAttachmentsController.cs) khoảng 177 báo PNG tối đa 5 MB, storage cũng enforce 5 MB.

**Hệ quả khi đi qua nginx này:** file PNG >4 MiB đến 5 MiB bị proxy trả 413 trước khi API áp dụng giới hạn đã công bố; multipart overhead còn làm ngưỡng file thực tế thấp hơn 4 MiB. Stack acceptance dùng nginx riêng nên không lấy test ở đó làm bằng chứng upload production thành công.

**Đề xuất:** đồng bộ API, proxy và UI; cap proxy phải có dư cho multipart nhưng vẫn giới hạn an toàn. Test 3,9/4,1/4,9/5,1 MiB qua web proxy và API trực tiếp, response nhất quán, không để orphan file.

### S07 — P2 — Trạng thái học và dữ liệu báo cáo chưa có invariant thống nhất

F07 đã chứng minh tiến độ tăng trong khi checkpoint tasks vẫn 0. F17 cho thấy nguồn curriculum thống kê khác phạm vi học. F18 cho thấy export mất trạng thái điểm.

**Ràng buộc cần ghi rõ:** tiến độ tự đánh dấu hay bằng chứng attempt? Chương trình kế hoạch hay đang áp dụng? Điểm provisional có được tính trung bình/xếp hạng không? Lịch sử giữ theo snapshot nào khi thay curriculum, câu hỏi hoặc override?

**Đề xuất:** định nghĩa nguồn authoritative cho mỗi metric và hiển thị status/as-of scope; dùng chung resolver/calculator và test dữ liệu lịch sử. Không sửa bằng cách chỉ làm các con số bằng nhau khi chúng vốn có nghĩa khác nhau.

### S08 — P2 — Error contract chưa thống nhất tại các luồng lộ trình

Validation, missing subject, prerequisite blocked, chuỗi quá dài và concurrency đã trả 500 trong thử nghiệm. Giao diện lộ trình không đọc `isError` đầy đủ ở detailed query khoảng 66–73; dữ liệu không có có thể dẫn sang bảng hỏi. Thông báo `formError` nằm trong bảng hỏi nên lỗi start khi bảng hỏi đóng có thể không thấy.

**Đề xuất:** validation trước mutation, map expected exception sang ProblemDetails/mã nghiệp vụ, log correlation; UI phân biệt loading/empty/forbidden/conflict/business error/network error. Production phải có exception handler không lộ thông tin nội bộ. Không suy ra cấu hình production leak chỉ từ Development fixture.

### S09 — P2 — Phụ thuộc frontend có advisory chưa xử lý

`npm audit --json` báo **11 package findings: 9 high, 2 moderate, 0 critical**; chi tiết package, advisory, affected range và fixAvailable trong `npm-audit.json`. Có phụ thuộc trực tiếp `axios`, `xlsx`; cần ưu tiên kiểm tra các đường sử dụng thực tế.

Đây là kết quả dependency scanner, không phải 11 khai thác đã chứng minh. Advisory về runtime Node có thể không áp dụng nguyên vẹn tới browser; một gói có nhiều advisory và gói phụ thuộc gián tiếp có thể trùng nguyên nhân.

**Đề xuất:** phân loại reachability, nâng phiên bản tương thích, thay thế gói khi không có bản fix phù hợp; chạy lại test/import/export/build. Không chạy `npm audit fix --force` tự động trong đợt báo cáo. Chưa scan dependency .NET và OS trong image nên không coi đây là tổng số lỗ hổng hệ thống.

### S10 — P2 — Khoảng trống giữa unit test và môi trường thật

Test InMemory không tái hiện đầy đủ decimal/string limits, MySQL transaction/concurrency và ràng buộc FK. Những phép thử mới trên MySQL đã tìm F06/F08/F14; browser tìm F01 trong khi unit/acceptance vẫn xanh.

**Đề xuất:** CI bắt buộc một bộ relational test trên schema disposable và browser test các luồng dữ liệu quan trọng. Các test cần MySQL phải fail/skip có chủ đích và công bố rõ, tránh hiểu 102 skipped là đã kiểm chứng. Chọn regression theo invariant, không chỉ thêm source-pattern assertions.

### S11 — P2/P3 — Migration và khả năng khôi phục cần thử độc lập

Harness fresh schema hai lần không kịp ready với thời gian đợi cũ; lần chạy tăng thời gian đợi thành công. Đây là giới hạn harness/startup, không ghi thành lỗi dữ liệu người dùng. MySQL có DDL không nằm trọn trong transaction, nên cần kiểm tra partial migration/restart đối với bản cũ có dữ liệu.

**Đề xuất:** backup/restore diễn tập, migration upgrade trên bản sao dữ liệu, failure giữa các bước và serialize migration khi nhiều instance khởi động. Chưa thử recovery nên không chứng nhận an toàn nâng cấp chỉ từ fresh build.

## 4. Những biện pháp đang hoạt động đúng trong phạm vi đã thử

- Academic access có ownership/tenant checks; foreign student/center không đọc hoặc nộp bài người khác trong acceptance.
- PlatformAdmin bị chặn các route academic đã smoke; người dùng tenant không vào platform routes đã thử.
- Server giữ timer/deadline authoritative; start trùng, late draft, submit trùng, draft sau submit, câu foreign/duplicate đã được kiểm tra.
- Competing draft saves trả 200/409 đúng kỳ vọng trong acceptance; không suy rộng kết quả này sang session PATCH vốn trả 500.
- Batch submit, required reasoning, normalized scoring, void full credit, worker fallback đạt acceptance.
- Preview token import gắn actor/tenant; học sinh bị chặn và confirm lại tuần tự bị chặn.
- Đọc notification dispatcher/lease thấy revalidate audience/center và fencing; chưa tìm được fault cụ thể nên không thêm lỗi giả định.

## 5. Bộ kiểm tra nghiệm thu tiếp theo

| Nhóm | Kịch bản cần chạy | Tiêu chí |
|---|---|---|
| Draft/browser | Reload mạng chậm, đổi câu, offline/reconnect, hai tab, hết giờ, ảnh/LaTeX | Không ghi dữ liệu chưa hydrate đè draft; conflict có UI rõ; server là nguồn cuối |
| Lộ trình | Hai lớp cùng môn, curriculum chuyển/ended, hai replica replan | Scope nhất quán; tối đa active path theo chính sách; không trạng thái dở |
| Buổi học | Bắt đầu đúng topic/questions, hoàn thành có/không attempt, hai PATCH | Transition rõ; progress/checkpoint/items đồng bộ; conflict 409 |
| Câu hỏi | Archive/delete sau recommendation hoặc sau preview | Revalidate tại sử dụng; không lộ Draft, không dẫn vào câu không nộp được |
| Import | CSV multiline/BOM/quotes, XLSX inline/shared/multisheet, boolean sai, decimal/length biên | Preview và confirm cùng invariant; lỗi đúng hàng; transaction không lưu dở |
| RBAC/lifecycle | Đổi quyền/ngừng học giữa start và submit; teacher chuyển lớp | Quyền được kiểm tra tại hành động; không vượt tenant; giữ lịch sử đúng |
| Reports | Pending/provisional/final/override/void; đổi curriculum; hai giáo viên | Metric có nghĩa rõ; CSV/XLSX/UI nhất quán; ghi chú đúng author |
| Upload | Qua proxy, sát ngưỡng, token replay, storage fail/cleanup cạnh tranh | Cap nhất quán; không gắn ảnh sai tenant/attempt; không mất ảnh đã submit |
| Workers | Crash giữa dequeue/save, lease expiry, retry, provider outage | Idempotency và fencing; trạng thái terminal rõ; không nhân đôi side effect |
| Deploy | Upgrade DB cũ, rollback/restore, load nhiều người, scan image/dependencies | Có bằng chứng phục hồi, tài nguyên đủ, không lỗi schema/timeout hàng loạt |

## 6. Các giới hạn chưa thể kết luận

Chưa kiểm thử mọi endpoint và tất cả tổ hợp dữ liệu; chưa làm penetration test, load test nhiều người, nhiều API replica, mobile/browser matrix, provider AI thật, backup/restore, upgrade database đang có dữ liệu thực, email/push bên ngoài hay các thao tác vận hành phá hủy dữ liệu. Các rủi ro source-only đã ghi riêng để tránh hiểu là lỗi runtime đều đã tái hiện.

**Ưu tiên phát hành:** sửa F01 trước, sau đó các lỗi chọn câu/luồng buổi và validation/concurrency; chạy lại regression trên MySQL + browser. Tiếp theo chốt scope lộ trình/metric, transaction/cache/resource limits và deployment caps. Hai báo cáo là cơ sở review và sửa lỗi, không phải chứng nhận hệ thống đã sạch lỗi.
