# NF-02…05 — Hoàn thiện kế hoạch thông báo/nhật ký

2026-10-10: người dùng yêu cầu “làm tiếp… hoàn thành toàn bộ trong file md”. Cho phép triển khai các gate còn lại trong kế hoạch, không commit/push. Không mở rộng sang email/push, presence, chat tự do, Teacher–Teacher/Admin–Teacher, nhắc tự động hoặc purge production. Reviewer con người chưa được chỉ định; không tự ghi nghiệm thu.

## NF-03b: bản gửi bất biến và tương tác có ngữ cảnh

- Ba ưu tiên `Normal|Important|Urgent`, loại `Information|Request`. Cùng rate limit cho mọi ưu tiên. Sender có quyền gửi mới chọn được; không cấp bypass cho Khẩn.
- Bản gửi có `RequiresAcknowledgement`, `AllowReply`, `CorrectionOfMessageId` nullable. Preview fingerprint bao gồm mọi tùy chọn. Đính chính phải là bản gửi mới, chủ gửi, cùng tenant nguồn, chỉ một thay thế cho mỗi bản, phạm vi mới nằm trong snapshot người nhận gốc và vẫn hợp lệ hiện tại. Không sửa title/body bản gốc.
- Một action append-only cho mỗi message (`Retracted|Corrected`), lý do 1…500 ký tự, ClientActionId GUID chống retry. Không cho thu hồi/đính chính bản đã có action; có thể tiếp tục chuỗi qua bản đính chính mới. Không hard delete. Thu hồi không lấy lại được nội dung đã đọc/chụp.
- Delivery có `AcknowledgedAt`, `HiddenAt` nullable, chỉ thiết lập lần đầu; không tự đánh dấu đọc khi xác nhận. Ẩn chỉ khỏi list/count, detail của chính người nhận vẫn được xem dấu vết. Bản thu hồi/được đính chính chỉ trả tombstone, không body/hành động cũ.
- Một phản hồi plain text 1…2000 ký tự mỗi delivery nếu sender cho phép, không attachments/link/chat. Revalidate sender và audience khi phản hồi; nội dung chỉ chủ gửi và người phản hồi xem, không roster lớp. Response là append-only; đọc/xác nhận/ẩn/phản hồi không thay đổi nghiệp vụ.
- Quyền tách `notifications.messages.retract_own` (Teacher/Manager), `platform.notifications.retract_own`; `notifications.inbox.acknowledge_own`, `.reply_own` (Student/Teacher/Manager), platform tương ứng. Quyền platform non-delegable; backfill chỉ active system roles như NF-01.
- API: POST `/notifications/sent/{id}/retract` `{clientActionId,reason}`; POST `/notifications/{id}/acknowledge`, `/hide`; POST `/notifications/{id}/reply` `{clientReplyId,body}`; GET `/notifications/sent/{id}/replies` paged own sender. Đính chính qua preview/send với `correctionOfMessageId`, `correctionReason`. 404 foreign, 409 superseded/conflict, 400 invalid, 403 thiếu scope/quyền. Mutation có rate limit server hiện có.
- Inbox bổ sung filter loại/ưu tiên/thời gian/cần xác nhận; loại/ưu tiên allow-list, UTC bounds, pages ≤100. UI không coi ack là “đã xử lý”. Sent hiển thị receipt tổng hợp và lifecycle; không cho recipient xem ai khác đã đọc.

## NF-02/NF-04: hợp đồng tích hợp

Các hooks và recipient policy theo mục 3/12/13/16 kế hoạch gốc. Stage cùng transaction; không notification ngoài commit, không bản nháp/thao tác bị chặn. AI readiness theo tập attempt/job hiệu lực và generation, không theo job cuối. Adapter activity từ history có nguồn, allow-list actor, không copy raw audit JSON. Phân quyền activity riêng own/scoped/administrative; Manager metadata, Platform vận hành. Không phát lại lịch sử cũ.

### Điểm tích hợp NF-02 được khóa trước code

