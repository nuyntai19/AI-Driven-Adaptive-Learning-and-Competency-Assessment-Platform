# Báo cáo mở rộng vận hành Platform Admin — 11/10/2026

Milestone: PLATFORM-OPS-OBS-01. Người dùng phê duyệt “oke làm đi”; phạm vi khóa trong [SPEC](../plans/PLATFORM-OPERATIONS-IMPLEMENTATION-SPEC-2026-10-11.md). HEAD nền khi kiểm chứng là `61ef80a7830772c6aec10d3cd0053e61d313cfe7`; lúc hoàn tất kiểm chứng chưa stage, commit hoặc push. Sau đó người dùng yêu cầu “oke push lên trước đi”: đóng gói các thay đổi thông báo/nhật ký/giao diện/vận hành hiện tại trên nhánh `student/answer`, ghi chú đọc báo cáo này trong commit. Credential, ảnh riêng, dump database và dữ liệu Docker trong `storage/` không thuộc gói Git.

Trạng thái: **COMPLETED-v1 / VERIFIED** trong phạm vi SPEC; đã rollout localhost và kiểm chứng bằng Chrome PlatformAdmin thật. Không coi các phần roadmap chưa triển khai bên dưới là đã hoàn thành hoặc thay thế human acceptance.

## 1. Chức năng đã xây dựng

| Trang Admin | Chức năng thực tế | Không làm trong bản này |
| --- | --- | --- |
| Vận hành hệ thống | Lọc trung tâm; đếm Pending/Processing/Completed/FallbackCompleted/FailedTerminal; tuổi job chờ lâu nhất; lease hết hạn; 20 job metadata gần nhất; truy vấn DB và heartbeat thật của ba worker; điều kiện cảnh báo | Không đọc bài/điểm/ảnh, không chạy lại AI, không dựng ETA khi thiếu quota; chưa có throughput lịch sử hoặc p50/p95 |
| Sử dụng AI | Mỗi provider invocation có UUID; Gemini/Groq; tách đọc ảnh/chấm/lộ trình; token nullable; thời gian provider; cửa sổ 24h/7/30 ngày; tổng hợp SQL theo ngày/model/stage; biểu đồ ngày; độ bao phủ token/giá và số metadata bị bỏ lỡ | Không hồi dựng token cũ; không tính theo số học sinh; không có hóa đơn hoặc phí theo từng trung tâm; không gọi AI thật để tạo số liệu |
| Lưu trữ & sao lưu | Snapshot 5 phút; số lượng/byte logic ảnh nháp và ảnh câu hỏi theo trung tâm; dung lượng trống volume API; lần backup thành công/lỗi, kích thước, checksum; lần thử phục hồi và checksum gắn đúng bản backup | Không xóa file, không restore đè, không đo toàn ổ C/D hoặc Docker VHD; chưa đo DB/index/temp/orphan; chưa tạo scheduler backup |

Ba mục mới trong sidebar, bố cục dùng chiều rộng khả dụng, typography Admin đã tăng ở zoom 100%, sáng/tối và bảng cuộn ngang trong khung ở màn hình nhỏ. Polling hàng đợi 30 giây, AI/storage 60 giây; không polling nền khi tab bị ẩn.

### Cảnh báo

Điều kiện: backlog, lỗi kết thúc tăng trong một giờ gần nhất, thiếu heartbeat AI/hậu xử lý, volume API ít chỗ, backup lỗi/cũ. Mặc định phải kéo dài 120 giây; một thông báo cho mỗi episode và một thông báo phục hồi, qua outbox hiện có của root. Lỗi lịch sử cũ không tự thành cảnh báo mới. Không gửi chỉ vì chưa từng có backup metadata. API/DB chết hoàn toàn cần giám sát ngoài hệ thống, không thể tự gửi in-app.

### Giá và số liệu thiếu

Giá mặc định **chưa xác minh**, không phải 0 USD. Cấu hình `PLATFORM_AI_PRICES_JSON` theo provider/model/pool, Free/Paid, thời gian hiệu lực, phiên bản và URL nguồn HTTPS. Không tính lại tiền lịch sử bằng bảng giá mới. Cached được trừ khỏi input trước khi tính riêng; thinking áp dụng quy tắc cấu hình; total chỉ dùng làm tổng/đối chiếu, không cộng kép. Bản này là ước tính theo rate profile, không mô phỏng mọi bậc giá/chiết khấu/hóa đơn của provider; chỉ bật profile sau khi người vận hành xác minh nó phù hợp gói đang dùng.

