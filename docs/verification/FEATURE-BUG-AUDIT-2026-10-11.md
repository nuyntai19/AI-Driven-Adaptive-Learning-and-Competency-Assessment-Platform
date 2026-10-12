# Báo cáo lỗi: thống kê, import câu hỏi, lộ trình và luyện tập

Ngày kiểm tra: 11/10/2026. Nhánh `student/answer`, commit `d6688b4fb6ebcf9c79229006661e1462fb68d8c1`.

## Kết luận để ưu tiên xử lý

**Có lỗi mất đáp án và lập luận đã lưu trên server khi học sinh mở lại bài và đổi câu.** Đây là lỗi cần ưu tiên sửa trước khi triển khai. Lộ trình cũng có thể chứa câu hỏi Draft; thao tác bắt đầu buổi học chưa gắn được buổi học với phiên luyện tập. Import có nhiều trường hợp preview báo hợp lệ nhưng dữ liệu bị hiểu sai hoặc không lưu được.

Báo cáo ghi nhận 21 mục. P1: nguy cơ mất dữ liệu hoặc sai luồng học chính; P2: lỗi chức năng/ràng buộc cần sửa; P3: thông tin hiển thị hoặc trải nghiệm. “Tái hiện” nghĩa là đã thực thi; “mã nguồn” nghĩa là có căn cứ trong code nhưng chưa dựng đủ tình huống bằng trình duyệt/API. Mức ưu tiên là đánh giá của cuộc rà soát, không phải kết quả của công cụ tự động.

Không sửa mã ứng dụng trong đợt này. Các thao tác tạo câu hỏi, bài tập, sửa trạng thái và luyện tập chỉ thực hiện trong database audit riêng; database này được dọn sau kiểm tra.

## Phương pháp và bằng chứng

- Backend Release: **4.138 passed, 102 skipped, 0 failed**. Những bài bỏ qua cần MySQL integration được cấu hình riêng.
- Frontend: **688 passed, 0 failed**; lint **0 errors, 26 warnings**.
- Docker đã build thành công API và web từ commit trên.
- Bộ acceptance HTTP/MySQL về bài tập: **24/24 passed**. Kiểm tra đồng hồ server, submit/draft, optimistic concurrency, replay/idempotency, quyền và chấm điểm.
- Smoke test 39 request qua bốn vai trò; các đường đi đã thử không có lỗi 500 bất ngờ. Các phản hồi 400/403 đúng đầu vào/quyền được giữ riêng.
- Tạo thêm dữ liệu âm tính, thử API trên MySQL thật, gọi production use case bằng probe và thử trình duyệt để tìm lỗi ngoài các test đang có.

Bằng chứng JSON/log nằm trong [evidence/system_audit_20261011](evidence/system_audit_20261011). Đặc biệt: `hydration-create.json`, `hydration-after-switch.json`, `http-results.json`, `runtime-results.json`, `probes-results.json`, `export-results.json`, `assignment-api-results.json`. Không lưu access token/mật khẩu trong bộ bằng chứng.

Thư mục evidence được giữ local theo `.gitignore` hiện có của repository; hai báo cáo Markdown không bị ignore. Khi chia sẻ báo cáo sang máy khác, cần kèm bộ bằng chứng đã làm sạch này.

## A. Luyện tập và bản nháp

### F01 — P1 — Mở lại bài và đổi câu làm mất bản nháp trên server

**Đã tái hiện bằng trình duyệt + đọc lại API/MySQL.**

1. Tạo bài tập hai câu, bắt đầu làm bằng học sinh thuộc lớp.
2. Lưu draft server: câu `10000` có `finalAnswer="10000"` và `reasoningText="AUDIT_FIRST_REASONING: With x=3, y=7."`; câu `10001` cũng có đáp án/lập luận. Draft version = 1.
3. Mở lại bài trong trình duyệt đã đăng nhập. Câu đầu hiển thị trống, bộ đếm chỉ 1/2 dù API có đủ hai câu.
4. Chuyển sang câu hai rồi quay lại câu đầu, chờ autosave.
5. Đọc API: version = 4; câu đầu đã trở thành `finalAnswer=null`, `reasoningText=null`; câu hai vẫn còn dữ liệu.

