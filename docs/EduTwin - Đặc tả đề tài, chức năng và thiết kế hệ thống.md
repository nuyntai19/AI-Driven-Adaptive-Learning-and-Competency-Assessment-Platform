# EDUTWIN – ĐẶC TẢ ĐỀ TÀI, CHỨC NĂNG VÀ THIẾT KẾ HỆ THỐNG

**Tên đề tài:** EduTwin – Nền tảng hỗ trợ học tập thích ứng, đánh giá năng lực và xây dựng hồ sơ học tập số cho học sinh.

## 1. Tổng quan đề tài

EduTwin hỗ trợ trung tâm giáo dục quản lý nội dung học, giao bài, thu nhận bài làm, phân tích cách giải và theo dõi trạng thái học tập theo từng chủ đề. Hệ thống kết hợp ba nguồn xử lý: chấm xác định bằng quy tắc, phân tích hỗ trợ bằng Gemini và xác nhận của giáo viên. Kết quả chỉ được đưa vào hồ sơ học tập sau khi đi qua cơ chế kiểm soát bằng chứng.

“Digital Twin” trong EduTwin không phải bản sao toàn diện của một con người. Đây là hồ sơ trạng thái học tập có lịch sử, gồm mức độ thành thạo theo chủ đề, chỉ số hành vi, mục tiêu, chỉ báo rủi ro và nguồn bằng chứng đã làm thay đổi hồ sơ.

Phiên bản hiện tại hướng tới trung tâm dạy và luyện thi THPT. Hệ thống có bốn loại tài khoản cố định: `Student`, `Teacher`, `CenterManager` và `PlatformAdmin`. Quyền thao tác cụ thể được cấu hình qua RBAC nhưng luôn bị giới hạn bởi loại tài khoản, trung tâm và phạm vi tài nguyên.

## 2. Bài toán thực tế

Điểm tổng của một bài kiểm tra không cho biết đầy đủ học sinh đang vướng ở chủ đề nào, sai do kiến thức hay do cách suy luận, và kết quả có ổn định qua nhiều lần học hay không. Giáo viên cũng khó đọc thủ công toàn bộ cách giải, ảnh nháp và lịch sử của nhiều học sinh.

EduTwin giải quyết bài toán bằng cách liên kết câu hỏi với cấu trúc kiến thức, lưu lượt làm cùng dữ liệu hành vi, phân tích cách giải ở tác vụ nền và duy trì hồ sơ theo thời gian. Giáo viên vẫn là người kiểm soát các trường hợp không chắc chắn. Các chỉ số như thời gian, mức tự tin hay tần suất bỏ qua chỉ là tín hiệu hỗ trợ; hệ thống không dùng chúng để kết luận tâm lý hoặc năng lực toàn diện của học sinh.

## 3. Mục tiêu hệ thống

- Quản lý trung tâm, tài khoản, lớp, môn, chương trình học và ngân hàng câu hỏi trong cùng một hệ thống nhiều tenant.
- Hỗ trợ tạo, xuất bản, làm và theo dõi bài tập theo lớp hoặc nhóm học sinh đã chọn.
- Lưu bài trước khi xử lý AI, chống nộp trùng và giữ được nguồn gốc của kết quả.
- Xây dựng hồ sơ học tập theo chủ đề từ bằng chứng đã được kiểm soát, có lịch sử trước/sau.
- Đề xuất nội dung học tiếp theo bằng quy tắc có thể giải thích và tạo kế hoạch học theo thời lượng người học cung cấp.
- Bảo đảm quyền, tenant và ownership được kiểm tra ở máy chủ, không chỉ bằng việc ẩn nút trên giao diện.

Hệ thống chưa được chứng minh là production-ready, chưa có tập dữ liệu đánh giá độ chính xác AI ở quy mô thực tế và không tích hợp mô hình ML.NET trong phiên bản hiện tại.

## 4. Đối tượng sử dụng

### 4.1 Student

Học sinh đăng nhập trong một trung tâm, xem dashboard, môn học, mục tiêu, bài được giao, kết quả, hồ sơ và gợi ý của chính mình. Khi làm bài, học sinh có thể trả lời trắc nghiệm, trả lời ngắn hoặc tự luận; nhập công thức bằng MathLive, xem công thức bằng KaTeX, dùng máy tính, bảng nháp, công cụ hình học và đồ thị cơ bản; khai báo mức tự tin; bỏ qua câu; đính kèm ảnh nháp; gửi yêu cầu giáo viên xem lại.

Nháp câu trả lời được lưu trong `localStorage`; nháp vector được lưu trong IndexedDB với khóa theo phạm vi và thời hạn. Đây là cơ chế khôi phục cục bộ, không phải lưu nháp phía máy chủ hay chế độ offline đầy đủ. Học sinh chỉ truy cập tài nguyên gắn với hồ sơ Student của mình và bài mà mình nằm trong danh sách người nhận.

### 4.2 Teacher

Giáo viên quản lý nội dung trong phạm vi quyền và lớp được phân công: môn, cấu trúc kiến thức, chương trình, câu hỏi, bài tập, người nhận, tiến độ và kết quả. Giáo viên có thể xem hàng chờ review, xác nhận hoặc điều chỉnh kết quả kèm lý do, xem lịch sử review và duyệt kết quả cuối của bài.

Quyền RBAC là điều kiện cần nhưng chưa đủ. Với dữ liệu lớp và học sinh, máy chủ còn kiểm tra giáo viên có phụ trách lớp chứa tài nguyên hay không. Luồng review hiện chỉ chấp nhận tài khoản có loại `Teacher`; quản lý trung tâm có permission liên quan cũng không tự động trở thành người review. Lượt luyện tập tự do không thuộc assignment không nằm trong phạm vi review của giáo viên theo guard hiện tại.

### 4.3 CenterManager

Quản lý trung tâm quản trị metadata của trung tâm, giáo viên, học sinh, lớp, môn, thành viên lớp, vòng đời tài khoản, đặt lại mật khẩu, vai trò, quyền và gán vai trò trong chính tenant. Hệ thống kiểm tra sự tương thích giữa permission và loại tài khoản, quyền mà người quản lý được phép phân phối, hành vi tự nâng quyền, thay đổi đồng thời và nguy cơ loại bỏ người quản trị cuối cùng.

Việc vô hiệu hóa giáo viên hoặc học sinh hiện cập nhật trạng thái tài khoản nhưng không tăng `AuthVersion` và không thu hồi refresh token trong cùng use case. Refresh về sau sẽ bị từ chối do trạng thái tài khoản, nhưng access token đã phát hành có thể tiếp tục dùng đến khi hết hạn. Vì vậy vòng đời activate/deactivate được xếp “Triển khai một phần”.

### 4.4 PlatformAdmin

PlatformAdmin thuộc tenant dành riêng `PLATFORM`. Vai trò này quản lý danh sách trung tâm, tạo và sửa metadata, đình chỉ/kích hoạt lại trung tâm, tạo CenterManager đầu tiên, quản lý CenterManager, chuyển người quản lý chính, đặt lại mật khẩu, khóa/mở tài khoản, xem nhật ký nền tảng, đổi mật khẩu bản thân, xem số phiên đang hoạt động và thu hồi toàn bộ phiên.

Các claim được kiểm tra để PlatformAdmin chỉ hợp lệ trong tenant `PLATFORM` và tài khoản thuộc tenant này cũng phải là PlatformAdmin. Các endpoint học thuật và guard theo tenant không cho vai trò nền tảng đọc attempt, đáp án, attachment, AI reasoning, review queue hoặc Digital Twin của trung tâm. Repository có mã kiểm thử cho ranh giới này, nhưng kiểm thử backend chưa chạy được trong lần đối chiếu do máy không có .NET SDK 10.

Giao diện thống kê nền tảng hiện cộng số học sinh, giáo viên, lớp và quản lý từ danh sách trung tâm của **trang đang tải**, trong khi API chỉ trả tổng số trung tâm toàn cục và số liệu từng trung tâm. Do đó các con số cộng này không phải thống kê toàn hệ thống khi có nhiều trang. Quản lý phiên hiện chỉ có số lượng phiên và thao tác thu hồi tất cả, chưa có danh sách chi tiết hoặc thu hồi từng phiên.

## 5. Đặc tả chức năng hệ thống

