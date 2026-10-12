# Kiểm thử hệ thống sau đồng bộ nhánh answer

Ngày: 12/10/2026. Hướng dẫn tự kiểm thử đã hoàn thành tại [SELF-TEST-GUIDE-2026-10-12.md](SELF-TEST-GUIDE-2026-10-12.md).

## Code được kiểm tra

Đã `git fetch origin --prune`; remote chỉ có `origin/student/answer`, không có nhánh riêng `origin/answer`. Theo nhánh answer dùng xuyên suốt công việc, đồng bộ bằng `git merge --ff-only origin/student/answer`: Already up to date. HEAD và remote cùng commit `d6688b4fb6ebcf9c79229006661e1462fb68d8c1`. Không có code mới từ remote để tích hợp. Các bản sửa F01–F21/R01–R03 và thay đổi local hiện có được giữ nguyên, chưa commit/push. Không reset/stash bỏ công việc.

Docker Desktop ban đầu tắt nên build đầu không chạy được vì thiếu engine pipe. Đã khởi động engine bằng Docker Desktop CLI, build API/web và nghiệm thu trên schema mới `edutwin_e2e_963637d6466a4ca087d22ab61f81a18f`, API 5001/web 3002. Không tạo fixture trong schema chính.

## Lỗi mới phát hiện và sửa

### R04 — Nộp luyện tự do mất ảnh khi upload thất bại

Callback production `handleFinalSubmit` trước sửa tiếp tục `submitAttempt` khi uploader trả null, dẫn đến lưu đáp án mà bỏ ảnh người học đã đính kèm. Hai test chạy chính callback tái hiện: uploader trả null hoặc lần đầu null/lần sau thành công đều tạo attempt sớm, trái kỳ vọng. Test trước sửa: 3 Passed/2 Failed.

Đã chặn submit khi có snapshot mà không lấy được token, báo lỗi tải ảnh, bật retry và giữ nguyên đáp án/ảnh. `finally` vẫn nhả submission lock. Năm test xác nhận: null, retry sau null, exception, không có ảnh và token đã chuẩn bị. Không đổi payload chấm điểm hay nới quyền.

Browser trên Docker mới: cố tình cho riêng endpoint prepare-upload trả 503; UI hiện lỗi, giữ đáp án/ảnh; SQL không có attempt mới. Khôi phục proxy, dùng PNG từ bảng nháp và thử lại: upload 200, attempt #16 có đúng một attachment, ảnh hiển thị chỉ đọc. Câu kế tiếp #17 không có ảnh, counter 1 so với counter 2 câu đầu. Buổi Completed, checkpoint 1, progress 33.3% trên bản gốc.

PNG synthetic đầu dùng trong thử nghiệm từng bị API từ chối 400 sau khi bỏ fault; không tính lần đó là retry thành công. PNG do ứng dụng xuất được đối chiếu qua API và browser, đều 200. Metadata proxy chỉ ghi method/path/status/Content-Type/kích thước, không ghi token, body hoặc Authorization. Cấu hình proxy đã khôi phục.

### R05 — Thông báo chờ giáo viên trong luyện tự do

Browser feedback của attempt tự do hiển thị “Đang chờ giáo viên xem xét” và hứa giáo viên chốt kết quả, trong khi boundary của free practice không có luồng giáo viên duyệt. Đây là lỗi thông báo/luồng kỳ vọng, không phải lý do mở rộng quyền giáo viên.

Thêm context `practiceMode` từ LearningPlayer vào feedback hierarchy và helper tạo thông báo. Luyện tự do dùng thông báo phân tích chưa hoàn tất/cần kiểm tra, hướng dẫn đối chiếu lời giải hoặc thử chấm lại; không hứa teacher queue. Nhận xét fallback và nhãn trạng thái trên UI tự do được sửa tương ứng. Assignment vẫn giữ AI đề xuất/chờ giáo viên duyệt, kể cả manual fallback. Không đổi correctness, điểm, trạng thái backend hay API authorization.

Bốn test mới xác nhận fallback tự do, AI disagreement tự do, assignment có điểm AI đề xuất và manual assignment/Completed. Browser trên image mới hiển thị “Phân tích AI chưa hoàn tất”, hướng dẫn đối chiếu/retry, vẫn giữ điểm theo quy tắc 10/10 và không hứa có giáo viên duyệt.

### R06 — Retry lỗi mạng đổi idempotency key và payload

`handleRetrySubmission` trước sửa tạo clientSubmissionId mới, còn submit dựng lại timer/answer/counter từ state hiện tại. Khi server đã commit nhưng response bị mất, retry có thể tạo attempt thứ hai. Test chạy chính hai callback production đã tái hiện: key đổi từ stable sang new, thời gian 12 thành 999, counter 1 thành 5 và answer cũng thay đổi.

Đã giữ key khi retry và đóng băng payload free practice trước gửi. Lỗi mạng/5xx không rõ đã commit hay chưa phải replay nguyên body, kể cả upload token đã được dùng; không upload lại ảnh đã gửi. Khi có phản hồi validation/403/404/422 rõ ràng, bỏ snapshot payload để cho sửa đầu vào với key chưa được dùng. Snapshot được xóa khi chuyển câu hoặc đổi user/subject/class/history/session; không mang request cũ sang scope mới. Assignment vẫn dùng cơ chế frozen payload riêng trước đó.