Assignment audit: `26718209-4757-4fd9-8690-efabbd10c79e`. Đây là dữ liệu test đã được dọn, không phải bài của người dùng.

**Ảnh hưởng:** học sinh mất câu trả lời và lập luận mà chưa chủ động xóa; có thể nộp thiếu bài. API draft riêng lẻ vẫn vượt qua acceptance, vì lỗi nằm trong phối hợp state của giao diện.

**Căn cứ code:** [LearningPlayerPage.tsx](../../web/edutwin-web/src/pages/LearningPlayerPage.tsx), hydration map khoảng dòng 683–709; hydrate input câu hiện tại khoảng 1040–1140; `persistCurrentAnswer` từ 1144; bộ đếm từ 1888. Suy luận nguyên nhân: map đáp án nhận dữ liệu server sau khi input câu đầu đã hydrate rỗng; effect hydrate input chưa chạy lại theo dữ liệu map, rồi thao tác đổi câu ghi input rỗng vào map và autosave.

**Đề xuất:** chỉ cho phép autosave/đổi câu sau khi draft server hydrate xong; hydrate input theo snapshot/version và phạm vi bài-câu; phân biệt dữ liệu chưa tải với người dùng chủ động xóa. Không chỉ vá bộ đếm.

**Kiểm tra hồi quy:** server có đủ draft → reload → đổi tất cả câu → chờ autosave → đọc lại server vẫn giữ nguyên đáp án, lập luận, LaTeX và ảnh. Lặp với mạng chậm, draft conflict, đổi lớp, hết giờ và nộp bài.

### F02 — P1 — Bắt đầu buổi trong lộ trình chưa nối với đúng phiên luyện tập

**Xác định qua mã nguồn.** [StudentLearningPathPage.tsx](../../web/edutwin-web/src/pages/StudentLearningPathPage.tsx) khoảng 194–207 cập nhật buổi thành `InProgress`, rồi chuyển đến route luyện tập chung cùng tham số phạm vi. Route không mang `sessionId`, danh sách câu hoặc chủ đề của buổi. Các call site hiện có của cập nhật trạng thái buổi nằm tại trang lộ trình; chưa thấy luồng hoàn thành buổi từ kết quả luyện tập.

**Ảnh hưởng:** nút “bắt đầu” có thể đưa học sinh vào đề xuất chung, không đúng buổi được chọn; buổi bị treo đang học và tiến độ khó phản ánh việc đã làm.

**Đề xuất/hồi quy:** phiên luyện tập phải mang định danh buổi và phạm vi câu/chủ đề; hoàn thành dựa trên kết quả server. Chọn hai buổi khác chủ đề phải mở nội dung khác nhau và hoàn tất đúng buổi.

### F03 — P1 — Lộ trình chứa câu hỏi Draft

**Đã tái hiện trên MySQL thật:** đặt câu `10003` thành Draft trong dữ liệu audit, generate trả 200 và schedule chứa câu đó; đã khôi phục trạng thái sau probe.

[GenerateLearningPathUseCase.cs](../../src/EduTwin.BLL/Recommendations/UseCases/GenerateLearningPathUseCase.cs) khoảng 214 chỉ lọc center, subject, topic, chưa xóa; không lọc trạng thái Active. Probe gọi production `BuildSchedule` cũng chọn Draft `901`.

**Ảnh hưởng:** lộ trình có thể đưa nội dung chưa duyệt vào lịch học; khi chuyển sang luồng chỉ cho phép Active có thể bị chặn.

**Đề xuất/hồi quy:** áp dụng thống nhất điều kiện khả dụng của câu hỏi ở bước chọn và bước bắt đầu buổi; Draft/Archived/Deleted phải vắng mặt, kể cả khi trạng thái thay đổi sau khi tạo lịch.

### F04 — P2 — Đề xuất câu kế tiếp vẫn trả câu Archived