Mỗi mục dưới đây trình bày mục đích, luồng xử lý, quy tắc, dữ liệu, lỗi và trạng thái thực tế. “Đã triển khai” không đồng nghĩa đã được kiểm định tải, bảo mật và vận hành sản xuất.

### 5.1 Xác thực và tài khoản

**Mục đích và đối tượng.** Cung cấp đăng nhập, làm mới token và đăng xuất cho bốn loại tài khoản; quản lý mật khẩu và phiên theo nghiệp vụ được cấp quyền.

**Luồng và quy tắc.** Khi đăng nhập, máy chủ xác định trung tâm, kiểm tra trung tâm và người dùng đang hoạt động, xác minh mật khẩu đã băm, nạp vai trò/quyền rồi phát JWT và refresh token. Refresh token chỉ được lưu dạng băm và được xoay vòng trong transaction; logout có tính idempotent. JWT chứa định danh người dùng, trung tâm, loại tài khoản/vai trò và `AuthVersion`. Middleware so sánh `AuthVersion` với bản ghi hiện tại để từ chối token đã bị vô hiệu hóa bởi các thao tác có tăng phiên bản.

**Phân quyền, dữ liệu và lỗi.** Claim bắt buộc phải xuất hiện đúng một lần; claim thiếu, lặp hoặc sai quan hệ PLATFORM bị từ chối. Login xử lý sai trung tâm, sai mật khẩu, người dùng hoặc trung tâm không hoạt động bằng lỗi xác thực phù hợp. Reset mật khẩu và xóa tài khoản tăng `AuthVersion` và thu hồi refresh token.

**Trạng thái và giới hạn.** Đăng nhập/refresh/logout: **Đã triển khai**. Vô hiệu hóa/kích hoạt tài khoản: **Triển khai một phần** vì use case cập nhật Teacher/Student chưa chủ động hủy access token đang còn hạn. Chưa có MFA, khóa theo số lần đăng nhập sai hoặc danh sách phiên chi tiết.

### 5.2 Tổ chức và Tenant

**Mục đích và đối tượng.** Tách dữ liệu của từng trung tâm; CenterManager quản trị tenant của mình, PlatformAdmin quản trị vòng đời trung tâm mà không được kế thừa quyền học thuật.

**Luồng và quy tắc.** `DbContext` áp dụng global query filter theo `CenterId`; các aggregate có vòng đời còn lọc `IsDeleted`. Nếu không xác định được tenant, ngữ cảnh dùng `Guid.Empty` để đóng quyền truy cập thay vì mở rộng. Use case tiếp tục kiểm tra liên kết cùng trung tâm trước khi tạo quan hệ.

**Dữ liệu và lỗi.** Các bảng nghiệp vụ mang `CenterId`; khóa ngoại, unique index và kiểm tra ở application layer ngăn liên kết chéo tenant. Tài nguyên không thuộc tenant thường được trả như không tìm thấy hoặc forbidden tùy contract để tránh rò rỉ thông tin.

**Trạng thái và giới hạn.** **Đã triển khai** cho đường truy cập đã kiểm tra. Đây là cơ chế phòng vệ nhiều lớp, chưa phải bằng chứng kiểm thử xâm nhập toàn diện.

### 5.3 Quản lý giáo viên

**Mục đích và chức năng.** CenterManager có quyền tạo, cập nhật, tra cứu, đặt lại mật khẩu và quản lý trạng thái giáo viên; giáo viên được gán lớp và vai trò phù hợp.

**Luồng, quy tắc và lỗi.** Máy chủ kiểm tra tenant, email/username, loại tài khoản, bản ghi Teacher và xung đột dữ liệu. Tài nguyên khác tenant không được cập nhật. Xóa hoặc reset mật khẩu có xử lý vô hiệu hóa phiên; thay đổi trạng thái qua cập nhật chưa làm vậy.

**Dữ liệu.** `users`, `teachers`, `roles`, `user_roles`, `refresh_tokens`, lớp do giáo viên phụ trách và audit phân quyền liên quan.

**Trạng thái và giới hạn.** CRUD và reset mật khẩu: **Đã triển khai**. Activate/deactivate và session invalidation: **Triển khai một phần**.

### 5.4 Quản lý học sinh

**Mục đích và chức năng.** CenterManager quản lý tài khoản/hồ sơ học sinh; học sinh xem và thay đổi dữ liệu được phép của chính mình. Thành viên lớp quyết định nhiều phạm vi bài tập và phân tích.

**Luồng, quy tắc và lỗi.** Tạo/cập nhật kiểm tra tenant, định danh, trạng thái và dữ liệu hồ sơ. Gán học sinh vào lớp kiểm tra cả hai bản ghi cùng tenant và tránh liên kết trùng. Student ownership được dùng tại các use case bài tập, attempt, review request, twin, recommendation và learning plan.

**Dữ liệu.** `users`, `students`, `class_students`, `student_assignment_progress` và các bảng hồ sơ học tập.

**Trạng thái và giới hạn.** CRUD, membership và ownership: **Đã triển khai**. Activate/deactivate có cùng giới hạn token như giáo viên nên phần này **Triển khai một phần**.

### 5.5 Quản lý lớp và môn

**Mục đích và chức năng.** Tổ chức học sinh, giáo viên, môn và phạm vi giao bài. CenterManager quản lý lớp/môn; giáo viên thao tác học thuật khi có permission và quan hệ phụ trách.

**Luồng và quy tắc.** Tạo/cập nhật kiểm tra tenant, trạng thái, môn, giáo viên và thành viên. Khi bài tập được xuất bản, hệ thống chụp danh sách người nhận vào `assignment_targets`; việc thay đổi thành viên lớp sau đó không làm thay đổi lịch sử bài đã giao.

**Lỗi và trạng thái.** Xử lý null, không tìm thấy, liên kết khác tenant và xung đột cập nhật. Chức năng chính: **Đã triển khai**.

### 5.6 RBAC

**Mục đích và đối tượng.** Cho phép CenterManager gom permission thành role và gán role cho tài khoản trong tenant; quyền nền tảng được quản lý riêng.

**Luồng và quy tắc.** Khi tạo/sửa role, hệ thống kiểm tra permission tồn tại, đang hoạt động, tương thích account type, có được ủy quyền phân phối và không tạo self-escalation. Khi gán/bỏ role, hệ thống kiểm tra tenant, trạng thái, optimistic concurrency và bảo vệ quản trị viên cuối cùng. Permission nền tảng không thể được phân phối cho vai trò trung tâm. Quyền hiệu lực được đánh giá từ user-role, role và permission đều đang hoạt động.

**Dữ liệu, lỗi và audit.** `permissions`, `permission_account_types`, `roles`, `role_permissions`, `user_roles`, `authorization_audit_logs`. Thay đổi có lý do và lịch sử. Xung đột phiên bản trả conflict thay vì ghi đè im lặng; thay đổi quyền làm tăng `AuthVersion` ở các luồng tương ứng.

**Trạng thái và giới hạn.** **Đã triển khai**. Audit này tập trung vào phân quyền, không phải nhật ký đầy đủ mọi hành động nghiệp vụ.

### 5.7 Knowledge Graph

**Mục đích và đối tượng.** Mô tả chương, bài, chủ đề, khái niệm và quan hệ tiên quyết/liên quan/phân cấp để gắn câu hỏi và xác định readiness.

**Luồng và quy tắc.** Giáo viên hoặc quản lý có quyền tạo node và edge trong môn thuộc tenant. Hệ thống ngăn tự trỏ, liên kết sai môn/phạm vi và chu trình đối với loại quan hệ cần cấu trúc không vòng lặp.

**Dữ liệu, lỗi, trạng thái.** `knowledge_nodes`, `knowledge_edges`. Có xử lý không tìm thấy, trùng quan hệ, cross-tenant và cycle. **Đã triển khai**; chất lượng graph phụ thuộc dữ liệu do trung tâm xây dựng.

### 5.8 Curriculum

**Mục đích và đối tượng.** Tổ chức các node kiến thức theo thứ tự/trọng số, gán chương trình cho lớp và làm phạm vi cho recommendation.

**Luồng và quy tắc.** Tạo curriculum, thêm node, gán lớp, kiểm tra cùng môn/tenant và xuất bản khi đủ điều kiện. Recommendation yêu cầu phạm vi curriculum rõ ràng; nếu có hơn một curriculum đang hoạt động phù hợp thì dừng thay vì tự chọn mơ hồ.

