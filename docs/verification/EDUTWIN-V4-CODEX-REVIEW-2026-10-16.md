# EduTwin V4 — rà soát và sửa sau Antigravity

Ngày: 16/10/2026. Trạng thái: đã sửa và kiểm thử các luồng dưới đây; không chứng nhận hoàn thành toàn bộ 38 tiêu chí của đặc tả V4.

## Phạm vi và baseline

- HEAD ban đầu: `46670e4b5bc12d22706ff219283209112a81f26f`, nhánh `shin/studytask`.
- Có working tree chưa commit từ Antigravity và công việc trước đó. Đã tích hợp trên trạng thái này; không reset, stage, commit hoặc push.
- Yêu cầu trực tiếp: rà soát bản V4, sửa lộ trình lặp chủ đề “Thì” và việc mọi buổi đều chuyển tới luyện tập thích ứng, cùng các lỗi liên quan.

## Phát hiện ở bản bàn giao

| Mức | Phát hiện | Hướng sửa |
|---|---|---|
| P1 | Trang lộ trình vẫn tạo lịch cũ, mọi nút start/resume điều hướng tới `/hoc-tap/luyen-tap` | Thay trang chính bằng tuyến kỹ năng/nhiệm vụ; đường bookmark cũ về trang tuyến; luyện tự do vẫn giữ cho nhu cầu riêng |
| P1 | Planner và agenda gán cứng Toán, bỏ qua môn, lớp và TargetSkillIds | Đọc Knowledge Graph, scope, nội dung Published, câu và evidence thật theo môn/tenant/owner |
| P1 | Sai một bài bất kỳ bị suy thành lỗi phân số/dấu; có bài bất kỳ được gán 85/90% mastery | Bỏ điểm và misconception giả; Unknown riêng, mastery chỉ đọc Twin đã có, trạng thái từ evidence theo từng kỹ năng |
| P1 | Player tạo ID bằng Date.now, nội dung và thứ tự cố định; evaluate lỗi vẫn báo Passed | Run/Activity/IssuedItem thật; không có fallback thành công giả; giữ lỗi để thử lại |
| P1 | Submit hoạt động chấm theo chuỗi “45”, “6”, tạo Attempt với QuestionId mặc định 1001 | Nộp câu đã phát qua validator và SubmitAttemptUseCase hiện hữu; hoạt động đọc không tạo Attempt giả |
| P1 | Evaluator dùng Activity.Score >=70, không căn cứ Evidence Gate; late review sửa trực tiếp Attempt | Một evaluator đọc issued submissions + evidence/review; không tự ghi Twin hay sửa điểm append-only |
| P1 | Start/resolve legacy có nhánh không đối chiếu Path owner; draft revision 0 vượt kiểm tra | Kiểm tra sở hữu xuyên chuỗi, scope lại ở các thao tác; OCC không miễn revision 0 |
| P1 | Worker legacy có thể supersede tuyến V4 sau Attempt | Giữ tuyến đã xác nhận; agenda đọc evidence mới để chọn bước tiếp theo |
| P2 | Preview tự supersede Goal, accept lặp làm tăng version/quyết định | Goal draft riêng; accept dưới khóa học sinh và idempotent |
| P2 | Nhiệm vụ 5/15/30 phút là tên và thời lượng gán cứng, không theo tuyến | Chọn nhiệm vụ thật phù hợp ngân sách, không đổi nhãn phút của hoạt động dài |
| P2 | Bản đọc path cũ có thể deserialize JSON V4 như mảng tuần | Adapter đọc an toàn và không backfill trong GET |
| P2 | Báo cáo gán lại nội dung của V4-01…38 thành endpoint/component và tự kết luận 38/38 | Không sử dụng báo cáo này làm nghiệm thu; đối chiếu lại đặc tả gốc |
| P2 | Docker local vẫn dùng image cũ; schema mới chưa áp dụng | Kiểm thử cô lập, sao lưu local rồi mới cập nhật runtime |

## Kiểm kê nội dung local