**Đã tái hiện trên MySQL thật:** archive câu đang được đề xuất `10001`, gọi next-question vẫn 200 và trả chính `10001`; đã khôi phục trạng thái.

[RecommendationEngine.cs](../../src/EduTwin.BLL/Recommendations/RecommendationEngine.cs), `GetNextQuestionAsync` khoảng 906–1000 đọc câu từ đề xuất đã lưu nhưng không kiểm tra lại trạng thái. Validator submit luyện tập lại chặn câu Archived.

**Ảnh hưởng:** học sinh nhận câu không còn làm/nộp hợp lệ. Không suy rộng thành bypass submit.

**Đề xuất:** revalidate snapshot và tính lại đề xuất khi câu không khả dụng. Test archive/delete câu giữa lúc có đề xuất và lúc bắt đầu làm.

## B. Lộ trình học

### F05 — P2 — Đầu vào hoặc điều kiện chưa đáp ứng trả 500

**Đã tái hiện:** `targetWeeks=0` → 500; subject không tồn tại → 500. Học sinh audit khác có đồ thị prerequisite bị chặn: generate → 500, log có `PREREQUISITE_GRAPH_BLOCKED`. Đây là tình huống cần thông báo nghiệp vụ hoặc validation, không phải thành công.

[RecommendationsController.cs](../../src/EduTwin.Api/Controllers/RecommendationsController.cs) khoảng 351–421 chỉ xử lý một số exception về scope. Lỗi validation/use case còn thoát thành exception. Ở stack Development, các trường hợp thử trả body không phải JSON; không kết luận production cũng lộ stack trace.

**Đề xuất:** map validation sang 400/422, điều kiện prerequisite sang mã nghiệp vụ phù hợp; trả ProblemDetails nhất quán. Giao diện cần hiện lý do và cách tiếp tục.

### F06 — P2 — Hai cập nhật buổi đồng thời trả 204 và 500

**Đã tái hiện bằng hai PATCH đồng thời trên MySQL:** một request 204, request còn lại 500.

[UpdateLearningPathSessionUseCase.cs](../../src/EduTwin.BLL/Recommendations/UseCases/UpdateLearningPathSessionUseCase.cs) tăng RowVersion và SaveChanges khoảng 56–57 nhưng API chưa chuyển concurrency exception sang conflict; request cũng chưa có expected version rõ ràng.

**Đề xuất:** trả 409 cùng version hiện tại, cơ chế refresh/retry có kiểm soát. Test hai tab đổi cùng buổi và hai buổi khác nhau trong cùng JSON schedule; không mất thay đổi đã xác nhận.

### F07 — P2 — Có thể đánh dấu hoàn thành mà chưa luyện tập; checkpoint lệch tiến độ

**Đã tái hiện:** PATCH Completed cho buổi chưa có attempt → 204; tiến độ thành 16,7% nhưng `checkpoint.completedTasks=0`. Probe production use case cũng hoàn thành với `attempts=0`.

Use case chỉ xác nhận trạng thái thuộc danh sách cho phép; chưa ràng buộc kết quả luyện tập và transition. Tiến độ tính lại trong schedule nhưng chưa đồng bộ checkpoint/item/path lifecycle.

**Đề xuất:** chốt Completed là tự đánh dấu hay chứng nhận học xong. Nếu tự đánh dấu, UI/báo cáo phải phân biệt với hoàn thành có bằng chứng. Kiểm tra invariant progress/checkpoint/item và trạng thái toàn lộ trình khi hoàn thành hết buổi.

### F08 — P2 — Chấp nhận enum tùy ý; ghi chú quá dài gây 500

**Đã tái hiện:** `goalType=INVALID_GOAL`, `pace=INVALID_PACE`, `preferredMode=INVALID_MODE` được generate 200 và lưu goal. Note 1.001 ký tự → 500 trên MySQL.

[GenerateLearningPathUseCase.cs](../../src/EduTwin.BLL/Recommendations/UseCases/GenerateLearningPathUseCase.cs) validation khoảng 330–334 chủ yếu kiểm tra khoảng số. Giới hạn chuỗi DB không được kiểm tra đầy đủ trước SaveChanges.