**Dữ liệu, lỗi, trạng thái.** `curriculums`, `curriculum_nodes`, `curriculum_classes`. **Đã triển khai**; chưa có cơ chế tự hòa giải nhiều curriculum hoạt động.

### 5.9 Question Bank

**Mục đích và đối tượng.** Giáo viên quản lý câu hỏi trắc nghiệm, trả lời ngắn và tự luận theo môn/chủ đề.

**Luồng và quy tắc.** Câu hỏi `Draft` được sửa và kích hoạt; câu `Active` không sửa trực tiếp nhưng có thể archive; câu `Archived` có thể kích hoạt lại. Câu hỏi có topic chính và các liên kết bổ sung. Import thực hiện bước preview rồi confirm, kiểm tra cấu trúc và dữ liệu trước khi ghi.

**Dữ liệu và lỗi.** `questions`, `question_options`, `question_knowledge_nodes`. Kiểm tra tenant, môn, node, loại câu, evaluation mode, đáp án và trạng thái. Câu đã archive vẫn có thể được đọc trong bài đã xuất bản để bảo toàn lịch sử.

**Trạng thái và giới hạn.** CRUD, lifecycle, CSV/XLSX import: **Đã triển khai**. Giao diện cho phép chọn `.tsv` nhưng backend chỉ xử lý `.csv`, `.txt`, `.xlsx`; TSV được xếp **Triển khai một phần** cho đến khi hai phía thống nhất.

### 5.10 Assignment

**Mục đích và đối tượng.** Giáo viên tạo bài nháp, chọn câu hỏi, lớp/người nhận, điểm, hạn và thời lượng; sau đó publish hoặc close.

**Luồng và quy tắc.** Tạo bài chỉ dành cho Teacher/CenterManager có quyền; kiểm tra lớp hoạt động, môn, giáo viên phụ trách, tiêu đề, hạn tương lai, thời lượng dương, target mode, danh sách không trùng và câu hỏi active cùng môn. Publish chỉ từ draft, yêu cầu row version, có câu hợp lệ; transaction chụp người nhận và tạo tiến độ từng học sinh. Whole-class lấy thành viên hoạt động tại thời điểm publish; selected-target kiểm tra từng học sinh là thành viên hợp lệ.

**Dữ liệu và lỗi.** `assignments`, `assignment_questions`, `assignment_targets`, `student_assignment_progress`. Xử lý invalid, forbidden, not found, conflict và ghi đồng thời bằng optimistic concurrency.

**Trạng thái và giới hạn.** Tạo/publish/close và snapshot người nhận: **Đã triển khai**. Giới hạn thời gian làm bài: **Triển khai một phần** vì server chưa chặn submit theo `StartedAt + TimeLimitMinutes`; server chỉ kiểm hạn nộp với khoảng ân hạn 120 giây. Timer/pause hiện do frontend quản lý nên có thể bị bỏ qua.

### 5.11 Không gian làm bài

**Mục đích và đối tượng.** Cho Student mở bài được giao, nhập đáp án/cách giải, dùng công cụ toán, lưu nháp và gửi từng câu.

**Luồng và quy tắc.** Máy chủ kiểm tra Student/User đang hoạt động, ownership, assignment đã publish, còn trong hạn, câu thuộc bài, trạng thái câu và yêu cầu reasoning/độ dài. Giao diện đóng băng payload khi gửi, dùng `clientSubmissionId` có phạm vi và chỉ xóa nháp sau trạng thái thành công cuối; sau khi gửi sẽ poll kết quả trong giới hạn.

**Chấm và chống gửi trùng.** Chấm xác định xử lý loại có đáp án/rule phù hợp và lưu `IsCorrect`, `AwardedScore`, reason code; tự luận hoặc trường hợp manual có thể chưa có kết luận. Unique key `(CenterId, StudentId, ClientSubmissionId)` chống tạo trùng. Gửi lại cùng ID và cùng payload trả kết quả cũ; cùng ID nhưng payload khác trả lỗi duplicate/conflict. Transaction lưu attempt, job AI và trạng thái liên quan; race ở unique được bắt và đối chiếu lại.

**Công cụ toán và attachment.** MathLive dùng để nhập, KaTeX để hiển thị; máy tính hỗ trợ số học, căn, lũy thừa, logarit, lượng giác; scratchpad/hình học có thể xuất PNG; grapher chỉ hỗ trợ biểu thức cơ bản. Attachment dùng token được bảo vệ, promote file và lưu metadata. File system không cùng transaction ACID với MySQL; code có bù trừ khi rollback và worker dọn orphan, nhưng vẫn tồn tại cửa sổ crash.

**Telemetry.** Lưu thời gian, confidence, số lần đổi đáp án, skip và trạng thái đã xem feedback. Đây là dữ liệu hỗ trợ, không tự quyết định đúng/sai.

**Trạng thái và giới hạn.** Submit, idempotency, telemetry, attachment và công cụ cơ bản: **Đã triển khai**. Server-side timer, grapher, offline và server-side draft: **Triển khai một phần/chưa triển khai** tương ứng.

### 5.12 AI Analysis

**Mục đích.** Phân tích cách giải và ảnh nháp sau khi attempt đã được lưu; không chặn việc bảo toàn bài làm.

**Luồng và dữ liệu vào.** Worker nhận job theo lease, lấy câu hỏi, evaluation mode, nội dung/đáp án chuẩn, lời giải/rubric, câu trả lời và biểu thức chuẩn hóa, kết quả chấm sơ bộ, reasoning, thời gian, confidence, số lần đổi đáp án, node được phép và byte ảnh. Câu bị skip không gọi Gemini mà dùng kết quả quy tắc riêng.

**Đầu ra và validation.** Schema gồm phiên bản, ngôn ngữ, phương pháp, chất lượng suy luận, loại lỗi, misconception, bước thiếu, root-cause node, confidence, feedback, solution type và lời giải gợi ý. Parser kiểm tra schema, enum, khoảng giá trị, chuỗi và danh sách node cho phép. Prompt yêu cầu AI không thay kết quả chấm xác định.

**Retry/fallback.** Timeout mặc định 30 giây và bị giới hạn tối đa 120 giây. Provider có thể xoay API key; job dùng lease, retry/backoff rồi chuyển fallback. Khi đã có đúng/sai xác định, fallback tạo bằng chứng trọng số thấp và yêu cầu review; khi chưa có đúng/sai thì không đổi mastery. Lỗi ảnh có đường retry và trạng thái cuối khác nhau giữa assignment và luyện tập tự do.

**Trạng thái và giới hạn.** Worker, schema, validation, retry và fallback: **Đã triển khai**. Chưa có benchmark giáo viên đủ lớn để kết luận độ chính xác; AI không phải người chấm cuối.

### 5.13 Evidence Gate

**Mục đích.** Quyết định bằng chứng có được dùng để cập nhật mastery hay chỉ lưu để review.

**Nguồn và chính sách hiện tại.** AI không hợp lệ, mâu thuẫn, bất thường, thiếu kết luận đúng/sai hoặc confidence dưới 50 được đưa vào `ReviewOnly`, trọng số 0. AI hợp lệ từ 50 đến dưới 80 dùng trọng số 0,50; nếu đã xem feedback là 0,25. AI hợp lệ từ 80 trở lên dùng 1,00; sau feedback là 0,35. Rule fallback có đúng/sai dùng 0,30; sau feedback là 0,20 và vẫn cần review. Teacher approval/override hợp lệ dùng 1,00; sau feedback là 0,50. Skip, chưa xác định đúng/sai và câu đã void không làm thay đổi mastery.

**Ví dụ.** Một câu được chấm đúng, AI hợp lệ với confidence 72 và học sinh chưa xem phản hồi sẽ tạo bằng chứng trọng số 0,50. Nếu AI mâu thuẫn với kết quả xác định, trọng số chuyển về 0 và giáo viên phải xem lại; confidence không được coi là xác suất đúng.

**Dữ liệu, replay và lỗi.** Quyết định được lưu ở `evidence_assessments`. Teacher review tạo bằng chứng kế tiếp thay vì sửa mất dấu nguồn cũ. Khi override một kết quả trước đây, hệ thống phát lại chuỗi attempt theo thời gian cho học sinh/topic để tính lại hồ sơ.

**Trạng thái và giới hạn.** **Đã triển khai** theo policy hiện tại. Các ngưỡng là quy tắc sản phẩm, chưa được chứng minh sư phạm bằng tập dữ liệu thực nghiệm.