Đã đọc cấu trúc dữ liệu, không sửa nội dung học thuật: mỗi center mẫu có ba chủ đề Tiếng Anh (Thì, Mệnh đề quan hệ, Từ vựng theo ngữ cảnh) và ba chủ đề Toán (Hàm số, Mũ–Logarit, Nguyên hàm). Mỗi chủ đề hiện có 5 câu Active. Chưa có cây kỹ năng con chi tiết về các thì trong dữ liệu này; hệ thống không được tự bịa ra các node đã được giáo viên duyệt.

Luồng mới lập chặng từ các node thực trong mục tiêu. Khi graph có kỹ năng con, dùng node lá thay vì lặp lại node cha. Nếu nội dung chỉ có một chủ đề, nói đúng giới hạn dữ liệu, không làm giả một tuyến nhiều kỹ năng.

Câu có lời giải đã xem/câu đã làm không trở thành checkpoint độc lập mới. Dành đủ câu mới cho kiểm tra; thiếu câu luyện riêng thì ghi thiếu, không dùng cùng bộ câu ở cả hai chế độ để cố đạt.

## Bằng chứng kiểm thử

- Baseline trước sửa: nhóm V4 + ArchivedLearningScope 35 pass. Đây không chứng minh 38 AC, bởi các fixture V4 cũ kiểm tra logic hardcode và một số thiếu cả quan hệ FK bắt buộc.
- Full backend: **4324 pass, 0 fail, 103 skip** (`scratch/v4-backend-final.log`). 103 test yêu cầu môi trường relational tùy chọn không chạy trong lượt này; không tính là pass. Các lỗi fixture legacy thiếu Path owner và kỳ vọng policy cũ đã sửa mà không nới guard.
- Bộ V4 cuối sau bổ sung chuyển tuyến legacy, ExamPrep và ngân hàng chỉ có 5 câu: **24 pass, 0 fail** (`scratch/v4-targeted-final.log`). Đi qua canonical validator/submit và issued link. Unit/integration fixture mô phỏng ranh giới worker có ghi chú; không gọi đây là E2E provider thật.
- Full frontend: **754 pass, 0 fail** (`scratch/v4-frontend-final.log`). Kỳ vọng source bảo vệ scope và MissionPlayer thay cho điều hướng luyện chung cũ.
- Lint sạch. Build production local và Docker API/web thành công (`scratch/v4-docker-release.log`); cảnh báo chunk MathLive lớn từ bundle hiện hữu.
- HTTP/MySQL: 7 nhóm kiểm tra pass ở lượt đầu image sửa, bao gồm chủ đề Tiếng Anh thực, accept/start đồng thời idempotent, draft resume + OCC, cách ly owner/tenant, học không sinh Attempt/điểm giả, 5 câu independent qua API submit thật. Harness `scripts/e2e/learning_path_v4_acceptance.cjs` chỉ dùng schema tạm do `acceptance_stack.cjs` sở hữu. Thêm 6 câu synthetic rõ nhãn chỉ trong schema tạm để đủ bộ kiểm tra; không sửa dữ liệu chính hoặc giảm policy.
- Browser headless đã pass đăng nhập thật, preview Tiếng Anh, accept, renderer học riêng, autosave, reload resume, kết quả ActivityOnlyCompleted và mobile không tràn ngang. Ảnh thực đã xem: `storage/v4-browser-evidence/study-desktop.png`, `path-mobile.png`. Harness chỉ dùng tài khoản seed của stack tạm; không dùng harness cũ đã retired. Lượt đầu sửa điều kiện chờ `/dang-nhap` trong harness, không vá auth của sản phẩm.
- Image cuối: chạy lại **7/7 nhóm HTTP/MySQL PASS** và **browser PASS**, bao gồm chờ agenda phản ánh Study Completed sau khi đóng player (`scratch/v4-api-acceptance.json`, `scratch/v4-browser-results.json`). Không sử dụng API key provider trong stack thử; kết quả AI bên ngoài không được tuyên bố đã kiểm chứng.

