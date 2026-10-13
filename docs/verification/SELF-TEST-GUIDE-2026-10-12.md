# Hướng dẫn tự kiểm thử các lỗi đã sửa

Thực hiện trong PowerShell tại `C:\AI-Driven-Adaptive-Learning-and-Competency-Assessment-Platform`, với bản code đã có các bản sửa F01–F21/R01–R06. Script tái sử dụng nằm trong `scripts/e2e`; dữ liệu kết quả được sinh vào `scratch` và không được đưa lên Git.

## 1. Chuẩn bị và chạy test tự động

Cần .NET 10, Node.js phù hợp package hiện tại, npm và Docker Desktop đang chạy. `.env` của project phải có cấu hình MySQL hợp lệ. Không chia sẻ file này hoặc token trong ảnh chụp.

```powershell
Set-Location C:\AI-Driven-Adaptive-Learning-and-Competency-Assessment-Platform
dotnet test tests/EduTwin.BLL.Tests --logger "trx;LogFileName=self-test.trx"
Push-Location web/edutwin-web
npm ci
npm run verify
Pop-Location
docker compose build api web
docker compose up -d mysql
node scripts/e2e/acceptance_stack.cjs start
```

Kiểm tra kết quả sau từng lệnh; `$LASTEXITCODE` phải bằng 0. Nếu có failed/build error thì dừng để đọc lỗi, không coi các bước sau là nghiệm thu thành công. Ở code cuối đợt 12/10, backend có 4165 passed và 102 skipped khi chưa cấu hình relational; frontend 708 passed; bundle 3 passed. Số test có thể tăng khi thêm kiểm thử. 26 lint warnings và cảnh báo bundle lớn là các tồn tại đã ghi nhận, không phải 0 warnings.

Stack chính ở cổng 3000 không tự đổi code khi chỉ build image. Stack nghiệm thu dùng API 5001/web 3002, schema và account tạm riêng. Không dùng dữ liệu production để tạo fixture hoặc rollback.

```powershell
node scripts/e2e/functional_audit_regression.cjs
node scripts/e2e/assignment_api_acceptance.cjs
node scripts/e2e/functional_retest_impact.cjs
node scripts/e2e/functional_retest_practice_retry.cjs
node scripts/e2e/functional_role_smoke.cjs
node scripts/e2e/functional_retest_default.cjs
```

Kỳ vọng: lần lượt 21, 24, 10, 4, 39 ca Passed và default-column Passed; không có FAIL. Các script sẽ tạo dữ liệu vào schema nghiệm thu. Cần giữ stack chạy nếu tiếp tục kiểm tra giao diện.

Để bật các test SQL đang bị skipped, chạy thêm:

```powershell
dotnet build tests/EduTwin.BLL.Tests
node scripts/e2e/functional_retest_mysql.cjs --full-tmpfs
```

Harness dùng MySQL riêng trên port 3308, tự dọn server khi kết thúc. Port này cần trống. Có 112 ca được chọn; bao gồm migration, FK, concurrency, notification và recommendation. `--full-tmpfs` tăng tốc DDL bằng database tạm trong RAM; không dùng để chứng nhận crash/power-loss recovery. Ca default-column chạy riêng trên schema đã migrate ở bước trước. Không coi skipped là passed. Không chạy `--resume` bằng danh sách lịch sử để thay cho một lượt nghiệm thu mới.

## 2. Tài khoản và cách ghi nhận

Mở `http://127.0.0.1:3002` trong cửa sổ riêng tư, dùng cùng hostname với URL fixture. `localhost` và `127.0.0.1` có vùng lưu phiên riêng, nên chuyển hostname có thể phải đăng nhập lại. Account chỉ dùng cho stack disposable:

| Vai trò | Trung tâm | Tên đăng nhập |
|---|---|---|
| Học sinh | EDUTWIN_A | student02 |
| Giáo viên | EDUTWIN_A | teacher.math |
| Quản lý | EDUTWIN_A | manager |
| Quản trị nền tảng | PLATFORM | platform.admin |

Mật khẩu fixture: `LocalTestOnly-20261006!`. Đây không phải mật khẩu tài khoản chính. Mỗi ca ghi: bước làm, kết quả mong đợi, thực tế, Passed/Failed, ảnh chụp và thời điểm. Dùng F12 → Network nếu cần xem request; không đưa Authorization/token vào báo cáo.

## 3. Bản nháp, nộp bài và xem lại — F01

```powershell
node scripts/e2e/functional_retest_browser_draft.cjs create
```