### 5.14 Teacher Review

**Mục đích và đối tượng.** Cho Teacher xử lý kết quả không chắc chắn và duyệt kết quả toàn bài.

**Luồng và quy tắc.** Guard yêu cầu tài khoản Teacher, assignment/target thuộc tenant và lớp do giáo viên phụ trách. Giáo viên đọc answer, reasoning, attachment và analysis; có thể approve hoặc override. Override bắt buộc lý do/feedback và điểm trong giới hạn; `OverrideVersion` chống ghi đè đồng thời. Final review chỉ thành công khi các latest attempt cần thiết đã hoàn tất và không còn `Pending`, `Processing`, `AnalysisFailed` hoặc `NeedsTeacherReview`; `FinalReviewVersion` xử lý concurrency.

**Review request.** Student có thể tạo yêu cầu xem lại cho tài nguyên của mình. Nếu đã có yêu cầu pending thì thao tác có tính idempotent; hệ thống đánh dấu attempt/analysis và nối thêm evidence thay vì sửa lịch sử.

**Side effects và lỗi.** Evidence, review và replay twin nằm trong transaction của thao tác chính. Sinh lại recommendation/nhận xét tổng hợp được gọi best-effort sau commit; lỗi không rollback review nên có thể tạo trạng thái tạm thời chưa đồng bộ.

**Trạng thái và giới hạn.** Review, override, history và final review: **Đã triển khai** cho assignment. Free-practice attempt không thuộc assignment chưa review được qua luồng hiện tại.

### 5.15 Digital Twin

**Mục đích.** Duy trì hồ sơ học tập có nguồn gốc và lịch sử, không đồng nhất với điểm bài.

**Knowledge/Mastery.** Mỗi topic có mastery và số bằng chứng. Cập nhật dùng hướng đúng/sai, độ mạnh bằng chứng `0,65 + 0,20 × reasoning + 0,10 × time + 0,05 × confidence calibration`, learning rate 0,25 khi trọng số lớn hơn 0 và hệ số độ khó 0,85/0,925/1/1,075/1,15. Trọng số 0 giữ nguyên mastery. Luồng hiện tại chỉ cập nhật topic chính của câu.

**Behavior.** Hồ sơ hành vi tổng hợp thời gian, confidence, skip, thay đổi đáp án và các thống kê theo môn từ attempt đã hoàn tất. Các giá trị này là mô tả hành vi quan sát được.

**Goal/Risk.** Học sinh đặt mục tiêu theo môn. Hệ thống duy trì chỉ báo mục tiêu/rủi ro bằng rule dựa trên dữ liệu hiện có; đây không phải mô hình dự báo điểm đã kiểm định.

**History.** `twin_update_history` lưu nguồn, giá trị trước/sau, lý do và thời điểm. Teacher override chạy replay theo thứ tự để tránh chỉ sửa điểm cuối mà bỏ qua ảnh hưởng lịch sử.

**Trạng thái và giới hạn.** Knowledge Twin, Behavior Twin, goal/risk và history: **Đã triển khai**. Tiến độ `CompletedQuestionCount` hiện được tăng trong orchestrator khi AI/fallback hoàn tất; job kết thúc `AnalysisFailed` có thể khiến bài vẫn `InProgress` dù tất cả answer đã được nhận. Đây là điểm **Triển khai một phần** của trạng thái tiến độ.

### 5.16 Recommendation

**Mục đích.** Chọn chủ đề/câu hỏi tiếp theo bằng quy tắc có thể giải thích.

**Luồng và thuật toán.** Hệ thống xác định curriculum duy nhất trong phạm vi. Nếu tất cả mastery từ 80 trở lên, sinh gợi ý ôn duy trì. Opportunity Gap chỉ dùng khi có ít nhất ba bằng chứng có hiệu lực, có candidate hợp lệ và có mẫu reasoning. Topic có prerequisite chỉ sẵn sàng khi mọi prerequisite đạt ít nhất 60; candidate chính có mastery dưới 80.

Giá trị cơ sở được tính ở mức báo cáo như sau: `ExpectedGain = (1 - Mastery/100) × ExamImportance`; xác suất ước lượng `= 0,20 + 0,60 × ReasoningQuality + 0,20 × PrerequisiteReadiness`; điểm thô `= ExpectedGain × Probability / max(EstimatedHours, 0,5)` rồi chuẩn hóa tương đối về 0–100. Đây là điểm xếp hạng, không phải cam kết tăng điểm thi.

Nếu chưa đủ điều kiện Opportunity Gap, Linear Fallback chọn tối đa năm topic sẵn sàng theo thứ tự curriculum; nếu không có topic sẵn sàng thì trả trạng thái blocked. Accept/dismiss có idempotency và kiểm tra concurrency. Dismiss thúc đẩy mục tiếp theo đã lưu trong snapshot, không tính lại toàn bộ tại thời điểm bấm.

**Dữ liệu, lỗi, trạng thái.** `recommendations`, `recommendation_generation_states`, mastery/curriculum liên quan. Sinh recommendation sau AI hoặc review là best-effort ngoài transaction chính nên có thể tạm chậm so với Twin. Chức năng chính: **Đã triển khai**, đồng bộ tức thời tuyệt đối: **chưa bảo đảm**.

### 5.17 Learning Plan

**Mục đích.** Chuyển mục tiêu và quỹ thời gian thành kế hoạch tuần/buổi có thể theo dõi.

**Luồng và quy tắc.** Input kiểm tra số tuần 1–52, phút/ngày 10–180, ngày/tuần 1–7 và target mastery 40–100. Logic xác định sinh phase, tuần, session, topic và câu hỏi; Gemini chỉ biên tập mục tiêu, hoạt động và lời giải thích, không được đổi ID, thời lượng, topic hoặc question. Khi AI lỗi, hệ thống giữ kế hoạch xác định.

**Dữ liệu và lỗi.** `student_learning_path_preferences`, `learning_paths`, `learning_path_items`, recommendation và curriculum. Một điểm chưa nguyên tử là preference được lưu trước, sau đó mới sinh recommendation/path và cập nhật `PlanJson`; không có transaction bao trùm toàn bộ nên lỗi giữa chừng có thể để preference mới đi cùng path cũ hoặc chưa có path.

**Trạng thái và giới hạn.** Tạo và theo dõi plan: **Triển khai một phần**. Cần transaction/outbox hoặc trạng thái workflow rõ ràng, đồng thời mở rộng nội dung học thực tế cho từng session.

### 5.18 Dashboard

**Mục đích.** Cung cấp tổng quan phù hợp từng actor: bài/tiến độ của Student, lớp và review của Teacher, tổ chức của CenterManager, trung tâm của PlatformAdmin.

**Luồng và phân quyền.** API tổng hợp dữ liệu sau khi áp tenant/ownership; frontend xử lý loading, empty và error. Dashboard không phải nguồn cấp quyền, mọi thao tác tiếp theo vẫn được backend kiểm tra.

**Trạng thái và giới hạn.** Dashboard học thuật chính: **Đã triển khai** theo các endpoint hiện có. Thống kê tổng hợp PlatformAdmin: **Triển khai một phần** vì giao diện đang cộng dữ liệu của trang hiện tại. Chưa có kho dữ liệu analytics, báo cáo tải lớn hoặc kiểm chứng hiệu năng.

### 5.19 Platform Administration

**Mục đích.** Quản lý vòng đời tenant và CenterManager ở cấp nền tảng, đồng thời giữ ranh giới với dữ liệu giáo dục.

**Luồng và quy tắc.** Tạo center và quản lý đầu tiên dùng transaction/khóa phù hợp; suspend/reactivate kiểm tra trạng thái; chuyển Primary Manager bảo vệ tính duy nhất và người quản lý cuối cùng; reset mật khẩu/thu hồi phiên làm tăng khả năng vô hiệu hóa credential; hành động quan trọng tạo platform audit. Các thao tác đồng thời dùng row version hoặc khóa DB ở điểm cần thiết.

**Dữ liệu và lỗi.** `centers`, `users`, `refresh_tokens`, role/audit. Xử lý not found, trạng thái không hợp lệ, xung đột và invariant primary manager. Rate limit hiện được cấu hình cho nhóm endpoint bảo mật nền tảng ở mức 5 yêu cầu/phút, không phải rate limit tổng quát cho toàn bộ login.

