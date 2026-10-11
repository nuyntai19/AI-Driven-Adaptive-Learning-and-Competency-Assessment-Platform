# Hoàn thiện thông báo và nhật ký — 10/10/2026

Baseline `61ef80a7830772c6aec10d3cd0053e61d313cfe7`, nhánh `student/answer`. **Đang kiểm chứng, chưa nghiệm thu toàn bộ, không commit/push.** Báo cáo này bổ sung, không ghi đè kết quả lịch sử của NF-01/NF-03a.

Checkpoint UX sau ảnh 11/10: [bảy điểm giao diện và nhập toán](NOTIFICATIONS-UX-REPORT-2026-10-11.md), backend rebuild/full **4118 pass/101 skip**, frontend cuối **671 pass**, SQL scope/legacy **2 pass**, build/lint/bundle cuối đạt. UI bốn tác nhân đã kiểm trên runtime mới: font, deep-link cùng trang, chuỗi student01 receipt/phản hồi/thu hồi/tombstone, input Chèn công thức, metadata Manager/Platform và mobile. Platform chỉ mở form soạn/phạm vi, không gửi mới. Còn ca offline/revoke browser và reviewer con người; không thay trạng thái gate cũ thành VERIFIED bằng suy luận. So hàng cũ sau test 10 do người dùng tạo: **0 hàng cũ mất/đổi**, 14 bảng khớp SHA, bốn bảng chỉ thêm mỗi bảng một hàng thuộc test 10. Không tuyên bố toàn database vẫn giống byte với checkpoint trước test mới.

## Chức năng trong source

- Gửi có loại/ưu tiên, preview phạm vi và chống gửi trùng; Đã nhận/Đã gửi, bộ lọc, số chưa đọc thật. Popover dùng portal, không còn overlay tối bị cắt bởi header.
- Xác nhận nhận độc lập với đã đọc; ẩn khỏi inbox; một phản hồi riêng nếu người gửi cho phép. Thu hồi giữ dấu vết; đính chính là bản gửi mới, không sửa nội dung gốc và không mở rộng tập người nhận gốc.
- Observer nghiệp vụ stage event trong cùng `SaveChangesAsync`: giao/đóng/cập nhật bài, nộp toàn bài, review/báo lỗi đề, chốt/mở lại/chốt lại, hủy câu đúng assignment, vòng đời lớp/thành viên/phân công, ledger giáo trình và metadata quản trị.
- AI chỉ stage marker nội bộ; worker kiểm tra toàn bộ câu/attempt/job có hiệu lực rồi tạo hai thông báo tổng hợp riêng cho học sinh và giáo viên. Phân biệt success/fallback/failed/skipped, không gọi đề xuất AI là điểm chốt hoặc nói lộ trình đã sẵn sàng. Job retry tái dùng ID vẫn có generation riêng; generation được kiểm tra lại dưới khóa khi phân phối. Marker cùng bài được kết thúc khi đã tổng hợp để không xếp 100 marker trước inbox.
- Nhật ký đọc từ nguồn lịch sử thật và event mới, không tạo lịch sử giả hoặc gửi hồi tố. Student own; Teacher theo lớp hiện phụ trách; Manager metadata; Platform quản trị nền tảng. Không trả raw audit JSON, ảnh, prompt, đáp án hoặc điểm học thuật cho Manager/Admin.
- Trang vận hành Manager/Admin: trạng thái queue, số đã phân phối/suppress, lỗi an toàn, oldest pending. Re-drive chỉ DeadLetter với version/lý do/clientActionId, có audit; không xóa event/delivery hoặc reset Pending/Processing/Completed.
- Theo lựa chọn người dùng 11/10: nhập tự do nhưng kiểm tra dấu hiệu nhạy cảm ở server cho preview/send/reply/reason. Kênh Manager ↔ Admin chặn thêm dấu hiệu bài làm/điểm cá nhân/định danh và liên lạc; mọi kênh chặn credential/link/path/HTML. Không ghi payload bị chặn, không tự sửa nội dung và không dùng AI. Đây là heuristic có thể chặn nhầm/bỏ sót, không bảo đảm DLP tuyệt đối.
- Bộ lọc ngày theo ngày địa phương, đổi sang UTC khi gửi API; không lệch ngày do dùng trực tiếp chuỗi UTC.