Test callback xác nhận retry sau lost response, timer/input drift, image token đã dùng, validation correction và reset khi Next. Bốn ca HTTP trên MySQL xác nhận: gửi lại cùng request có một attempt; đổi payload cùng key bị 409; replay token đã dùng có một attachment; validation 400 chưa tạo attempt rồi sửa với cùng key thành công. Contract free-practice trả 202 cả lần đầu và replay; kỳ vọng 200 ở lượt script đầu đã được sửa theo controller, không sửa API để chiều test.

Lost-response ở frontend được mô phỏng trong callback, phía HTTP chủ động bỏ response rồi replay; không coi đây là fault injection đầy đủ trên mạng/browser. Payload snapshot ở component chưa được chứng nhận khôi phục sau reload hoặc tắt browser giữa lỗi mạng.

## Kết quả nghiệm thu

| Nhóm | Kết quả |
|---|---|
| Backend toàn bộ | 4165 Passed, 102 Skipped do không có connection trong lượt thường, 0 Failed; TRX `answer-system-retest-20261012.trx` |
| MySQL relational mới, một lượt đầy đủ | **112/112 Passed**, 0 Failed/Skipped; 17.86 phút; TRX `functional-retest-mysql-full-tmpfs-20261012.trx` |
| Default-column VSTest | 1/1 Passed trên schema acceptance đã migrate |
| Frontend cuối R04/R05/R06 | **708 Passed**, 0 Failed/Skipped |
| Bundle / lint / build | 3/3 Passed; 0 lint errors, 26 warnings; TypeScript/Vite build thành công |
| HTTP F01–F21 | 21/21 Passed trên schema mới |
| Assignment | 24/24 Passed: ownership, timer, conflict, submit, void, worker fallback |
| HTTP phạm vi ảnh hưởng | 10/10 Passed: race import, inactive scope, guard, NeedsReview/restart, offline acknowledgement |
| Phiên bản kế hoạch | 6/6 Passed trên fixture mới: bản gốc giữ progress, current tách identity, owner/subject/class/missing boundary |
| Replay luyện tự do bổ sung | 4/4 Passed: discarded response, payload drift, consumed image token, validation correction |
| Role smoke | 39 route được đối chiếu status mong đợi và JSON; có cả 400/403 có chủ đích |
| Browser + SQL | R04 upload lỗi không tạo attempt; retry lưu ảnh; counter 2 → 1; Completed/checkpoint/progress đúng; R05 thông báo đúng luồng |

Không cộng trùng test giữa các suite. 101 ca skipped trong lượt thường nằm trong suite SQL; ca default-column còn lại chạy riêng, đủ 102. Lượt SQL đầu dùng volume disk chạy chậm đã được hủy có chủ đích bằng đúng testhost của harness và ghi Aborted; container/volume đã dọn. Lượt 112/112 là chạy mới từ đầu trên server tmpfs, không cộng kết quả partial để báo full pass.

TMPFS dùng schema/server MySQL thật, FK/constraint/transaction/concurrency giữ nguyên; không chứng nhận mất điện/crash recovery. Live AI không được gọi: Gemini key rỗng, fallback và pipeline synthetic provider được kiểm tra.

## Bằng chứng và giới hạn

Evidence tại `docs/verification/evidence/answer_system_retest_20261012/` (gitignored): log, TRX, HTTP JSON, browser SQL proof, ảnh upload-blocked/upload-success và kết quả browser cuối. Fixture chỉ chứa dữ liệu synthetic. Lượt kiểm thử không chứng minh tuyệt đối mọi tình huống đều không lỗi.

Các rủi ro đã ghi trong audit trước còn nguyên phạm vi: path chung theo học sinh/môn khi nhiều lớp, transaction generate chưa bao trọn engine, lock theo process, preview cache RAM, giới hạn giải nén thực tế, dependency advisory, live AI, nhiều replica, tải lớn và backup/restore production. Không ghi chúng là đã khắc phục hoặc đã nghiệm thu trong lượt này.

## Trạng thái cuối

Các trạng thái chưa commit/push bên dưới là tại thời điểm nghiệm thu. Khi đóng gói bàn giao lên nhánh `shin/check-code` theo yêu cầu tiếp theo, script dùng lại được chuyển từ `scratch` sang `scripts/e2e`, hướng dẫn cập nhật đường dẫn; bản vá thử, fixture kết quả, probe chẩn đoán và log tạm được dọn. Báo cáo giữ lại; evidence runtime ở máy không được đưa lên Git. Harness SQL chỉ giữ lượt đầy đủ, bỏ mode resume/repair phụ thuộc fixture của đợt cũ.

Đã kiểm tra remote lần cuối: HEAD và `origin/student/answer` vẫn cùng commit nêu trên. Build API và web cuối thành công. Web image cuối R04/R05/R06 được khởi động trên stack acceptance; browser tải đúng bản kế hoạch đã lưu, hiển thị buổi Đã xong và progress 33.3%. Helper đối chiếu TRX/JSON/log xác nhận 4165 backend, 112 SQL một lượt, 1 default-column, 708 frontend, 3 bundle, 65 HTTP và 39 role smoke; không cộng trùng suite.

Đã đóng tab browser do đợt thử tạo, dọn container API/web acceptance, schema/account disposable, server MySQL riêng và volume của lượt disk bị hủy. Không còn container acceptance/retest MySQL; manifest đã xóa. Cấu hình proxy fault/metadata đã khôi phục. Docker chính API/web/Adminer vẫn chạy, MySQL healthy; không redeploy API/web chính trong lượt nghiệm thu này. `git diff --check` sạch. Bản sửa vẫn ở working tree, chưa commit/push.

Ảnh xác nhận thông báo luyện tự do sau sửa:

![Thông báo luyện tự do đúng luồng](evidence/answer_system_retest_20261012/practice-feedback-final.png)