**Trạng thái và giới hạn.** Quản lý center/manager, primary manager, audit, đổi mật khẩu và revoke-all: **Đã triển khai**. Thống kê toàn cục và quản lý từng phiên: **Triển khai một phần**. Ranh giới từ chối dữ liệu học thuật có test source nhưng cần chạy lại backend test.

## 6. Các quy trình nghiệp vụ chính

### Luồng 1 – Giáo viên tạo và giao bài

**Giáo viên nhập bài nháp → máy chủ kiểm tra quyền, tenant, lớp phụ trách, môn và câu hỏi → lưu assignment → giáo viên chọn WholeClass hoặc học sinh cụ thể → publish với row version → transaction chụp người nhận và tạo progress → học sinh nhìn thấy bài.**

Nếu câu hỏi bị archive, khác môn, lớp không hoạt động, người nhận không thuộc lớp hoặc dữ liệu thay đổi đồng thời, publish bị từ chối và không tạo nửa vời danh sách người nhận.

### Luồng 2 – Học sinh mở và làm bài

**Student mở danh sách → API lọc đúng target → mở chi tiết → có thể ghi `StartedAt/InProgress` → frontend khôi phục nháp → học sinh nhập answer, reasoning, confidence và dùng công cụ toán.**

Get detail kiểm tra publish, target, trạng thái và thời hạn. Tuy nhiên start use case chưa tự kiểm đầy đủ trạng thái/hạn trước khi ghi và thời lượng riêng của bài chưa được server cưỡng chế; đây là giới hạn cần sửa.

### Luồng 3 – Học sinh submit từng câu/bài

**Frontend đóng băng payload và clientSubmissionId → API validation → kiểm tra Student/ownership/target/question/due date → deterministic grading → transaction lưu attempt + AI job + progress → promote attachment → trả trạng thái nhận bài → frontend poll.**

Gửi lại cùng ID/cùng nội dung trả kết quả đã có; khác nội dung bị conflict. Lỗi AI sau đó không xóa attempt. Lỗi file/DB có bù trừ nhưng storage và DB không phải một transaction phân tán.

### Luồng 4 – Deterministic grading

**Chuẩn hóa answer → chọn grader theo question/evaluation mode → so với option/canonical answer/rule → tạo IsCorrect, AwardedScore và reason code → giữ null nếu không thể chấm xác định.**

AI không được ghi đè kết quả xác định này. Tự luận/manual có thể phải chờ giáo viên.

### Luồng 5 – Tạo AI job

**Attempt được kiểm tra → tạo job pending trong cùng transaction → commit attempt và job → worker nhận sau.**

Cách này tránh mất bài khi Gemini chậm hoặc lỗi. Unique/idempotency tránh tạo job mới khi cùng submission được phát lại.

### Luồng 6 – Background worker xử lý AI

**Worker lease job → tải attempt/question/attachment → gọi Gemini với timeout → parse/validate schema → lưu analysis → retry/backoff khi lỗi → fallback hoặc trạng thái lỗi cuối → gọi orchestrator hoàn tất.**

Lease giảm xử lý đồng thời cùng job; retry không bảo đảm dịch vụ ngoài sẽ thành công. AnalysisFailed cuối có thể làm progress chưa hoàn tất như đã nêu.

### Luồng 7 – Evidence Gate

**Analysis/rule/teacher evidence → kiểm tra nguồn, đúng-sai, contradiction, anomaly, confidence và feedback-viewed → chọn Applied/ReviewOnly và trọng số → lưu evidence assessment.**

Thiếu kết luận đúng/sai, skip hoặc evidence không hợp lệ có trọng số 0.

### Luồng 8 – Cập nhật Mastery/Digital Twin

**Evidence hợp lệ → tính evidence strength và hệ số độ khó → cập nhật mastery topic chính → cập nhật behavior/goal-risk liên quan → ghi before/after vào twin history → sau commit thử sinh recommendation.**

Lỗi recommendation không rollback Twin. Khi trọng số 0, mastery giữ nguyên dù evidence vẫn được lưu.

### Luồng 9 – Teacher Review

**Teacher mở queue → guard kiểm tra assignment/class ownership → xem answer/ảnh/AI → approve hoặc yêu cầu override → transaction lưu evidence/review và cập nhật Twin → ghi history → thử làm mới recommendation.**

Tài nguyên ngoài lớp hoặc ngoài tenant bị từ chối; tài khoản không phải Teacher không đi qua luồng review hiện tại.

### Luồng 10 – Teacher Override và replay

**Teacher nhập kết luận/điểm/feedback/lý do + OverrideVersion → kiểm tra giới hạn điểm và concurrency → nối evidence mới → lấy chuỗi attempt theo thời gian của student/topic → replay mastery → ghi lịch sử.**

Version cũ trả conflict. Replay bảo đảm thay đổi một bằng chứng cũ được phản ánh vào các bước sau, thay vì chỉ sửa số cuối.

### Luồng 11 – Tạo recommendation

**Xác định curriculum → kiểm tra mastery/evidence/prerequisite → chọn Maintenance, Opportunity Gap hoặc Linear Fallback → xếp hạng → lưu snapshot → Student accept/dismiss với idempotency.**

Nhiều curriculum active hoặc không có topic ready được trả thành trạng thái rõ ràng, không tự đoán.

### Luồng 12 – Tạo Learning Plan

**Student gửi mục tiêu/quỹ thời gian → lưu preference → lấy recommendation/curriculum → sinh cấu trúc deterministic → tùy chọn gọi Gemini để biên tập text → validate không đổi cấu trúc → lưu path/items/PlanJson.**

AI lỗi thì dùng nội dung deterministic. Lỗi sau khi preference đã lưu có thể để dữ liệu tạm không đồng bộ vì chưa có transaction toàn luồng.

### Luồng 13 – CenterManager thay đổi Role/Permission

**Manager gửi role/permission + lý do + version → kiểm tra permission hiệu lực, account type, tenant, delegability, actor effective rights, self-escalation và last-admin → transaction cập nhật → ghi authorization audit → làm mất hiệu lực token theo cơ chế phiên bản ở luồng tương ứng.**

Thao tác vượt quyền, quyền nền tảng hoặc version cũ bị từ chối.

### Luồng 14 – PlatformAdmin tạo/suspend center

**PlatformAdmin hợp lệ trong PLATFORM tenant → tạo center + Primary CenterManager trong transaction hoặc chọn center hiện có → kiểm tra trạng thái/invariant → suspend/reactivate → ghi platform audit.**

Suspend chặn các lần xác thực/refresh về sau; hệ thống vẫn cần kiểm tra và hoàn thiện chiến lược vô hiệu hóa tức thời mọi access token liên quan.

## 7. Vai trò của AI

Gemini chỉ tham gia sau khi bài đã được máy chủ nhận và trong bước biên tập nội dung kế hoạch. AI không quyết định quyền truy cập, tenant, ownership, đáp án chuẩn, điểm cuối hoặc trực tiếp ghi mastery. Output phải qua parser, validation và Evidence Gate; giáo viên xử lý trường hợp không chắc chắn.

Pipeline AI có job status, lease, retry/backoff, timeout, API-key rotation và fallback. Dữ liệu gửi đi được giới hạn theo nhiệm vụ phân tích; attachment chỉ được đọc khi hợp lệ. Lời giải gợi ý và confidence của AI là dữ liệu hỗ trợ, không phải sự thật đã kiểm định. Nhận xét tổng hợp toàn bài hiện được ghép từ kết quả từng câu theo rule, không phải một lần gọi AI riêng.

## 8. Hồ sơ học tập số

Hồ sơ gồm bốn phần liên quan:

- **Knowledge/Mastery:** mức thành thạo và số evidence theo topic, thay đổi bằng chính sách có trọng số.
- **Behavior:** thời gian, confidence, skip, answer changes và thống kê quan sát theo môn.
- **Goal/Risk:** mục tiêu học sinh và chỉ báo rule-based về khoảng cách/rủi ro.
- **Twin History:** nguồn thay đổi, before/after, lý do và timestamp phục vụ truy vết/replay.

Điểm bài, reasoning quality và mastery là ba đại lượng khác nhau. Một bài đúng có thể cập nhật mastery khác nhau tùy độ khó, chất lượng evidence và việc đã xem feedback; một bài chưa có kết luận đúng/sai không làm tăng mastery chỉ vì AI đưa ra nhận xét.