Các phần email/SMS/browser push, presence, chat tự do, nhắc tự động và purge production không thuộc phạm vi. Không đổi provider/model/quota/hàng đợi chấm AI, không sửa bài làm hoặc điểm.

## Quyền/schema

Catalog hiện tại: 93 permission, 113 applicability mapping (baseline 71/80). Chỉ default system roles active được additive backfill; custom/revoked role không tự nhận quyền. Platform capability non-delegable; re-drive sensitive.

Migrations mới sau checkpoint NF-03a:

1. `20261010092545_AddNotificationLifecycle`: action/reply bất biến, receipt/lifecycle và quyền.
2. `20261010100225_AddNotificationBusinessContextAndActivity`: context JSON nullable allow-list và quyền nhật ký; không backfill event.
3. `20261010101422_AddNotificationOperations`: quyền vận hành; không đổi bảng học thuật.

Runtime local đã rollout NF-02/NF-03b/NF-04/NF-05 ngày 11/10: hai migration cuối được áp dụng, tổng migration 45 → 47. Chưa nghiệm thu toàn UI bốn tác nhân hoặc production.

## Bằng chứng kiểm thử hiện tại

| Phạm vi | Kết quả / giới hạn |
| --- | --- |
| Backend build | Build `-m:1` đạt 0 warning/0 error, gồm atomicity và log/metric worker |
| Frontend full | **663 pass, 0 fail, 0 skip**, gồm ngày địa phương và hiển thị lỗi nội dung an toàn |
| TypeScript/Vite | Đạt; cảnh báo bundle mathlive hiện có, không phải lỗi notification |
| Lint phần thay đổi | 0 error/0 warning |
| Backend full mới nhất | **4117 pass/0 fail/101 integration skip**, tổng 4218, `notification-regression-post-ui.trx`; gồm 22 ca content policy + 3 MVC lỗi thật, JSON lịch sử, void/handover và suppress final-review version cũ. 101 skip gồm 22 ca notification MySQL (chạy riêng) và 79 integration khác; không tính skip là SQL pass |
| MySQL đầy đủ không benchmark | **19 pass/0 fail/0 skip**, một lượt 8 phút 42 giây, log `notification-checks-1791653111274.log`: foundation/lifecycle/business/AI/activity/operations, upgrade/index/EXPLAIN, rollback grade/void và suppress final-review version cũ. Sau thêm content policy: **7 pass/0 fail/0 skip** SQL manual/relay/lifecycle/scope, log `notification-checks-1791654059551.log`; chứng minh payload bị chặn không tạo message/reply/action/outbox. Source cuối chạy lại relay **1 pass**, log `notification-checks-1791654252440.log` |
| Bundle budget | **3 pass/0 fail**, trên dist build mới nhất; initial preload/deferred routes vẫn trong ngưỡng |
| Benchmark | Lượt độc lập **2 pass/0 fail**, log `notification-checks-1791650360424.log`. Fan-out 100 event/200 delivery, 8 worker: dispatch p50 280,48 ms/p95 506,43 ms; lag harness p50 5553,77 ms/p95 7320,24 ms; 7 command/event. Persistence 50/100 câu, hai học sinh, 20 mẫu mỗi nhánh A/B + warmup đạt ngưỡng đã khóa. 0 request Gemini. Scheduler production vẫn một event/tenant/chu kỳ 5 giây, không dùng harness để hứa lag production |
| UI thật | Teacher Math nhật ký 26 hoạt động đã tải sau sửa collation, responsive không tràn ngang; preview synthetic chặn đúng và hiển thị hướng dẫn sau sửa traceId. student01 đã đọc/xác nhận/phản hồi riêng bản tiếp nối: badge 1 → 0, ack 01:33:35 11/10, một phản hồi được lưu và form phản hồi đóng. Nhật ký student01 75 hoạt động/4 trang; source giáo trình 4 mục, ngày 10/10 có 6 mục. Còn kiểm tra Teacher nhận phản hồi/thu hồi và UI Manager/Platform; các thông báo người dùng tạo khác giữ nguyên |