**Đề xuất:** whitelist giá trị DTO, trim và giới hạn chuỗi thống nhất schema; kiểm tra trước mutation. Test giá trị không biết, rỗng, Unicode và chiều dài sát biên.

### F09 — P2 — Một số lựa chọn trong bảng hỏi chưa tác động tới lịch học

**Xác định qua mã nguồn:** các trường weak/focus topics, self level, pace, preferred mode được lưu trong preference, nhưng thuật toán chọn/sắp câu và `GetSessionType` chủ yếu dùng ngày/mastery; chưa thấy sử dụng đầy đủ các lựa chọn này để đổi hoạt động/lịch. Không khẳng định mọi câu trả lời đều bị bỏ qua: thời lượng/mục tiêu vẫn có tác động ở các bước khác.

**Ảnh hưởng:** người dùng thay lựa chọn nhưng lịch không phản ánh ưu tiên/kỳ vọng được giao diện gợi ý.

**Đề xuất:** quy định cụ thể trường nào điều khiển thuật toán, trường nào chỉ ghi nhận. Test cùng học sinh với hai preference khác nhau và giải thích được khác biệt trong schedule.

## C. Import tạo nhiều câu hỏi

Mã chính: [QuestionImportUseCase.cs](../../src/EduTwin.BLL/CurriculumAndQuestions/Import/QuestionImportUseCase.cs). Import CSV chuẩn đơn giản đã preview/confirm thành công; confirm lại bị chặn; học sinh bị 403. Các lỗi dưới đây không có nghĩa toàn bộ import hỏng.

### F10 — P2 — CSV có xuống dòng trong ô được đặt trong dấu nháy bị tách sai

**Đã tái hiện qua HTTP và production parser:** một bản ghi logic có nội dung multiline → `totalRows=2`, `validCount=0`, `invalidCount=6`.

Parser `ReadLineAsync` khoảng 762/769 xử lý từng dòng vật lý; trạng thái dấu nháy không được giữ qua dòng. File CSV hợp lệ có lời giải/nội dung nhiều dòng bị lỗi.

**Đề xuất/hồi quy:** parser CSV hỗ trợ quoted newline, dấu nháy kép escaped, delimiter/BOM; một bản ghi multiline phải tính đúng một hàng và giữ nguyên nội dung.

### F11 — P2 — XLSX dùng inline string không được đọc đúng

**Đã tái hiện bằng XLSX tối thiểu với `inlineStr` và production parser:** không có hàng hợp lệ. `GetCellValue` khoảng 866 yêu cầu phần tử `<v>`, bỏ qua `<is><t>`.

File XLSX có thể dùng shared strings hoặc inline strings. Không phải mọi file XLSX đều lỗi. Parser còn chọn cố định `sheet1.xml`, cần kiểm tra quan hệ workbook/sheet thay vì giả định tên tệp.

**Đề xuất/hồi quy:** test workbook Excel và các trình xuất XLSX khác; shared/inline strings, sheet không nằm ở `sheet1.xml`, ô trống và công thức có cached value.

### F12 — P2 — Header tiếng Việt có dấu không nhận diện đúng

**Đã tái hiện bằng parser:** header `Nội dung câu hỏi,Loại câu hỏi,Đáp án đúng,Lời giải` → 0 hàng hợp lệ, nhiều lỗi thiếu trường. `NormalizeHeader` khoảng 881 loại ký tự ngoài ASCII, chưa chuyển dấu tiếng Việt về ký tự gốc trước đối chiếu alias.

**Đề xuất:** chuẩn hóa Unicode/diacritics hoặc dùng alias chính xác theo template. Test header có dấu, không dấu, BOM, khoảng trắng và kiểu chữ.

### F13 — P3 — Số hàng không hợp lệ thực chất là số lỗi từng trường

**Đã tái hiện:** file một hàng sai trả `invalidCount=6`; `InvalidCount = errors.Count` khoảng 476. Một hàng có thể có sáu lỗi.