## 9. Khuyến nghị học tập

Khuyến nghị dựa trên curriculum, mastery, prerequisite, importance, reasoning quality và thời gian dự kiến. Khi dữ liệu đủ, Opportunity Gap xếp hạng cơ hội tương đối; khi chưa đủ, Linear Fallback đi theo thứ tự curriculum; khi đã đạt các topic, hệ thống tạo maintenance review.

Accept/dismiss thay đổi trạng thái recommendation có kiểm tra lặp và đồng thời. Learning Plan dùng recommendation làm một đầu vào nhưng tiến độ hoàn thành session không tự chứng minh mastery. Điểm Opportunity Gap không phải dự đoán tăng bao nhiêu điểm thi và chưa được hiệu chỉnh bằng thử nghiệm sư phạm.

## 10. Phân quyền và Multi-Tenant

### 10.1 Phạm vi trung tâm và tài nguyên

Mỗi người dùng làm việc trong ngữ cảnh trung tâm của mình. Máy chủ kiểm tra cả quyền chức năng và phạm vi tài nguyên; việc ẩn nút trên giao diện chỉ hỗ trợ trải nghiệm, không thay thế kiểm tra tại máy chủ. Loại tài khoản là ranh giới cố định, còn RBAC chỉ phân phối các permission tương thích trong ranh giới đó.

Mỗi request cần có claim hợp lệ, người dùng và trung tâm đang hoạt động, permission hiệu lực và quyền trên tài nguyên cụ thể. Giáo viên còn bị giới hạn theo lớp hoặc học sinh phụ trách. Học sinh chỉ truy cập dữ liệu học tập của mình và các bài mà mình thuộc danh sách người nhận. CenterManager chỉ quản trị dữ liệu trong tenant hiện tại.

Quản lý trung tâm được cấu hình role trong danh mục quyền cho phép. Role phải phù hợp với loại tài khoản; quyền nền tảng không được phân phối cho tài khoản trung tâm thông thường. Khi thay đổi role hoặc permission, hệ thống kiểm tra quyền hiệu lực của người thực hiện, khả năng phân phối quyền, hành vi tự nâng quyền, xung đột phiên bản và nguy cơ làm mất người quản trị cuối cùng còn đủ quyền cần thiết.

PlatformAdmin hoạt động trong tenant hệ thống `PLATFORM`, quản lý trung tâm và tài khoản CenterManager nhưng không được dùng quyền nền tảng để đọc attempt, answer, attachment, AI reasoning, hàng chờ teacher review hoặc Digital Twin của trung tâm. Ranh giới này được thực hiện bằng claim, policy, endpoint và kiểm tra nghiệp vụ. Đây là cơ chế bảo vệ trong ứng dụng, không phải tuyên bố hệ thống an toàn tuyệt đối trước mọi phương thức truy cập ngoài ứng dụng.

### 10.2 Cơ chế cô lập dữ liệu

Global query filter của EF Core là lớp bảo vệ mặc định, còn use case/guard kiểm tra quan hệ nghiệp vụ. Các unique constraint, foreign key, row version, transaction và row lock bảo vệ trạng thái khi ghi. Không nên dựa vào việc giao diện ẩn nút: backend vẫn là nơi ra quyết định cuối.

## 11. Kiến trúc hệ thống

```text
React 19 + TypeScript + Vite
              ↓ HTTP/JSON
ASP.NET Core Web API (.NET 10)
              ↓
Application/BLL: use case, validation, authorization, orchestration
              ↓
Domain + Contracts: entity, policy, DTO và abstraction
              ↓
EF Core 10
              ↓
MySQL 8
```

- **Frontend:** định tuyến theo actor, form, trạng thái tải/lỗi/rỗng, client API, nháp cục bộ và công cụ toán.
- **API:** controller/endpoints, authentication, authorization, tenant context, mapping lỗi và rate limiting ở phạm vi được cấu hình.
- **BLL/Application:** quy tắc nghiệp vụ, scope guard, transaction, idempotency, Evidence Gate, replay, recommendation và learning plan.
- **Domain/Contracts:** mô hình dữ liệu, enum/trạng thái, hợp đồng giữa tầng và abstraction cho AI/storage.
- **Infrastructure:** EF Core/MySQL, migration, seed Development có kiểm soát, JWT, attachment storage, Gemini provider và background workers.
- **Triển khai:** Docker Compose gồm MySQL 8.0.35, API, web/nginx và Adminer; volume giữ dữ liệu, attachment và data-protection key. Đây là cấu hình triển khai/demo, chưa phải chứng nhận production.

## 12. Cơ sở dữ liệu

Migration/schema hiện có **43 bảng nghiệp vụ**; nếu tính bảng kỹ thuật `__EFMigrationsHistory` thì có **44 bảng vật lý**.

| Nhóm | Số bảng | Bảng chính |
| --- | ---: | --- |
| Tổ chức, tài khoản, lớp, môn | 8 | `centers`, `users`, `refresh_tokens`, `teachers`, `students`, `subjects`, `classes`, `class_students` |
| Kiến thức, curriculum, câu hỏi | 8 | `knowledge_nodes`, `knowledge_edges`, `curriculums`, `curriculum_classes`, `curriculum_nodes`, `questions`, `question_options`, `question_knowledge_nodes` |
| Assignment, attempt, AI, review | 11 | `assignments`, `assignment_questions`, `assignment_targets`, `student_assignment_progress`, `attempts`, `attempt_attachments`, `ai_analysis_jobs`, `reasoning_analyses`, `evidence_assessments`, `student_review_requests`, `teacher_review_histories` |
| Digital Twin, recommendation, plan | 10 | `student_subject_goals`, `student_twins`, `knowledge_twins`, `behavior_twins`, `twin_update_history`, `recommendations`, `recommendation_generation_states`, `learning_paths`, `learning_path_items`, `student_learning_path_preferences` |
| RBAC và audit | 6 | `permissions`, `permission_account_types`, `roles`, `role_permissions`, `user_roles`, `authorization_audit_logs` |

Ràng buộc tenant, khóa ngoại, unique key cho idempotency, row version và transaction được dùng ở các luồng ghi quan trọng. Attachment vật lý nằm ngoài MySQL nên được xử lý bằng promote/bù trừ/orphan cleanup, không có bảo đảm atomic tuyệt đối giữa file và row.

## 13. Công nghệ sử dụng

- Frontend: React 19, TypeScript 5.7, Vite 6, MathLive, KaTeX và các module đồ họa/canvas của dự án.
- Backend: C#, ASP.NET Core Web API và .NET 10.
- Dữ liệu: Entity Framework Core 10 và MySQL 8.
- AI: Google GenAI/Gemini qua provider có timeout, key rotation và worker nền.
- Xác thực: JWT bearer, refresh token băm, password hashing và ASP.NET Core Data Protection.
- Kiểm thử: xUnit ở backend; Vitest và Testing Library ở frontend.
- Vận hành local/demo: Docker Compose, nginx, Adminer, migration và seed chỉ được phép trong môi trường Development.

## 14. Bảo mật và yêu cầu phi chức năng

### 14.1 Phiên đăng nhập và nhật ký

Mật khẩu được lưu dưới dạng băm. Hệ thống sử dụng JWT và refresh token; refresh token được lưu dạng băm và xoay vòng trong transaction. JWT kiểm tra issuer, audience, chữ ký và thời hạn; khóa ký phải dài tối thiểu 32 byte và không được dùng giá trị placeholder. Seed data bị chặn ngoài môi trường Development.

`AuthVersion` cho phép làm mất hiệu lực access token cũ khi các luồng như đổi/reset mật khẩu, xóa tài khoản hoặc thay đổi quyền tăng phiên bản xác thực. Khi đình chỉ trung tâm, các yêu cầu tiếp theo vẫn phải đi qua kiểm tra trạng thái trung tâm, người dùng và phiên; đây không phải cơ chế đẩy thông báo tức thời tới mọi thiết bị. Riêng use case cập nhật trạng thái Teacher/Student hiện chưa tăng `AuthVersion` hoặc thu hồi refresh token ngay trong cùng thao tác, vì vậy access token đã phát hành có thể còn dùng được đến khi hết hạn. Giới hạn này được giữ trong bảng trạng thái và hướng phát triển.