Lỗi môi trường testhost bị hạn chế loopback đã được phân biệt với lỗi ứng dụng; lượt kiểm chứng có kết nối nội bộ được phép mới được dùng làm kết quả. Log MySQL đã redact tại `storage/verification/notification-checks-1791648672288.log` (ignored).

Docker API image build mới nhất thành công (Release 0 warning/0 error); API context chỉ 16,09 MB, không chứa backup VHDX. Build web `npm ci` báo 11 vulnerability (2 moderate/9 high); package/package-lock không thay đổi trong feature notification. Đây là cảnh báo dependency cần rà soát riêng, không được dùng kết quả test chức năng để tuyên bố đã xử lý bảo mật thư viện. Không tự chạy `npm audit fix` hoặc nâng package trong hạng mục này. Cảnh báo chunk mathlive cũng được giữ tách khỏi lỗi chức năng.

Các thiếu sót phát hiện và xử lý: fixture thiếu `EstimatedLearningMinutes`/`AssignmentQuestion.Points`; MySQL provider không implicit rollback đủ batch có observer nên mở transaction khi chưa có (transaction caller giữ nguyên quyền sở hữu), kiểm chứng lỗi event rollback cả assignment/submission; biểu thức Select+Guid Contains trong assertion test được chuyển thành đối chiếu bounded trên memory; DI TimeProvider; policy ở method API; JSON lịch sử unknown không bị gán Pending. Log/metric worker chỉ IDs/count/state/error code/duration/lag, không body/recipient/provider data.

## Backup và bảo toàn dữ liệu

Backup trước rollout NF-02/NF-04/NF-05: `storage/backups/2026-10-10-before-notification-business.sql`, SHA256 `969adb83c0cf18c812675f8a92b2fb83965e36498d0644559da8da3fc6d76b0f`, proof JSON cùng tên. Không đưa backup vào Git.

Counts: assignments 10; questions trong assignment 62; targets 26; progress 26; attempts 212; analyses 212; attachments 8; curricula 8; curriculum nodes 87; curriculum classes 4; twins 4; goals 20; students 10; users 18; class members 30; knowledge nodes 96; knowledge twins 15; twin history 380; application ledger 7. Sau rollout đối chiếu SHA, không chỉ count: xem kết quả bên dưới.

Diễn tập phục hồi thực sự bằng `verify_notification_backup.cjs`: chỉ schema UUID cô lập, không phục hồi đè database thật; **19/19 bảng khớp count và SHA**, `restoredExactly=true`, schema diễn tập đã dọn. Không in nội dung backup, đáp án hay credential.

### Rollout local 2026-10-11 và kiểm chứng sau migration