Collector dùng buffer RAM giới hạn 1.000, producer không chặn chấm; thử ghi SQL lại khi lỗi, có counter dropped. Crash có thể mất phần chưa flush; heartbeat và dropped khởi đầu lại khi API khởi động. Sổ đã ghi bền vững trong SQL. Một call có thể gom nhiều câu/học sinh, không quy tiền theo từng học sinh.

## 2. Quyền và bảo toàn dữ liệu

- Ba quyền riêng: `platform.operations.read`, `platform.ai_usage.read`, `platform.storage.read`; sensitive, non-delegable, chỉ PlatformAdmin của tenant PLATFORM. Controller kiểm tra policy; service kiểm tra role/root/user và tenant thực của DbContext; collector cross-center phải ở root scope.
- Ba API chỉ GET: `/api/v1/platform/operations`, `/api/v1/platform/ai-usage`, `/api/v1/platform/storage`. Không endpoint mutation mới.
- Job projection chỉ mã job kỹ thuật, center, trạng thái/retry/time và mã lỗi allow-list; không AttemptId/StudentId, raw error, prompt/response, key, ảnh hoặc điểm. Ledger không có liên kết học sinh/bài làm.
- Dung lượng ảnh câu hỏi dùng `SUM(LENGTH(...))` tại MySQL; API Admin không lấy blob. Các aggregate xuyên center có tập center rõ ràng, loại root và center đã xóa.
- Catalog code hiện có 96 permissions / 116 mappings. Chuỗi migration lịch sử có 146 applicability rows (110 trước thông báo + 33 thông báo + 3 mới); không xóa 30 mapping lịch sử ngoài scope. Mapping không đồng nghĩa role đã được cấp. Kiểm thử SQL kiểm tra riêng ba mapping mới chỉ là PlatformAdmin; quyền mặc định Teacher/Student/Manager không mở rộng bởi milestone này.
- Migration additive `20261011085301_AddPlatformOperationalMonitoring`: thêm `platform_ai_calls`, `platform_ops_snapshots`, `platform_ops_alerts` và ba permission/mapping. Không sửa bảng học thuật. Role bootstrap cấp ba quyền Admin mới có thể thu hồi phiên cũ bằng auth-version; nếu cần, đăng nhập lại, không đổi mật khẩu.

## 3. Kiểm thử và bằng chứng

| Gate | Kết quả đã xác nhận |
| --- | --- |
| Backend hồi quy | 4.138 pass, 0 fail, 102 skip / 4.240 tổng; 102 kiểm thử DB có điều kiện chưa chạy trong lượt không cấu hình SQL |
| Unit riêng vận hành | 20 pass; phạm vi actor/root/DbContext, giá thiếu/chồng lấn/cached/thinking, dedup ledger, buffer, cache và cảnh báo episode/phục hồi |
| MySQL thật | 2 pass / 0 fail / 0 skip; migration/catalog lịch sử, hai trung tâm, lọc một trung tâm, `LENGTH` 137 byte, hai provider-call metadata/dedup, tổng 260 token/latency, mapping mới chỉ PlatformAdmin; không pending model changes |
| Frontend đầy đủ | 688 pass, 0 fail, 0 skip |
| Lint | `npx eslint . --quiet` exit 0 |
| Frontend production | Typecheck/Vite build thành công, 975 modules; route vận hành code-split; warning chunk MathLive có sẵn ngoài scope |
| Bundle budget | 3 pass |
| API Docker | Build/publish cuối thành công, 0 warnings/errors; image API `6def7d1c8120c466ef5859655426fd599cebf633428b07a80445950e1974f723` |
| Backup/restore | Backup trước rollout 3.485.665 byte; restore vào schema UUID cô lập đã pass, 19 bảng bảo vệ khớp chính xác |
| Rollout/live comparison/UI | 48 migrations; 3 quyền/root grants, 0 grant sang non-root; collector có snapshot hai trung tâm; ba API không đăng nhập đều 401; 19 bảng đối chiếu khớp, identity user không đổi, đúng một hàng auth/session Admin tăng phiên bản; desktop/mobile/sáng/tối không tràn ngang trang |