**Ảnh hưởng:** tổng hợp preview không khớp số hàng, gây hiểu nhầm quy mô lỗi. Tách `invalidRowCount` theo RowIndex khỏi `errorCount`; test một hàng nhiều lỗi và nhiều hàng một lỗi.

### F14 — P2 — Preview nhận MaxScore vượt giới hạn DB

**Đã tái hiện HTTP/MySQL:** `MaxScore=1000` → preview 200/valid=1, confirm 400 `IMPORT_FAILED`. DB dùng decimal(5,2); validation import chỉ yêu cầu điểm > 0.

**Đề xuất:** kiểm tra miền giá trị và precision/scale ngay preview, phù hợp [QuestionConfiguration.cs](../../src/EduTwin.DAL/Persistence/Configurations/CurriculumAndQuestions/QuestionConfiguration.cs). Test 999,99; 1.000; nhiều chữ số thập phân; và kiểm tra transaction không lưu dở file khi một hàng lỗi.

### F15 — P2 — Sai chính tả boolean âm thầm làm mất yêu cầu lập luận

**Đã tái hiện:** `ReasoningRequired=tru` vẫn valid=1, kết quả `reasoningRequired=false`. Parsing khoảng 282–297 cho giá trị không nhận diện rơi về false.

**Ảnh hưởng:** người tạo tưởng bắt buộc lập luận nhưng câu được nhập không yêu cầu. Chỉ chấp nhận alias đã công bố; ô trống có thể dùng default rõ ràng, giá trị không biết phải báo lỗi hàng.

### F16 — P2 — Khác biệt giữa hợp đồng import và runtime

**Đã tái hiện:** `.docx` bị 400; CenterManager preview bị 403. `API_CONTRACTS.md` phần import khoảng 1288 mô tả có DOCX và quyền Teacher + CenterManager, trong khi runtime giới hạn các định dạng hiện có và quyền học thuật Teacher.

**Đề xuất:** chốt chính sách rồi đồng bộ tài liệu, UI và API. Không tự mở quyền manager để “sửa” khi quy định hiện hành có thể chủ ý chỉ cho Teacher. Nếu DOCX chưa hỗ trợ, bỏ khỏi cam kết/template; nếu cần hỗ trợ, bổ sung parser và preview an toàn.

## D. Báo cáo, thống kê và dữ liệu hiển thị

### F17 — P2 — Thống kê trung tâm dùng nguồn curriculum khác phạm vi học đang áp dụng

**Xác định qua mã nguồn:** [GetCenterDashboardUseCase.cs](../../src/EduTwin.BLL/Dashboards/GetCenterDashboardUseCase.cs) khoảng 231–267 dùng liên kết `CurriculumClasses`; dashboard lớp/phạm vi học dựa ledger `ClassCurriculumApplications` đang hiệu lực (`EndedAt == null`).

**Tình huống cần test:** application đã kết thúc nhưng planning link còn tồn tại, hoặc đang chuyển curriculum. Thống kê/ranking trung tâm có thể vẫn tính curriculum cũ trong khi dashboard lớp và học sinh đã chuyển phạm vi.

**Đề xuất:** định nghĩa thống kê là kế hoạch hay chương trình đang học; dùng chung resolver hoặc ghi nhãn riêng. Chưa tái hiện đầy đủ việc kết thúc/chuyển chương trình trong đợt này.

### F18 — P2 — CSV báo cáo học sinh không phân biệt điểm tạm thời

**Đã tái hiện bằng serializer production:** resultStatus Provisional, score 9 → CSV ghi `Đã nộp bài` và `9.0 / 10`, không có nhãn điểm chưa chốt. UI có helper hiển thị trạng thái; Excel ranking cũng có nhãn “Chưa chốt điểm”.

[teacherReportsHelpers.ts](../../web/edutwin-web/src/pages/teacher/teacherReportsHelpers.ts), `buildStudentReportCsv` khoảng 682.

**Ảnh hưởng:** CSV dùng bên ngoài có thể bị hiểu là kết quả cuối cùng khi AI/review chưa hoàn tất. Thêm cột trạng thái điểm, thống nhất các định dạng; test pending/provisional/final/void/override.