- `NotificationBusinessInterceptor` chỉ quan sát các transition được allow-list trước `SaveChangesAsync`, gắn event và dispatch vào cùng unit of work. Không gọi SaveChanges đệ quy hoặc HTTP/Gemini; SaveChanges thất bại rollback cả nghiệp vụ và event. Đăng ký tại cấu hình runtime DbContext; seed/bootstrap không có human actor không phát sự kiện quản trị giả. Transaction caller đã mở không bị thay đổi quyền sở hữu.
- Bổ sung sau ca SQL atomicity: MySQL provider không bảo đảm implicit transaction cho batch có event thêm từ interceptor. Nếu mutation được quan sát chưa có transaction, interceptor mở transaction trước stage và kết thúc sau SaveChanges; failure/cancel rollback transaction mình sở hữu. Nếu caller đã mở transaction thì chỉ tham gia, không commit/rollback transaction của caller. Không thêm một SaveChanges hay network call; không đổi AI queue/provider.
- Allow-list: Assignment Published/Closed/thay DueAt hoặc Instructions; progress lần đầu Completed; FinalReviewVersion đổi cùng status; review request mới hoặc giải quyết; lớp Status/TeacherId; membership Added/Status; ledger Added/EndedAt; tạo curriculum/assignment metadata cho Manager; audit account/role và graph hợp lệ. Không quan sát draft answers, ViewedAt, rowversion đơn thuần, quota hoặc retry nội bộ.
- AI chỉ ghi marker khi job chuyển terminal. `resource_context` nullable JSON chứa IDs/loại/version allow-list, không prompt, response, bài làm, điểm hoặc ảnh. Marker không tạo inbox. Worker riêng kiểm tra latest job của mỗi attempt có hiệu lực và generation `ManualRetryCount`, bỏ câu void/skipped theo trạng thái thật. Aggregate dedupe bằng hash tập attempt/latest-job/generation; một thông báo riêng student và một teacher, số success/fallback/failed, không nói điểm đã chốt hoặc nhận xét/lộ trình đã sẵn sàng.
- Một event tối đa 500 recipients; snapshot lớn chia deterministic chunks. Worker chỉ intersect snapshot với quan hệ/account hiện tại, không bổ sung học sinh mới. Handover tạo thông báo mới cho teacher hiện tại; teacher cũ chỉ metadata, không bản sao kết quả.
- Related link dựng server từ context và vai trò hiện tại, kiểm tra membership/target/ownership. Thiếu scope trả tóm tắt với link null; notification không cấp quyền. Event stale Published khi lớp/bài đã đóng bị suppress, không phát nhắc học mới.
- Chốt/mở lại toàn bài: version trong event phải còn bằng `FinalReviewVersion` tại dispatch; giữ sự kiện lịch sử nhưng suppress phiên bản đã thay thế. Khóa student/progress trong transaction ngắn lúc kiểm tra và phân phối, không phát trạng thái “đã chốt” lỗi thời sau lần mở lại/chốt mới.
- Database thêm duy nhất `notification_events.resource_context` JSON nullable ở gate này; event vẫn append-only. Không backfill event từ dữ liệu đã tồn tại.

### Adapter NF-04 được khóa trước code

- API GET `/api/v1/activity` và GET `/api/v1/activity/{source}/{id}`, chỉ đọc. Các nguồn authoritative: authorization audit allow-list, teacher review history, review request và ledger áp dụng giáo trình. Không tạo bảng activity mới, không backfill inbox.
- Quyền riêng theo convention module.resource.action: `activity.history.read_own` Student, `activity.history.read_scoped` Teacher, `activity.history.read_administrative` Manager, `platform.activity.read` PlatformAdmin (non-delegable). Active system roles được additive backfill; role tùy chỉnh không tự nhận.
- DTO nguồn/ID thật, thời gian/actor/loại/đối tượng, trạng thái/version trước-sau được phép và lý do đã sanitize. Không trả JSON audit thô, prompt, rubric raw, ảnh, đáp án, token. Manager/Admin chỉ metadata, không điểm/note bài làm hoặc before/after học thuật.
- Student chỉ audit kết quả target chính mình, review history/request của mình và ledger lớp từng tham gia. Teacher chỉ review/result của lớp hiện phụ trách và audit học thuật do mình thực hiện; mất ownership không đọc lại kết quả lớp cũ. Manager giới hạn loại administrative trong tenant; Platform chỉ nguồn PLATFORM đã lọc.
- List có source/time/action filters, page ≤100, stable order time/source/id; detail dùng cùng scope query, 404 foreign. UI bốn actor có Nhật ký hoạt động riêng, loading/empty/error/permission, responsive; chỉ link server đã kiểm tra, không nút tự đánh dấu nghiệp vụ đã xử lý.

### Vận hành NF-05 (khóa trước source)

- Chỉ metadata outbox trong tenant hiện tại: trạng thái/số lần thử/đếm phân phối/suppress/mã lỗi/độ trễ; không body, recipient, resource context hoặc học liệu. Manager có `notifications.dispatches.read`, `.redrive`; Platform có `platform.notifications.read_dispatches`, `.redrive` bất khả ủy quyền. Redrive là quyền sensitive; không tự cấp role tùy chỉnh.
- GET `/api/v1/notification-operations` paged ≤100 và summary; POST `/{eventId}/redrive` với clientActionId, expectedRowVersion, reason 1…500. Chỉ DeadLetter được re-drive, optimistic concurrency; Pending/Processing/Completed không bị reset. Retry cùng clientActionId idempotent; audit append-only có actor/lý do/trước-sau, không xóa event/delivery. Worker vẫn revalidate scope/version, không hồi sinh thông báo obsolete.
- Adapter activity bổ sung nguồn Business từ event đã có: own delivery cho học thuật, chỉ event metadata allow-list cho Manager/Platform. Trang nhật ký giới hạn cửa sổ 2.000 dòng; dùng bộ lọc ngày để xem xa hơn, tránh gom 100.000 dòng từ nhiều nguồn vào memory.
- Kiểm tra generation khi dispatch dùng transaction ngắn khóa student/attempt/job; không đổi AI queue, provider hoặc retry policy. Job retry có thể tái dùng cùng ID nên marker dedupe gồm ManualRetryCount, không dùng CreatedAt làm thời gian của lượt retry mới.