Backup tên `2026-10-11-before-platform-ops`, SHA256 `acfe6b0ac8bee81342cf915435da44abb91132100d3ec734fe725fba543cede2`. Bản sao/checkpoint/evidence riêng nằm trong `storage/`, đã được Git ignore; report không chứa dump, credential hoặc payload học sinh. Script phục hồi không chấp nhận dump chuyển database và chỉ drop UUID schema chính nó tạo. Không restore live.

Các lỗi kiểm thử ban đầu đã được phân biệt: testhost/MSBuild trong sandbox bị hạn chế IPC, chạy lại bên ngoài sandbox đạt; vài assertion số quyền cũ được cập nhật theo catalog mới, không nới kiểm tra privacy. Gate MySQL còn phát hiện số applicability row lịch sử khác catalog; assertion được sửa có giải thích và bổ sung kiểm tra ba mapping root-only, không sửa dữ liệu cũ để làm test pass.

Rollout đầu phát hiện Oracle MySQL EF không xử lý đúng parameter UUID-array khi nhiều trung tâm. Đã sửa queue/storage/alert dùng subquery center đủ điều kiện trong SQL; flush ledger dùng scalar UUID predicate đã có, tối đa 100 calls. Ca SQL được mở rộng lên hai trung tâm/hai calls và pass; collector live sau bản sửa không còn báo lỗi, snapshot được ghi. Không dùng fixture một trung tâm làm bằng chứng phủ trường hợp này nữa.

Một lượt full regression chạy cùng build/SQL báo lỗi hai ca thông báo cũ; chạy lại nhóm đó đạt 6/6 và lượt full cuối đạt 4.138/0/102, không thay assertion hay code thông báo để né lỗi. Nguyên nhân tính không ổn định của lượt trung gian chưa được xác định; ghi lại, không khẳng định chỉ do tải CPU hoặc rằng mọi phép thử luôn ổn định.

### Số đo live và giao diện

- Zoom100% / DPR1, desktop1920px: nội dung storage x=344, rộng1.537px, gutter24px sau sidebar320px. Client/scroll-width trang cùng1.905px (15px scrollbar), không tràn ngang. Tiêu đề37,5px; typography scoped Admin giữ nguyên.
- Mobile390px (client375px): ba trang không tràn ngang; storage table cuộn trong khung341px với nội dung800px. Drawer có đủ ba menu mới và dẫn đến đúng trang; viewport đã reset, theme trả sáng khi bàn giao.
- All-center queue: 212 job hoàn thành, trong đó2 fallback; Pending/Processing/FailedTerminal đều0 tại thời điểm xem. Lọc center B cho0, không lẫn212 của center A. Ba heartbeat hoạt động; không gọi AI mới chỉ để kiểm chứng.
- Snapshot09:33:35 UTC: hai trung tâm; tổng8 attachment,620.461 byte; ba ảnh câu hỏi183.706 byte. Volume API báo free1.014.839.185.408 / total1.081.101.176.832 byte; đây là filesystem volume Docker, có thể thin-provisioned, **không chứng minh ổ C vật lý còn lượng đó**. Chưa có giám sát free-space host.
- AI ledger live0 call mới: màn hình ghi rõ chưa có token/latency, giá chưa xác minh, không dùng token/điểm lịch sử để điền biểu đồ. Nhánh biểu đồ có dữ liệu được kiểm tra bằng helper/SQL synthetic, chưa được kiểm chứng bằng provider call thật trong lượt này.
- Backup thành công, checksum đúng bản đã rehearsal; cả UI và manifest xác nhận. Cảnh báo episode/recovery được kiểm tra unit qua outbox synthetic, không cố tình tạo lỗi/saturation hoặc gửi cảnh báo thử cho người thật.
- Evidence private: `storage/verification/notification-checks-1791711138219.log`, các JSON inventory/restore và PNG trong `storage/verification/platform-operations-ui/`. Màn hình sáng/tối được xem bằng skill computer-use; không nộp bài, đổi điểm, chạy lại job, gửi hoặc xóa thông báo.