### F19 — P2 — CSV chưa trung hòa nội dung có thể được Excel hiểu là công thức

**Đã kiểm tra output serializer:** input `=1+1`, `@SUM(1,1)` → output giữ nguyên các giá trị này trong dấu nháy. `downloadCsv` khoảng 121–127 chỉ escape CSV. Dấu nháy CSV không bảo đảm ứng dụng bảng tính coi nội dung là text.

Đây là **rủi ro export**, chưa thử thực thi payload trong Microsoft Excel, chưa chứng minh khai thác trên môi trường người dùng.

**Đề xuất:** quy tắc neutralize/text cho dữ liệu do người dùng nhập bắt đầu bằng ký tự công thức; ưu tiên XLSX với cell kiểu string. Test tên học sinh/lớp/ghi chú có `=`, `+`, `-`, `@`, tab/newline và round-trip với bảng tính.

### F20 — P2 — Ghi chú sư phạm local chưa tách theo người đăng nhập

**Xác định qua mã nguồn:** [TeacherStudentManagementView.tsx](../../web/edutwin-web/src/pages/teacher/TeacherStudentManagementView.tsx) khoảng 68/79 dùng key chung `teacher_pedagogical_notes`, không namespace center/actor.

Hai giáo viên dùng cùng browser và cùng học sinh có thể đọc/export ghi chú local của nhau. UUID học sinh giảm nhầm giữa các trung tâm nhưng không giải quyết người viết trong cùng trung tâm. Không phát hiện API đọc ghi chú trái quyền trong đợt này.

**Đề xuất:** persist ghi chú qua server với author/scope, hoặc tối thiểu namespace theo center+actor và chính sách cleanup. Test logout/login hai giáo viên trên một browser.

### F21 — P3 — Dashboard học sinh có số liệu đề xuất hardcode

[StudentDashboardPage.tsx](../../web/edutwin-web/src/pages/StudentDashboardPage.tsx) khoảng 178/182 hiển thị “Dự kiến 25 phút”, “3 câu hỏi” bằng giá trị cố định; quan sát browser cũng thấy các nhãn này.

**Ảnh hưởng:** nhãn có thể không khớp số câu/thời lượng buổi thực tế. Lấy từ dữ liệu đề xuất hoặc ghi rõ đó là gợi ý chung; test buổi có số câu/thời lượng khác nhau.

## Điểm đã kiểm tra và không ghi thành lỗi

- Báo cáo lớp được Teacher/CenterManager truy cập và Student bị 403 trong dữ liệu audit.
- Tính điểm bài tập chuẩn hóa về thang 10; đã đối chiếu calculator trước khi đánh giá, không ghi nhầm khác biệt raw score là lỗi.
- Submit/draft API có chặn sai chủ sở hữu, sai tenant, câu ngoài bài, submit trước start, thay draft đã nộp; clock/deadline và duplicate submit đã qua 24 bài acceptance.
- Auth login/logout có clear query cache; không kết luận cache chung làm lộ dữ liệu tenant chỉ vì query key không chứa actor.

## Thứ tự sửa và nghiệm thu đề xuất

1. F01: sửa hydration/autosave, thêm browser regression bảo vệ dữ liệu đã lưu.
2. F02–F04: nối buổi với luyện tập; lọc/revalidate câu hỏi; xác định bằng chứng hoàn thành.
3. F05–F08: validation + ProblemDetails + concurrency + invariant tiến độ.
4. F10–F16: sửa parser và đồng bộ preview với schema/quyền/định dạng.
5. F17–F21: thống nhất nghĩa thống kê/export và scope ghi chú.

Kết quả test xanh hiện tại không phủ hết hành vi browser và ràng buộc MySQL. Không kết luận toàn hệ thống không còn lỗi; giới hạn và các luồng cần kiểm tra tiếp được ghi trong [báo cáo toàn hệ thống](SYSTEM-FLOW-CONSTRAINT-AUDIT-2026-10-11.md).