Các thao tác quản trị quyền và nền tảng được lưu kèm người thực hiện, thời điểm và lý do theo nghiệp vụ. Lịch sử teacher review, evidence và Digital Twin giúp đối chiếu thay đổi kết quả. Không đồng nhất các lịch sử chuyên biệt này với việc hệ thống đã ghi audit cho mọi hành động.

### 14.2 Bảo mật và toàn vẹn dữ liệu

- **Xác thực và phân quyền:** policy/RBAC, account type, tenant, ownership và teacher scope được kết hợp. Permission động không vượt ranh giới loại tài khoản; PlatformAdmin bị tách khỏi API học thuật.
- **Toàn vẹn dữ liệu:** đầu vào, trạng thái và liên kết cùng trung tâm được kiểm tra; unique constraint hỗ trợ chống nộp trùng; optimistic concurrency, transaction và row lock được dùng ở các bước ghi phù hợp.
- **Idempotency và cạnh tranh ghi:** submit attempt, refresh/logout, review request và accept/dismiss có xử lý gửi lặp. Dữ liệu thay đổi giữa lúc đọc và ghi có thể trả conflict để người dùng tải lại thay vì ghi đè im lặng.
- **Riêng tư:** dữ liệu gửi tới AI được giới hạn theo nhiệm vụ phân tích. Không nên đưa secret hoặc nội dung học sinh không cần thiết vào log hay yêu cầu dịch vụ ngoài.

### 14.3 Chất lượng và giới hạn hiện tại

- **Khả dụng:** attempt được lưu trước khi gọi AI; worker có timeout, retry và fallback nên AI lỗi không xóa bài đã nhận. Bài chưa có kết luận đúng/sai vẫn có thể cần giáo viên xử lý. Progress có thể bị giữ ở `InProgress` khi job AI thất bại ở trạng thái cuối; recommendation hoặc nhận xét hậu xử lý cũng có thể chậm vì được gọi best-effort sau commit.
- **Khả năng giải thích:** hệ thống lưu nguồn bằng chứng, quyết định của Evidence Gate, lịch sử review, thay đổi trước/sau của Digital Twin và căn cứ của recommendation theo quy tắc.
- **Dễ sử dụng:** frontend có trạng thái loading, empty, error và chờ phân tích; MathLive, KaTeX, calculator, scratchpad và các công cụ toán giúp học sinh trình bày bài trong phạm vi hiện có. Grapher, autosave đa thiết bị và offline recovery chưa hoàn chỉnh.
- **Khả năng bảo trì và kiểm thử:** giao diện, contract, nghiệp vụ, domain, truy cập dữ liệu, AI provider và worker được tách thành các project hoặc module có trách nhiệm riêng. Frontend test đã chạy đạt; backend có mã kiểm thử nhưng chưa chạy được trong môi trường đối chiếu vì thiếu .NET SDK 10.
- **Khả năng mở rộng và vận hành:** kiến trúc có worker nền, migration, Docker và cơ chế retry, nhưng chưa có bằng chứng load/soak/chaos test, SLO, benchmark concurrency, diễn tập backup/restore hoặc observability production.

Hiệu năng khi có nhiều người dùng, độ chính xác AI, độ tin cậy sư phạm, kiểm thử xâm nhập và trải nghiệm trên nhiều thiết bị vẫn cần được đánh giá. Đây là các yêu cầu cần kiểm chứng, không được trình bày như kết quả hệ thống đã đạt.

## 15. Trạng thái triển khai hiện tại

| Nhóm chức năng | Trạng thái | Căn cứ và giới hạn chính |
| --- | --- | --- |
| Login, refresh rotation, logout | Đã triển khai | Có validation, token băm, transaction và idempotent logout. |
| Tenant isolation, ownership, RBAC | Đã triển khai | Có claim checks, global filters, guard và audit; vẫn cần chạy security suite. |
| CRUD giáo viên/học sinh/lớp/môn | Đã triển khai | Đường API–use case–EF–UI có thật. |
| Activate/deactivate tài khoản | Triển khai một phần | Chưa tăng AuthVersion/thu hồi refresh token ngay trong update use case. |
| Knowledge Graph và Curriculum | Đã triển khai | Có validation quan hệ/cycle và phạm vi curriculum. |
| Question Bank, CSV/XLSX import | Đã triển khai | TSV được UI nhận nhưng backend không hỗ trợ. |
| Tạo, publish, close assignment | Đã triển khai | Snapshot target và progress trong transaction. |
| Time limit bài tập | Triển khai một phần | Timer phía client; server chưa cưỡng chế StartedAt + TimeLimit. |
| Submit, deterministic grading, idempotency | Đã triển khai | Unique client ID, payload comparison và transaction. |
| Math input, calculator, scratchpad, geometry | Đã triển khai trong phạm vi cơ bản | Không phải Casio đầy đủ; draft chỉ lưu local. |
| Function Grapher | Triển khai một phần | Biểu thức và tính năng còn giới hạn. |
| AI worker, validation, retry/fallback | Đã triển khai | Chưa có benchmark độ chính xác; terminal failure ảnh hưởng progress. |
| Evidence Gate | Đã triển khai | Có policy trọng số, review-only và lịch sử. |
| Teacher review/override/final review | Đã triển khai cho assignment | Free practice chưa review; side effect sau commit là best-effort. |
| Digital Twin/mastery/behavior/history | Đã triển khai | Chỉ topic chính được cập nhật; progress còn phụ thuộc AI completion. |
| Recommendation/Opportunity Gap | Đã triển khai | Rule-based; làm mới sau commit có thể trễ. |
| Learning Plan | Triển khai một phần | Có tuần/buổi và fallback, nhưng preference/path chưa atomic. |
| Dashboard học thuật | Đã triển khai | Chưa có kiểm chứng tải/analytics quy mô lớn. |
| Platform Administration | Triển khai một phần | Lifecycle chính có; global stats và quản lý từng session chưa đầy đủ. |
| Mobile app, billing, ML.NET | Chưa triển khai / ngoài phạm vi | Không được coi là hạng mục bắt buộc của phiên bản hiện tại. |

## 16. Giới hạn hiện tại

1. Server chưa thực thi thời lượng bài theo thời điểm bắt đầu; client timer có thể bị bỏ qua.
2. Start-assignment use case chưa tự kiểm đầy đủ trạng thái/hạn trước khi ghi.
3. Vô hiệu hóa Teacher/Student chưa làm mất hiệu lực access token ngay lập tức.
4. `CompletedQuestionCount` phụ thuộc orchestrator hoàn tất AI/fallback; lỗi AI cuối có thể giữ progress ở `InProgress`.
5. Lưu preference và tạo Learning Plan không nằm trong một transaction/workflow nguyên tử.
6. Recommendation và nhận xét tổng hợp sau AI/review là best-effort ngoài transaction, có thể tạm không đồng bộ.
7. Attachment storage và database không atomic; có bù trừ và dọn orphan nhưng vẫn có crash window.
8. UI cho phép chọn TSV trong khi backend import không hỗ trợ TSV.
9. Thống kê PlatformAdmin cộng dữ liệu của trang hiện tại, không phải tổng toàn hệ thống.
10. Quản lý phiên chỉ có count và revoke-all; chưa có list/revoke-one.
11. Nháp chỉ nằm trên trình duyệt, không đồng bộ thiết bị và không chứng minh đã nộp.
12. Free-practice attempt chưa có đường Teacher review theo scope guard hiện tại.
13. Audit bị phân mảnh theo nghiệp vụ; chưa bao phủ mọi hành động trong một mô hình thống nhất.
14. Chưa có benchmark AI do giáo viên xác nhận, E2E trình duyệt toàn luồng, load/concurrency diện rộng, observability hoặc hardening production.

## 17. Hướng phát triển

### Ưu tiên cao – cần hoàn thiện cho đồ án

- Cưỡng chế time limit ở server bằng `StartedAt`, thời lượng và chính sách grace thống nhất; kiểm tra status/due ngay tại start và submit.
- Khi activate/deactivate tài khoản, tăng `AuthVersion`, thu hồi refresh token trong transaction và bổ sung test access-token cũ.
- Tách trạng thái “đã nhận đủ câu” khỏi kết quả AI để progress hoàn tất đúng ngay cả khi analysis thất bại; cung cấp retry/recovery rõ ràng.
- Biến Learning Plan thành workflow nguyên tử hoặc có trạng thái/outbox bù trừ; không để preference mới đi với plan cũ.
- Sửa thống kê PlatformAdmin thành aggregate phía server và thống nhất định dạng import TSV giữa UI/backend.
- Chạy backend test bằng .NET SDK 10 và MySQL thật; bổ sung E2E bốn actor cho submit, AI outage, review và cross-tenant.