## 4. File trong phạm vi milestone này

Đây không phải danh sách toàn bộ worktree: phần thông báo/nhật ký/nhập công thức từ các lượt trước vẫn được giữ riêng, không ghi nhận thành thay đổi mới của giám sát.

- DAL: `PlatformOperations/OperationalModels.cs`; `Persistence/Configurations/PlatformOperations/OperationalConfigurations.cs`; migration mới và Designer; `EduTwinDbContext.cs`, model snapshot, `Seeding/AuthorizationPermissionCatalog.cs`.
- Contracts: `Platform/PlatformOperationsDtos.cs`.
- BLL: `Platform/Operations/{OperationalTelemetry,PlatformOperationsService,OperationsMaintenance}.cs`; `Platform/DependencyInjection.cs`.
- API: `Controllers/PlatformOperationsController.cs`; `PlatformOperations/{OperationalHostProbe,OperationsBackgroundService}.cs`; `Program.cs`; instrumentation trong `GoogleGenAIGenerateContentClient`, `GroqGenerateContentClient`, `GeminiVisualEvidenceInspector`, `ReasoningBatchExecutor`, `GeminiAIService`, `GeminiLearningPathPlanEnricher`; heartbeat trong `AIAnalysisJobBackgroundService`, `AIStudentPostProcessingBackgroundService`, `Notifications/NotificationBackgroundService`.
- Frontend: `types/platformOperations.ts`, `api/platformOperationsApi.ts`, `utils/platformOperationsPresentation.ts`, `pages/PlatformOperationsPages.tsx`, `tests/platformOperations.test.ts`; wiring trong `App.tsx`, `auth/permissions.ts`, `layouts/PlatformLayout.tsx`.
- Tests backend: `Platform/PlatformOperationsTests.cs`, `Notifications/PlatformOperationsMySqlTests.cs`; assertion/catalog/whitelist trong `IdentityAndTenancy/{AuthorizationBootstrapperTests,ListPermissionsUseCaseTests,GlobalQueryFilterTests}.cs`; test catalog migration trong `Recommendations/RecommendationMySqlConcurrencyTests.cs`.
- Ops/config: `scripts/ops/platform_backup_status.cjs`, `platform_operations_inventory.cjs`, `docker-compose.yml`, `.env.example`. Reuse `run_notification_checks.cjs`, `verify_notification_backup.cjs` và helper backup có sẵn; mount backup manifest read-only.
- Docs: proposal/SPEC/report này; amendments trong `CONSTITUTION`, `PROJECT_REQUIREMENTS`, `DATABASE_SCHEMA`, `API_CONTRACTS`, `UI_UX_SPEC`, `MASTER_PLAN`, `PROJECT_TRACKING`.

## 5. Giới hạn còn lại / bước sau cần duyệt riêng

1. Chưa load-test 1.000 học sinh đồng thời. Index, SQL projection, cache 30 giây, buffer/batch giới hạn và polling đã có; đó không phải chứng nhận tải. Cần kịch bản provider giả, quota thật và ngân sách trước test provider thật.
2. Chưa có rollup/retention ledger, p50/p95, throughput lịch sử, ngân sách/over-budget alert hoặc custom date range. Không purge tự động; cần chính sách đã duyệt trước vận hành dài hạn.
3. Chi phí chưa cấu hình profile đã xác minh; không có lịch sử trước rollout hoặc billing theo trung tâm. Model lưu là model được yêu cầu, không cam kết model-version thực phục vụ.
4. Storage là dung lượng logic/snapshot hiện tại, không growth forecast hoặc quét file vật lý. Backup thủ công có bằng chứng restore, chưa tự động và chưa thay thế chiến lược off-machine/off-site.
5. Không có các thao tác nguy hiểm trên trang giám sát. Các quyền retry AI/xóa dữ liệu/khôi phục/đổi billing vẫn nằm ngoài scope.

Rollback an toàn: quay về image trước trong thời gian bảo trì nếu cần, giữ schema additive và backup; không chạy migration Down hoặc restore đè live để hoàn tác UI. Không tự thay chính sách quyền, điểm hoặc chứng cứ học tập.