## Rollout và bảo toàn dữ liệu

- Sao lưu trước rollout: `storage/backups/pre-v4-codex-20261016.sql` (thư mục ignored); không đưa dữ liệu hoặc secret vào Git.
- Có kiểm kê counts trước cập nhật ở `storage/backups/pre-v4-counts.txt`.
- Đã build và cập nhật **chỉ API/web** bằng `docker compose up -d --no-deps api web`; không recreate MySQL, không xóa volume. API áp dụng migration khi khởi động theo cơ chế hiện hữu.
- Xác nhận migration cuối `20261015180050_AddLearningPathV4Entities`, đủ sáu bảng V4; image đang chạy trùng image build cuối. HTTP giao diện 200, API protected 401 khi không đăng nhập (`scratch/v4-local-verification.json`).
- Counts trước/sau: users **17/17**, attempts **4/4**, evidence_assessments **4/4**, twin_update_history **4/4**. Không đưa bài làm thử vào dữ liệu chính.
- Lộ trình legacy giữ lịch sử. Học sinh cần dùng **Thiết lập mục tiêu → Xem trước lộ trình → Chọn tuyến học này** để chuyển sang V4; không tự chuyển/cấp đạt cho tiến độ cũ.

## Sửa bổ sung từ ảnh lỗi 400 khi xem trước mục tiêu

- Đã tái hiện nguyên nhân: URL chỉ có subjectId làm hook trả `classId: ""`. GET bỏ chuỗi rỗng nhưng POST goal gửi nguyên chuỗi; nullable Guid model binding trả 400 trước khi vào planner. Response thực chỉ rõ lỗi `errors.classId`; ngày và skill không phải nguyên nhân.
- Hook nay trả lớp hiện tại được academic-context xác nhận khi URL chưa có lớp. Hai POST goal/proposal cũng loại mã lớp rỗng, để trường hợp không có lớp gửi đúng optional field.
- Modal sử dụng ProblemDetails để hiển thị thông báo từ API thay vì chuỗi Axios “Request failed with status code 400”.
- Tái hiện HTTP trên schema thử: empty classId → 400; lớp hợp lệ + hạn 30/10/2026 + skill Thì → goal 200, preview 200 với đúng một chặng. Frontend chạy lại 754/754 pass, lint sạch. Browser **PASS** với URL thiếu classId, preview tất cả kỹ năng rồi điều chỉnh hạn 30/10/2026 và chọn riêng Thì trước khi accept; tiếp tục xác minh autosave/reload/kết quả/mobile (`scratch/v4-goal-browser.log`, `scratch/v4-browser-results.json`). 7 nhóm HTTP/MySQL chạy lại PASS. Docker web build thành công, cập nhật riêng web local sau kiểm thử; không đổi API/schema/dữ liệu chính trong lượt sửa bổ sung.

## Giới hạn không được gọi là hoàn thành toàn bộ V4

- Gói bài học tương tác sâu cho nhiều môn và màn authoring/publish giáo viên hoàn chỉnh chưa được bản Antigravity triển khai. Bản sửa không tự xuất bản bài học AI như đã được giáo viên duyệt.
- Khi có Published content khớp node ID/code, planner có thể đưa nó vào tuyến. Khi chưa có, dùng mô tả node/ví dụ có lời giải của ngân hàng thật với nhãn rõ; không gọi đó là một kho bài giảng đầy đủ.
- Chưa có bằng chứng pilot học sinh thật, chất lượng sư phạm, hiệu quả học tập, lịch ôn cá nhân đầy đủ hoặc tối ưu thời lượng được hiệu chuẩn.
- Mốc vận dụng đa kỹ năng, rubric riêng từng kỹ năng và phản hồi misconception đã xác nhận cần nội dung/coverage hợp lệ; không suy ra chỉ từ điểm tổng hoặc một lỗi ngẫu nhiên.
- Những mục trên là khoảng trống sản phẩm cần tiếp tục, không phải lý do khôi phục dữ liệu giả hoặc ghi 38/38 pass.