Mở URL script vừa in bằng account student02. Đây là bài hai câu có draft trên server trước lần mở browser.

1. Kiểm tra cả hai câu có đáp án/lập luận được khôi phục.
2. Ở câu 10000, đổi lập luận thành đúng chuỗi `RETEST_UPDATED_REASONING: y=2*3+1=7.`; giữ đáp án đúng.
3. Chờ lưu, đổi sang câu hai, refresh, quay lại câu đầu. Lập luận mới và đáp án phải còn; không bị draft cũ ghi đè.
4. Chạy `node scripts/e2e/functional_retest_browser_draft.cjs after-edit`: phải exit 0; script đối chiếu trực tiếp draft server.
5. Nộp bài, chờ kết quả rồi mở lại cả hai câu. Kết quả phải chỉ đọc, không cho sửa/nộp lại. Nếu AI không được cấu hình, trạng thái fallback/đợi giáo viên phải rõ; không tự coi điểm tạm là đã chốt.

## 4. Luyện tập và quay lại lộ trình — F02–F09, R01/R02

```powershell
node scripts/e2e/functional_retest_browser_path.cjs
```

Mở URL mới script in ra. Chọn đúng lớp/môn, ghi lại pathId và bắt đầu buổi đầu:

1. URL luyện tập phải có sessionId/classId; câu được lấy từ buổi đó.
2. Câu đầu đổi đáp án hai lần rồi nộp. Chọn câu tiếp: đáp án, công thức, lập luận và ảnh của câu trước phải được xóa. Nhập đáp án câu mới đúng một lần; nếu xem payload nộp trong Network thì `answerChanges` phải là 1, không cộng số lần của câu trước.
3. Làm đủ và đúng các câu, đánh giá buổi. Buổi chuyển Completed, checkpoint/tiến độ cập nhật. Chỉ bấm Completed khi chưa làm đủ không được qua. Làm sai đủ câu phải vào NeedsReview; làm lại cần lượt mới sau lúc bắt đầu lại.
4. Quay về lộ trình: buổi đã xong phải vẫn hiện trên đúng bản kế hoạch đã học. Bấm xem lại buổi Completed phải mở được, không gửi PATCH kéo trạng thái về InProgress.
5. Tạo bản kế hoạch mới, rồi mở lại link có pathId của bản cũ. Bản cũ phải giữ progress; bản hiện tại có identity riêng. Đổi lớp/môn không mang session/pathId của scope cũ sang.
6. So sánh TheoryHeavy/PracticeHeavy/ExamPrep và thời lượng: lịch phải thay đổi phù hợp; chỉ chứa câu Active tự chấm được. Thử đầu vào ngoài giới hạn trong Network/API: 400 cho dữ liệu sai, 404 cho tài nguyên không có quyền/không tồn tại, 409 cho điều kiện chưa khả dụng hoặc version stale; không có 500.

`functional_retest_plan_version.cjs` chứa kỳ vọng của fixture lịch sử, không chạy trực tiếp trên stack mới rồi kết luận FAIL ứng dụng. Dùng unit test và các bước trên, hoặc tạo lại đầy đủ fixture phù hợp trước khi chạy.

### Tải ảnh và thông báo AI — R04/R05

Trong câu luyện tự do, vẽ một nét bằng bảng nháp rồi đính kèm ảnh do ứng dụng tạo. Đáp án/lập luận phải hợp lệ.

1. Chạy `node scripts/e2e/functional_retest_upload_fault.cjs on` để riêng upload của proxy acceptance trả 503.
2. Nộp bài: phải hiện lỗi tải ảnh/nút thử lại, giữ đáp án và ảnh.
3. Chạy `node scripts/e2e/functional_retest_upload_fault.cjs check-blocked`: exit 0 xác nhận chưa tạo attempt.
4. Chạy `node scripts/e2e/functional_retest_upload_fault.cjs off` để khôi phục proxy; luôn chạy off kể cả test thất bại.
5. Bấm thử nộp lại: ảnh phải hiện trong bài đã nộp và chỉ đọc; không tạo hai attempt cho một lần nộp thành công.
6. Nếu luyện tự do dùng AI fallback, thông báo phải là phân tích AI chưa hoàn tất/cần kiểm tra và hướng dẫn đối chiếu hoặc retry. Không hứa giáo viên sẽ duyệt. Bài tập có giáo viên vẫn phải hiển thị chờ giáo viên duyệt khi phù hợp.

Các mode fault chỉ dùng với manifest acceptance hiện tại. Không áp dụng config này lên proxy chính.