### Ưu tiên trung bình – nâng chất lượng

- Lưu nháp phía server, autosave có version/idempotency và phục hồi đa thiết bị; cải thiện responsive và trạng thái mạng yếu.
- Mở rộng Function Grapher, hình học và khả năng kiểm tra biểu thức nhưng vẫn mô tả đúng phạm vi, không gắn nhãn Casio.
- Bổ sung danh sách phiên, revoke từng phiên, lockout/rate limit đăng nhập và audit tập trung.
- Dùng outbox/job cho recommendation/comment hậu xử lý, dashboard aggregate và thông báo để side effect có thể retry có kiểm soát.
- Xây tập dữ liệu teacher-confirmed, đo precision/consistency/failure của AI và hiệu chỉnh Evidence Gate có version.
- Thực hiện performance, concurrency, attachment recovery, backup/restore và security testing.

### Hướng mở rộng sau đồ án

- Kho analytics và báo cáo xu hướng ở mức đã ẩn danh/tổng hợp phù hợp quyền riêng tư.
- Nội dung học, tài nguyên và notification gắn trực tiếp với recommendation/learning session.
- Quan sát vận hành gồm structured log, metric, trace, cảnh báo, SLO và runbook production.
- Chỉ cân nhắc mô hình ML hoặc dự báo sau khi có dữ liệu đủ chất lượng, baseline, đánh giá sai lệch và quy trình giáo viên giám sát. Billing và mobile app tiếp tục nằm ngoài phạm vi trừ khi mục tiêu dự án thay đổi.

## 18. Kiểm thử và tiêu chí nghiệm thu

### 18.1 Bằng chứng chạy trong lần đối chiếu

- Frontend: đã chạy `npm test`; **275/275 test đạt**, 34 file test, không có test thất bại.
- Backend: có **209 file C#** trong project test và khoảng **2.355 vị trí khai báo `[Fact]`/`[Theory]`** theo kiểm kê source. Không được xem đây là số test đã chạy vì theory có thể sinh nhiều case và việc đếm attribute không phản ánh discovery.
- Backend chưa chạy được trong môi trường kiểm tra do chỉ có .NET Runtime 8.0.8 và **không có .NET SDK**, trong khi project target .NET 10. Kết luận đúng là: **có mã kiểm thử nhưng chưa có bằng chứng chạy thành công trong lần kiểm tra này**.

Test backend hiện được tổ chức quanh assessment/reasoning, assignment, curriculum/question, dashboard, Digital Twin, identity/tenancy, knowledge graph, organization, persistence, platform, recommendation và seeding. Frontend test chủ yếu là unit/component/source-contract; chưa thay thế một bộ browser E2E xuyên suốt.

### 18.2 Ma trận nghiệm thu

| Lớp kiểm thử | Trọng tâm | Điều kiện nghiệm thu |
| --- | --- | --- |
| Unit | Validation, Evidence Gate, mastery, recommendation, state transition | Có case đúng/sai/ngưỡng biên và kết quả xác định. |
| API/Controller | Mapping request/error, authn/authz, null/not-found/conflict | Status và payload nhất quán; không lộ dữ liệu. |
| Authorization/Tenant | Cross-tenant, teacher scope, student ownership, PlatformAdmin denial | Mọi truy cập ngoài phạm vi bị backend từ chối. |
| Integration/MySQL | Transaction, FK, unique, row lock, optimistic concurrency | Chạy với MySQL thật; không chỉ dựa vào mock/in-memory. |
| Attempt/AI | Idempotency, attachment, retry, timeout, fallback, terminal failure | Không mất/nhân đôi bài; trạng thái progress phục hồi được. |
| Review/Twin | Approve, override, version conflict, replay/history | Mastery tái tính đúng và có nguồn gốc. |
| Recommendation/Plan | Linear, Opportunity Gap, blocked, accept/dismiss, AI plan fallback | Quy tắc và snapshot nhất quán; lỗi giữa chừng không để state mơ hồ. |
| Frontend | Form, permission, loading/empty/error, draft, polling | Giao diện phản ánh đúng contract và không báo thành công sớm. |
| E2E | Bốn actor từ đăng nhập đến kết quả/review | Chạy trên stack thật, gồm AI outage và dữ liệu khác tenant. |
| Phi chức năng | Load, concurrency, security, backup/restore, observability | Có số đo, ngưỡng chấp nhận và bằng chứng chạy; hiện chưa đủ. |

Tiêu chí quan trọng nhất là bài đã được máy chủ nhận không mất do AI lỗi; quyền/tenant/ownership luôn được kiểm tra ở backend; kết quả và Twin truy vết được; dữ liệu chưa chắc chắn hiển thị đúng trạng thái; các điểm “Triển khai một phần” không được trình diễn như đã hoàn tất.

## 19. Phân chia công việc nhóm nếu dữ liệu hiện có đủ căn cứ

Repository có tài liệu phân công dự kiến nhưng chưa đủ bằng chứng để xác nhận tỷ lệ đóng góp thực tế. Bảng dưới đây chỉ là kế hoạch cần các thành viên và lịch sử công việc xác nhận, không phải kết luận nghiệm thu cá nhân.

| Thành viên | Phần phụ trách dự kiến |
| --- | --- |
| Tuấn Tài | Tích hợp, xác thực và phân quyền backend; quản lý tiến độ và phát hành. |
| Thịnh | Trải nghiệm người dùng, giao diện quản lý tổ chức và phân quyền. |
| Khoa | Cơ sở dữ liệu, migration, cấu trúc kiến thức và curriculum. |
| Thành Tài | Question Bank, Assignment, không gian làm bài và kiểm thử luồng học sinh. |
| Sơn | AI, Evidence Gate, Digital Twin và Recommendation. |

## Phụ lục A – Tình huống minh họa

### A.1 Học sinh làm đúng nhưng AI không chắc chắn

Deterministic grading giữ kết quả đúng. Nếu AI confidence dưới 50 hoặc mâu thuẫn, Evidence Gate đặt trọng số 0 và chuyển review; AI không tự đổi điểm. Giáo viên có thể xác nhận để tạo evidence mới.

### A.2 Học sinh gửi cùng câu hai lần

Nếu `clientSubmissionId` và payload giống nhau, máy chủ trả attempt đã có. Nếu cùng ID nhưng nội dung khác, máy chủ trả lỗi duplicate/conflict. Unique constraint xử lý cả tình huống hai request đến gần như đồng thời.

### A.3 Gemini mất kết nối

Attempt và AI job đã được lưu. Worker retry/backoff; nếu đã có đúng/sai xác định thì fallback có thể cập nhật với trọng số 0,30 và yêu cầu review, còn chưa có đúng/sai thì mastery giữ nguyên. Job lỗi cuối không xóa attempt nhưng hiện có thể làm tiến độ chưa chuyển hoàn tất.

### A.4 Giáo viên sửa một kết quả cũ

Máy chủ kiểm tra scope, reason, score và version; nối evidence mới; replay chuỗi attempt theo thời gian; cập nhật Twin và history trong transaction. Recommendation được làm mới sau commit và có thể trễ nếu side effect lỗi.

### A.5 Người dùng truy cập tài nguyên khác trung tâm

JWT/tenant context, global query filter và resource guard cùng giới hạn truy vấn. Tài nguyên không thuộc phạm vi không được trả về chỉ vì người dùng biết ID. PlatformAdmin cũng không được dùng quyền nền tảng để đi vòng vào endpoint học thuật.

## Phụ lục B – Nguồn kỹ thuật dùng để đối chiếu

Việc xác minh dựa trên source frontend/backend, controller/middleware, use case, policy, EF Core configuration/migration, worker AI, Docker và test. Các tài liệu `PROJECT_REQUIREMENTS.md`, `DATABASE_SCHEMA.md`, `API_CONTRACTS.md`, `UI_UX_SPEC.md`, `CONSTITUTION.md`, `MASTER_PLAN.md`, `PROJECT_TRACKING.md`, `TEAM_ASSIGNMENT.md`, `CODEBASE_CHANGE_PLAN.md` và `docs/decisions`, `docs/evaluation`, `docs/verification` được dùng làm ngữ cảnh; khi khác source code, source code được ưu tiên.