- Backup mới ngay trước rollout giữ cả 5 message/1 action đính chính/5 event/6 delivery hiện có: `storage/backups/2026-10-11-before-notification-rollout.sql`, SHA256 `0a36e0a9cb2e43593cc0fb025e7d633c2cff53000dcc3c4f334523a0ea388bed`. Backup cũ/VHDX vẫn được giữ, không ghi đè.
- API/web build thành công và recreate riêng hai service bằng `up -d --no-deps`; MySQL/adminer/volumes giữ nguyên. API liveness/readiness HTTP 200 Healthy, MySQL healthy, FK checks=1, orphan analyses/evidence/history/path/knowledge=0.
- API image `25f24be42831f3c1e2b12f5e48d45f27ff692b2903b2f021cee4f6ff1802b3d9`; web image `9c2567d5a20dad183781a9444b80d1e5231dbf6b88f43f5eb07631d4ef881c0f`, cả hai container khớp latest image. HTML runtime khớp đúng web image build (`07728de742f6af44b9a16c25b3a3fbc6b4d6c622348c57b31059ad4661b1b949`). Dist Windows có hash khác build Linux; không dùng kiểm tra đó để báo nhầm deploy thất bại hoặc coi hai artifact giống byte.
- 47 migration, 93 permission/113 mapping, resource_context mới=1. Các notification cũ giữ 5 message/1 action/0 reply/5 event/6 delivery và 5 dispatch Completed; không phát lịch sử học thuật hồi tố.
- Restore baseline mới vào schema UUID riêng và compare-live: `restoredExactly=true`, `academicChangedTables=[]`, `userIdentityDataUnchanged=true`, `userSessionChangesExpected=true`, `changedUserSessionRows=18`, `liveProtectedDataUnchanged=true`. 18 user chỉ tăng AuthVersion/RowVersion đúng 1 và UpdatedAt do additive default-role backfill; identity/password/status/tenant và các field còn lại khớp. Access token cũ hết hiệu lực đúng policy; Teacher Math có refresh cookie hợp lệ đã làm mới phiên qua luồng bình thường. Việc chuyển tài khoản kiểm thử vẫn do con người đăng nhập.
- Chưa tạo attempt, chấm lại/chốt/void hoặc đổi lớp/giáo trình thật để kiểm thử. Chuỗi thử thông báo riêng student01 còn tiếp tục phản hồi và thu hồi theo phê duyệt.
- UI Teacher Math sau reload xác nhận sidebar Nhật ký đã có và phiên được refresh bình thường, nhưng GET activity trả 500. Log xác nhận `Illegal mix of collations ... operation 'like'`: audit cũ `utf8mb4_unicode_ci`, GUID-to-string CAST kế thừa `utf8mb4_0900_ai_ci`. Source sửa riêng collation của biểu thức prefix theo [hướng dẫn EF Core](https://learn.microsoft.com/en-us/ef/core/miscellaneous/collations-and-case-sensitivity), không ALTER dữ liệu thật hoặc chuyển lọc ownership sang memory. Thêm SQL fixture legacy column để tái hiện query cũ lỗi và kiểm tra query mới/scope/detail; đang chạy kiểm chứng và build lại API. Không coi UI Teacher đã đạt trước kết quả này.
- Bản sửa query đạt build 0 warning/error, unit activity/content **30 pass**, SQL legacy collation + four-actor scope **2 pass/0 fail/0 skip**, log `notification-checks-1791655868880.log`. Test xác nhận query cũ thật sự ném lỗi collation, query mới list/detail đạt, giáo viên ngoài ownership không nhận dữ liệu. Đang build lại API để kiểm tra Teacher Math lần cuối; số test toàn bộ trước đó vẫn 4114 pass, ca SQL mới được ghi riêng, không giả đã chạy full lại.
- UI preview synthetic chỉ để xem trước, không gửi: server block payload trước persist nhưng controller `Problem()` thêm `traceId` trùng với MVC factory, gây 500 thay vì 400. Bỏ extension traceId thủ công ở NotificationMessages/Notifications, dùng trace từ factory; thêm MVC regression với DI factory thật cho content 400, invalid filter 400 và missing own item 404. Đang kiểm thử và build lại; số message thật vẫn 5, không tạo bản gửi synthetic.
- **3 ca MVC regression đạt**, dùng DI `AddControllers`/ProblemDetailsFactory thật; không thay handler toàn ứng dụng. Full regression post-UI **4117 pass/0 fail/101 skip**. API cuối đã build/recreate, image `37eac157f999c2c45bf9a06a1bd60e458a3e0287bb504dcd8660539f2bdcc135` khớp latest; readiness HTTP 200 cả direct/proxy.
- UI sau sửa: nhật ký Teacher Math tải 26 hoạt động trên hai trang; screenshot `edutwin-activity-teacher-after-fix.png`. Kiểm tra viewport hẹp thực tế 341 px (override 390 px do browser zoom), document scrollWidth=clientWidth=341; reset về mặc định 1732 px, không tràn ngang. Screenshot mobile giữ riêng; không dùng số requested viewport để giả kích thước thực.
- Preview synthetic sau sửa trả lỗi hạn chế nội dung đúng thông điệp “Nội dung chưa được lưu”, không gửi; screenshot `edutwin-notification-content-rejection.png`. Form synthetic đã hủy. Bản tiếp nối trong chuỗi student01 được gửi 01:21:20 11/10, title “Kiểm chứng thông báo EduTwin – Tiếp tục kiểm chứng”, body giữ nội dung thử cũ, preview đúng Duy Bảo Trịnh, ack/reply=true; đã phân phối 1, đọc/ack=0 và chưa phản hồi. Screenshot `edutwin-notification-continued-proof.png`; metadata giờ 6 message/2 action/6 event/7 delivery/0 reply, 6 dispatch Completed, chỉ thêm phiên bản thử và dấu vết đính chính được duyệt. Chờ con người chuyển tab sang student01 để hoàn tất tương tác.
- Metadata cho thấy 0/5 bản gửi cũ còn có sender-auth-version hiện hành sau additive backfill. Không sửa snapshot/bypass guard để thử phản hồi. Tiếp tục bằng một bản đính chính mới trong đúng chuỗi kiểm chứng đã được duyệt, chỉ student01, mục đích/nội dung thử không đổi; các phiên bản và thông báo người dùng tạo khác được giữ.
- Sau API cuối và bản thử tiếp nối, SHA của **18/18 bảng học thuật vẫn khớp** backup trước rollout (`academicDataUnchanged=true`, `changedTables=[]`). Users không so sánh lại sau các lượt con người đăng nhập; core identity/phiên đã kiểm chứng ngay sau migration trước UI như trên. Không coi thay LastLoginAt hợp lệ là mất dữ liệu hoặc nới phép so sánh identity để che thay đổi không rõ nguồn.

### Kiểm chứng student01 trên runtime cuối — 2026-10-11

- Con người đăng nhập student01; xác nhận tên đăng nhập trong menu hồ sơ, không trích token/cookie hoặc giả quyền. Chuông ban đầu có 1 chưa đọc; mở preview không đánh dấu đã đọc. Bản cũ hiển thị tombstone đính chính, không còn nội dung/CTA gốc.
- Mở chi tiết bản tiếp nối delivery `7` đánh dấu đọc, số chưa đọc 1 → 0; lúc này vẫn chưa xác nhận. Sau đó bấm xác nhận riêng, UI ghi **01:33:35 11/10/2026**. Gửi đúng một phản hồi thử đã được duyệt: “Đã nhận thông báo thử. Đây là phản hồi riêng để kiểm chứng chức năng; không thay đổi bài làm hoặc điểm.” UI hiển thị “Phản hồi riêng của bạn” và không còn form gửi lần hai. Screenshot `edutwin-student-ack-reply-proof.png` lưu local, không thêm vào Git.
- Đổi môn sang Tiếng Anh trong hộp thư vẫn giữ đúng thông báo/receipt/phản hồi, sau đó trả về Toàn bộ. Chưa ẩn, thu hồi hoặc tạo thông báo khác. Teacher nhận notice tổng quát và đọc nội dung phản hồi qua quyền sender vẫn cần xác minh bằng UI phía Teacher.
- Nhật ký riêng tải **75 hoạt động/4 trang**, mở chi tiết audit `38` hiện Pending → Approved, version 3, lý do và nguồn thật; không làm lại bài hoặc chỉnh điểm. Bộ lọc nguồn Giáo trình áp dụng trả **4 mục**; khoảng ngày địa phương 10/10/2026 → 10/10/2026 trả **6 mục**, đều ngày 10/10. Nhập `date` qua automation fill đơn thuần chưa phát sự kiện React; xác minh lại bằng thao tác bàn phím thật rồi mới ghi kết quả, không suy đoán đó là lỗi source. Các bộ lọc được xóa sau thử. Screenshot `edutwin-student-activity-proof.png`, `edutwin-student-activity-date-proof.png`.
- Màn hình hẹp: document clientWidth=scrollWidth=**341 px**, không tràn ngang. Popover trong viewport trình duyệt có gutter, không nền tối modal; dữ liệu receipt sau refetch không còn dấu chưa đọc, Escape đóng và aria-expanded=false. Cache preview có thể xuất hiện trạng thái cũ trong lúc refetch đầu tiên; không dùng snapshot tức thì để kết luận server chưa lưu đọc. Screenshot `edutwin-student-bell-mobile-proof.png`; đã reset viewport về mặc định.
- Lượt này chỉ đọc/ack/phản hồi trong chuỗi thử riêng student01; không sửa source hoặc chạy lại provider AI. Kiểm thử tự động giữ kết quả lượt đã ghi phía trên, không cộng các thao tác UI vào số unit/SQL pass. Chờ chuyển Teacher Math để hoàn tất receipt phía gửi và thu hồi, sau đó kiểm tra Manager/Platform; chưa nghiệm thu toàn bộ UI.
- Kiểm tra SQL chỉ metadata sau lượt này: **6 message/2 action/7 event/8 delivery/1 reply**, **7 dispatch Completed**; delivery `7` có read=1/ack=1/reply=1. Một event/delivery thêm là notice phản hồi cho người gửi, không tạo message thủ công mới. SHA **18/18 bảng học thuật khớp** backup trước rollout, `academicDataUnchanged=true`, `changedTables=[]`; không xuất hàng dữ liệu hoặc so sánh LastLoginAt sau con người đăng nhập.

### Số đo persistence (ms, không phải thời gian AI provider/HTTP end-to-end)

| Ca | Trước p50/p95 | Sau p50/p95 | Command trước/sau |
| --- | --- | --- | --- |
| Nộp 50 câu × 2 học sinh | 179,15 / 383,33 | 214,28 / 256,81 | 3 / 13 |
| Ghi AI terminal, bộ 50 câu | 40,46 / 127,06 | 57,39 / 66,66 | Không đo riêng |
| Nộp 100 câu × 2 học sinh | 333,50 / 428,63 | 372,98 / 527,37 | 5 / 15 |
| Ghi AI terminal, bộ 100 câu | 46,11 / 58,12 | 66,97 / 92,51 | Không đo riêng |

Ngưỡng đã đặt trước: p95 sau ≤ p95 trước + max(10%, 100 ms). Ca 100 câu tăng 98,74 ms, gần ngưỡng; không mô tả là miễn phí/nhanh hơn. Ca 50 câu p95 giảm có thể do nhiễu runtime, không dùng để hứa cải thiện. Hai lỗi fixture (Points và ID tiến độ non-generated) được sửa trước khi lấy số đo hợp lệ.

## Gate còn mở

### Đối chiếu checklist mục 18 của kế hoạch

| Nhóm | Bằng chứng tự động đã chạy | Phần không được tự coi là đã nghiệm thu |
| --- | --- | --- |
| Atomicity | SQL publish/submit/grade/void + lỗi insert outbox; caller-owned rollback giữ nguyên | Không tạo thao tác chốt/void thật để demo trên dữ liệu người dùng |
| Chống trùng | SQL concurrent send/worker, unique delivery, lease/restart; unit read snapshot/first timestamp | Crash harness chứng minh bằng lease/concurrency, không cố làm hỏng container thật |
| AI | Unit 50/100 câu; SQL 50 câu/latest reused-job generation/fallback/failed; benchmark không Gemini | Không chạy lại Test 4–9 thật; không coi AI terminal là điểm đã chốt/lộ trình sẵn sàng |
| Điểm | Approved→Reopened→Approved unit, obsolete final version SQL, void đúng assignment | UI lịch sử có before/after nguồn thật nhưng còn kiểm tra với phiên đăng nhập mới |
| Nhận bài | Submission có skipped; void completion không tạo submission; draft không thuộc hook | Không sửa bài nộp cũ |
| Scope/lịch sử | Ma trận unit, SQL Teacher archived revalidation + four-actor activity; fixture SelectedStudents/handover | Chuỗi UI chỉ student01 được duyệt, không gửi cả lớp hoặc tạo enrollment thật |
| Riêng tư | SQL Manager metadata/Platform scope; content policy unit và SQL relay/reply/reason; không raw payload | Heuristic không bảo đảm phát hiện mọi dữ liệu cá nhân/diễn đạt vòng tránh |
| UI | Tests routes/cache key/poll pause/backoff/portal/ESC/viewport/date/error; cả bốn tác nhân đã xem runtime UX cuối, font/portal/menu/math editor/names/metadata/mobile đạt; Platform mở form soạn đúng kênh quản lý | Chưa gửi mới bằng Manager/Platform hoặc redrive event thật; không đồng nhất test helper với thử offline/role revoke trên browser thật |
| Gửi/thu hồi | SQL immutable correction/retraction/reply/idempotence; preview tamper/revocation unit; student01 phản hồi riêng; Teacher đọc đúng bản gửi, receipt 1/1 và thu hồi; Student tombstone chặn tương tác mới | Chỉ chuỗi student01 được duyệt, không broadcast hoặc thử thu hồi thông báo người dùng khác |
| MySQL/triển khai | Fresh + upgrade từ lifecycle/index/EXPLAIN/restore SQL; append-only/FK/unique/OCC/redrive; local rollout/readiness và compare-live đạt | Không triển khai production hoặc tự ghi reviewer nghiệm thu |
| Hiệu năng | Hai benchmark 50/100 persistence + fan-out, p50/p95/query count, 0 provider requests | Không đo Gemini end-to-end trong gate này và không hứa lag production từ stress harness |

Các hàng trên là phạm vi bằng chứng, không phải dấu xác nhận của reviewer con người. Những lựa chọn retention/ẩn danh production chưa duyệt được giữ ngoài bản đầu, không tự purge.

- Gate tự động cuối UX: backend **4118 pass/101 skip**, frontend **671 pass**, SQL scoped/legacy rebuild **2 pass**. Các lượt feature trước có SQL 19 ca + 7 ca sau content policy + relay source cuối 1 ca + 2 ca collation/four-actor; MVC failure 3 ca; benchmark 2 ca. Bundle dist cuối **3 pass**. Giữ từng lượt riêng, không cộng trùng hoặc tính lượt SQL bị hủy trước dọn ổ là pass.
- Local rollout additive đã đạt readiness/image/migration và compare-live dữ liệu; giữ backup. Không stage/commit/push.
- Đã hoàn tất Teacher nhận phản hồi/thu hồi, Student tombstone sau thu hồi và xem UI Manager/Platform theo tài khoản do người dùng đăng nhập; không broadcast. Platform form chỉ chọn quản lý trung tâm, inbox/operations không lấy dữ liệu tenant A; không gửi thêm hoặc redrive. Các ca offline/role revoke browser chưa được giả VERIFIED từ unit. Chi tiết runtime image/ảnh/font/delta test 10 nằm trong báo cáo UX ngày 11/10.
- Đối chiếu checklist mục 18 kế hoạch: ghi riêng unit/SQL/UI và những ca chưa được chạy, không ghi toàn gate VERIFIED bằng suy luận từ code.
- Retention/ẩn danh production cần quyết định chủ sở hữu; bản đầu không purge. Reviewer con người chưa được chỉ định.

### Sự cố môi trường và dọn ổ C

Docker build bị ngắt RPC EOF/500 trong lượt chuẩn bị image; chưa rollout. Người dùng báo ổ C đầy và yêu cầu dọn rác. Trước dọn: C trống 2,84 GB, cache Docker 22,97 GB. Đã prune chỉ build cache, dọn npm cache 392,58 MB và 17 log Remote Desktop tạm cũ hơn 24 giờ (1157,50 MB); giữ toàn bộ ảnh clipboard, NuGet, nguồn, image/volume và SQL backup. C trống 4,36 GB sau dọn host cache; đĩa ảo chưa tự thu nhỏ.

Hash 19 bảng bảo vệ sau sự cố/dọn cache vẫn khớp backup (`protectedHistoryUnchanged=true`). Người dùng đồng ý tạm dừng Docker để sao lưu VHDX sang D và compact. Bản sao offline `storage/backups/docker-disk-before-compact-20261010.vhdx` đủ 32.325.500.928 byte, SHA256 khớp gốc: `0FB7C2EC577E2B012386407FDCD7B79C92CE64E7EA7886F0BD88A9E477D8C587`; backup này vẫn được giữ trên D, ignored.

Lượt compact đầu chỉ thu hồi 9 MB. Sau TRIM đúng mount ext4 `/mnt/docker-desktop-disk` (không dùng `--all`, không xóa block đang được filesystem dùng), compact offline lần hai đạt exit 0: VHDX còn 10.754.195.456 byte, thu hồi thêm 20,08 GiB; C trống 25,84 GiB lúc đo, 25,76 GiB sau khởi động. Không coi số potential discard gần 1 TB của TRIM là dung lượng host lấy lại. Thực hiện theo [hướng dẫn compact của Microsoft](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/compact-vdisk), [backup Docker](https://docs.docker.com/desktop/settings-and-maintenance/backup-and-restore/) và [hợp đồng fstrim](https://github.com/util-linux/util-linux/blob/master/sys-utils/fstrim.8.adoc).

Khởi động Docker gặp hai nguồn socket runtime cũ lỗi `The file cannot be accessed by the system`, chưa đến bước đọc database. PowerShell không xóa được reparse socket; đã dừng riêng tiến trình Docker bị lỗi và đổi tên giữ lại ba thư mục runtime sau khi xác minh mỗi thư mục chỉ chứa socket 0 byte, không cấu hình/kho bí mật. Docker engine đã khởi động được, bốn container cũ đã start, MySQL healthy; **19/19 bảng khớp count và SHA sau compact/khởi động**, `protectedHistoryUnchanged=true`, không có bảng thay đổi. Không reset factory, đổi setting bảo mật, purge volume hay phục hồi đè database thật.

Sau kiểm thử SQL đã dọn riêng schema UUID bị ngắt từ 10/10 16:46 UTC, xác minh không có users/centers/assignments/attempts và không có kết nối trước DROP. Không dùng wildcard để DROP, không chạm schema/volume thật. SQL fixture mới tự dọn bằng finally/dispose.

Kiểm tra ngay trước rollout 11/10: 19 bảng tiếp tục khớp SHA/count; số schema notification test tồn đọng bằng 0; 5 message/5 event/6 delivery thật được giữ nguyên. Ổ C còn 15,37 GiB lúc build (sau một số test và tải lại image/cache), không đồng nhất số trống sau compact với số trống sau build.

## Phạm vi file (checkpoint trước lượt UX: 96 file text chưa commit)

| Nhóm | Số file | Nội dung |
| --- | --- | --- |
| Root specification/tracking | 7 | API_CONTRACTS, CONSTITUTION, DATABASE_SCHEMA, MASTER_PLAN, PROJECT_REQUIREMENTS, PROJECT_TRACKING, UI_UX_SPEC |
| Build context | 1 | `.dockerignore` loại `storage/`, không đưa backup 30 GB vào ngữ cảnh build |
| docs | 8 | ADR lịch sử, kế hoạch chính, 3 SPEC và 3 báo cáo checkpoint |
| API | 8 | Program, composite policies, 4 controller và 2 file worker/preview protector |
| BLL | 16 | SeedExtensions/DI transaction observer và 15 file module Notifications, gồm content policy |
| Contracts | 3 | Inbox/send/activity/operations DTO |
| DAL | 20 | DbContext/model snapshot/catalog, 6 entity, 1 configuration, 5 migration + 5 Designer |
| Backend tests | 13 | 3 identity tests cập nhật catalog/tenant, 10 file kiểm thử notification, gồm upgrade/index/content policy |
| Web | 18 | App/permissions/4 layout, API/6 component/2 page/types/helpers/tests |
| Ops | 2 | Runner kiểm thử MySQL cô lập/redact và diễn tập phục hồi backup vào schema UUID riêng |

Không có thay đổi provider/AI queue/model/quota, file ảnh nháp/câu hỏi, dữ liệu mẫu học thuật, package hay Docker Compose trong checkpoint này. Backup/log/test artifact ignored, không nằm trong 96 file. Danh sách phần sửa UX tiếp theo nằm trong báo cáo ngày 11/10; không dùng số 96 lịch sử làm số file hiện tại.