R06: chạy script `functional_retest_practice_retry.cjs` để đối chiếu số attempt/attachment khi replay cùng clientSubmissionId và payload. Lần đầu và replay đều trả 202; cùng key nhưng đổi payload phải 409. Các test `practiceAttachmentSubmit.test.ts` chạy callback thật và mô phỏng response bị mất: timer/input thay đổi vẫn phải retry đúng request cũ; ảnh đã gửi không upload lại; validation rõ ràng vẫn cho sửa. Đây là kiểm tra retry trong phiên hiện tại, chưa chứng nhận khôi phục snapshot request sau khi reload/tắt browser.

## 5. Import và ràng buộc — F10–F16

Dùng giáo viên vào ngân hàng câu hỏi → Import. Tạo CSV UTF-8:

```csv
Nội dung câu hỏi,Loại câu hỏi,Đáp án đúng,Lời giải,MaxScore,ReasoningRequired
"Dòng thứ nhất
Dòng thứ hai",ShortAnswer,2,"Có dấu ""nháy""",10,true
Câu sai điểm,ShortAnswer,2,Giải thích,1000,false
Câu sai boolean,ShortAnswer,2,Giải thích,10,tru
```

Preview phải có 3 hàng, 1 hợp lệ/2 lỗi; dòng xuống hàng vẫn là một câu. Giá trị 1000 và `tru` phải được báo lỗi rõ. Thử thêm điểm 0, số âm hoặc hơn hai số thập phân ở tạo/sửa câu: đều bị chặn.

Thử XLSX với ô text thông thường và DOCX dạng bảng có hàng header/cột câu hỏi: phải đọc được đúng dữ liệu. File không đúng cấu trúc phải hiện lỗi, không báo import thành công giả. Chọn môn/chủ đề active hợp lệ rồi confirm; số câu tạo phải đúng hàng hợp lệ theo thao tác UI. Không confirm cùng preview hai lần để tạo trùng. Student/Manager không có quyền import; truy cập API phải bị chặn 403. Các ca worksheet path đặc biệt, race confirm, inactive scope và giới hạn upload đã có trong script tự động.

## 6. Báo cáo, thống kê và ghi chú — F17–F21

1. Quản lý xem thống kê lớp dùng curriculum application hiện hành: không tự lấy toàn bộ môn khi chưa có application. Kiểm chứng số liệu bằng danh sách/lượt bài tương ứng, không chỉ thấy chart là kết luận đúng.
2. Giáo viên xem bài chưa chốt và đã chốt, xuất CSV: trạng thái điểm phải khớp UI. Nội dung chứa dấu nháy/newline vẫn nằm đúng ô; chuỗi bắt đầu bằng `=`, `+`, `-`, `@` phải được trung hòa khi xuất. Kiểm thử mở Excel thực tế là bước bổ sung, không nằm trong chứng nhận trước.
3. Lưu ghi chú ở giáo viên A; đăng xuất rồi vào giáo viên B hoặc center khác trên cùng browser: không thấy ghi chú A. Quay lại A: ghi chú vẫn còn. Cần có account/lớp hợp lệ cho cả hai người; không đánh giá bằng một account duy nhất.
4. Dashboard không hiện số 25 phút/3 câu giả định cố định. Dữ liệu rỗng/lỗi phải có trạng thái rõ, không biến thành thành công với số 0 gây hiểu nhầm.

## 7. Phân quyền và xung đột

Dùng hai cửa sổ/session khác nhau. Học sinh không đọc/sửa bài của học sinh khác; người khác center không đọc tài nguyên center này. Hai PATCH cùng version chỉ một thành công, request còn lại 409; nộp bài retry không tạo thêm attempt. Kiểm tra timer, late submit, void, worker retry/fallback bằng 24 ca assignment đã có. R03 rollback quyền được kiểm tra bởi SQL suite riêng, không thử rollback database đang dùng.

## 8. Dọn sau kiểm thử và kết luận

```powershell
node scripts/e2e/acceptance_stack.cjs stop
```

Luôn chạy stop kể cả có test lỗi. Chỉ nghiệm thu các ca có kết quả quan sát/đối chiếu đúng. Test xanh không đảm bảo mọi tổ hợp đều không lỗi: live AI, nhiều replica, tải lớn, backup/restore và các rủi ro kiến trúc còn được ghi riêng trong báo cáo hệ thống. Nếu gặp lỗi, lưu bước tái hiện và response đã bỏ credentials để xử lý tiếp.