## Gate và rollout

### Bổ sung UX được người dùng yêu cầu 2026-10-11

- Trang thông báo/nhật ký học sinh không có bộ chọn môn hoặc phạm vi lớp; đây là hộp thư/lịch sử tài khoản, không lọc theo môn. Menu Nhật ký dùng cùng căn lề và icon SVG với Hồ sơ năng lực.
- Box chuông có “Đã xem” cho các mục chưa đọc đang hiện (không tự ack), và “Xóa tất cả” chỉ dọn xem nhanh trên trình duyệt hiện tại. Lưu watermark delivery ID scoped center/user, không gọi hide/delete inbox, không đổi số chưa đọc bằng thao tác dọn. Mục mới sau watermark vẫn hiện; có thể hiện lại box. Không hứa đồng bộ dọn box giữa thiết bị.
- Nhật ký hiển thị tiếng Việt, mô tả hành động và tên đối tượng đã được scope; mã/source/status kỹ thuật chỉ trong mục mở riêng. Không mở rộng quyền đọc học thuật để lấy tên; Manager/Platform vẫn metadata. DTO thêm targetLabel nullable, không thêm bảng/migration.
- Thông báo/nhật ký dùng body tối thiểu 16px và metadata 14px; popover responsive không overlay. “Theo dõi gửi” giải thích vận hành kỹ thuật và không lẫn chức năng soạn/gửi.
- Học sinh nhập đáp án số/biểu thức/tọa độ bằng RichMathEditor với nút Chèn công thức như lập luận; không bật thường trực VisualMathField. Authoring giữ input theo mode, TextExact vẫn văn bản thuần, trắc nghiệm/chế độ chấm/finalAnswer/answerDisplayLatex và review chỉ đọc không đổi. MathLive chỉ ở popover chèn công thức/Casio, không đổi parser/chấm AI/bài đã nộp.

### Bổ sung 2026-10-11: nhập tự do và bộ lọc dấu hiệu nhạy cảm

Người dùng chọn giữ nhập tự do, chặn dấu hiệu nhạy cảm thay vì mẫu cố định. Đây là kiểm tra heuristic, không phải bảo đảm DLP tuyệt đối: có thể chặn nhầm hoặc bỏ sót tên riêng/cách diễn đạt không rõ ràng. Không dùng AI, không đọc roster học sinh để đoán nội dung và không tự sửa văn bản.

- Preview và send đều kiểm tra title/body/lý do đính chính; reply và lý do thu hồi kiểm tra ở server trước khi tạo hàng dữ liệu. Reply/thu hồi xác định kênh từ audience bất biến đã lưu, không tin kênh do client khai báo. Gọi thẳng send không bypass được.
- Mọi kênh chặn dấu hiệu credential/token, URL/path/HTML chủ động (thông báo chỉ plain text, không kênh gửi tệp/link). Hai kênh `PlatformSupport` và `CenterManagersRelay` bổ sung chặn dấu hiệu bài làm/đáp án/lời giải/ảnh nháp/rubric, điểm cụ thể, định danh học sinh và thông tin liên lạc/cá nhân rõ ràng. Không cấm thông tin học thuật hợp lệ trong kênh Teacher → Student được phép.
- Chuẩn hóa Unicode/dấu tiếng Việt/zero-width để dò dấu hiệu phổ biến; regex có timeout và đầu vào giới hạn. Quá hạn kiểm tra fail closed. Không ghi nội dung khớp vào log/Problem Details.
- Trả HTTP 400 `NOTIFICATION_CONTENT_RESTRICTED`, không đổi DTO/quyền/migration; UI chỉ hướng dẫn bỏ dữ liệu riêng/credential/link và mô tả lỗi vận hành ở mức khái quát. Không hồi tố sửa/xóa nội dung đã gửi.
- Test phải chứng minh preview/send/reply/reason bị chặn không tạo message/action/reply/outbox, kênh nội bộ hợp lệ không bị chặn theo luật học thuật, và retry/phân quyền/lifecycle vẫn giữ nguyên.

Mỗi checkpoint ghi trạng thái thực; toàn kế hoạch chỉ hoàn tất khi mọi checklist đã có bằng chứng. Unit + MySQL isolated atomicity/isolation/immutable/lost race + UI + benchmark như NF-01. Backup và hash academic tables trước additive migration local. Không test notice thật nếu chưa được duyệt người nhận/nội dung. Retention production cần quyết định chủ sở hữu riêng; bản đầu chỉ hide, không purge.
