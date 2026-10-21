# Student QA — sửa lỗi và kiểm thử trực tiếp 20–21/10/2026

Môi trường: localhost:3000, API localhost:5000, dữ liệu demo EDUTWIN_A.
Tài khoản kiểm thử: student01, student02, teacher.math; phạm vi chính student01, dùng teacher để tạo bài, chấm, xử lý yêu cầu và gửi thông báo.
Không ghi mật khẩu hoặc nội dung cấu hình bí mật trong báo cáo.

## Bản sửa

1. Nhật ký có thêm nguồn **Nộp bài và phân tích** từ thời điểm đã lưu của câu trả lời, lần nộp toàn bài và bản phân tích. Trước đây chỉ có lịch sử giáo viên duyệt và sự kiện thông báo, nên bài đã nộp không hiện trong nhật ký học thuật. Nguồn **Giáo viên đánh giá** được đặt tên đúng phạm vi. Giữ scope student-own / teacher-owned-class; manager và platform không có dữ liệu học thuật.
2. Sự kiện `AssignmentAiReady` có tên và mô tả tiếng Việt cụ thể; không còn rơi vào nhãn chung “Cập nhật được ghi nhận”.
3. Câu hỏi seed có **chủ đề**, nhưng thiếu **khối lớp**. Đây là nguyên nhân bộ lọc lớp 10 không tìm được câu hỏi; nhận định ban đầu rằng thiếu chủ đề đã được sửa sau khi kiểm tra dữ liệu. Seed mới có khối; backfill chỉ bổ sung khối còn trống cho câu hỏi khớp ID, trung tâm, môn, chủ đề, giáo viên tạo và nguyên văn đề của template. Không ghi đè câu đã phân khối hoặc đề do giáo viên sửa.
4. README lấy mật khẩu demo từ cấu hình lúc seed, không công bố một mật khẩu mặc định sai.
5. Nguy cơ 62% ở student và 0 học sinh nguy cơ cao ở teacher phù hợp ngưỡng 70%. Dashboard teacher giải thích ngưỡng này và sửa phép nhân 100 thừa khi hiển thị phần trăm.
6. Giáo viên duyệt nguyên trạng cũng tăng OverrideVersion. Feedback trước đây dựa vào version để phân loại nên hiển thị nhầm thành “điều chỉnh”. Bản sửa dùng ReviewDecision rõ ràng; student đã thấy nhãn xác nhận nguyên trạng ở câu 2 của bài QA nhiều dạng.
7. Player hiển thị ghi chú tổng kết, tên giáo viên và thời điểm chốt toàn bài. Trước đây thiếu ghi chú tổng kết. Nội dung đề xuất AI đã được cập nhật sau giáo viên duyệt, không tiếp tục hứa sẽ duyệt trong tương lai.
8. Cảnh báo nộp bài phân biệt câu thiếu đáp án và câu đã có đáp án nhưng thiếu lập luận bắt buộc; không cho xác nhận nộp trong trường hợp thiếu lập luận. Quay lại kiểm tra giữ nguyên đáp án.

## Lỗi tìm thêm trong vòng hồi quy ngày 21/10

- **Năng lực bị giảm khi teacher xác nhận câu bỏ qua:** tái hiện trên câu 3 bài QA nhiều dạng. Lúc nộp ghi rõ không cập nhật năng lực, nhưng sau duyệt mức thành thạo đi từ khoảng 84% về 74%. Sửa EvidenceAssessmentFactory để mọi nguồn chấm của câu bỏ qua có trọng số 0; replay của cả duyệt và điều chỉnh cũng bỏ qua trọng số cũ sai. Mẫu hiệu chỉnh mức tự tin không nhận câu bỏ qua. Có kiểm thử hồi quy cho teacher approval/override và vẫn giữ điểm bài tập 0.
- **Bảo mật student gọi API platform:** mở modal thấy phần hồ sơ trống do bị từ chối quyền. Thêm `/account/me/*` tự quản lý theo user + center + role đang đăng nhập; giữ nguyên ranh giới platform. Sửa cache hồ sơ theo danh tính, hiển thị lỗi tải có nút thử lại; thu hồi phiên ghi đúng là toàn bộ phiên và đăng xuất, thay vì hứa chỉ thu hồi “phiên khác”.
- **Modal báo lỗi tràn khỏi màn hình hẹp:** phần đầu và nút gửi nằm ngoài vùng thao tác; lần báo QA trước phải dùng Enter. Sửa hai modal xem xét/báo lỗi có chiều cao theo viewport, cuộn nội bộ và ngữ nghĩa dialog.
- **Điểm bù hiện số thực dài:** `+3.3333333333333335 điểm` ở chi tiết và player. Làm tròn hiển thị 2 chữ số, giữ nguyên phép tính điểm.
- **Câu miễn hiện “Đúng”, câu bỏ qua bị mô tả như AI chưa hoàn tất:** thêm nhãn miễn/bỏ qua riêng, không khẳng định AI đã phân tích câu bỏ trống. Duyệt câu bỏ qua có mô tả phù hợp, không tuyên bố mọi luận điểm AI đều chính xác.
- **Thông tin dễ hiểu nhầm:** ẩn prefix kỹ thuật của loại báo lỗi khỏi lời nhắn student; nhãn thang điểm gốc ở wizard teacher nói rõ các câu đóng góp bằng nhau; lịch sử quyết định tuyến có thời điểm và chú thích là số liệu tại thời điểm cập nhật, tránh bị hiểu là tiến độ hiện tại.

Không thay schema. Dữ liệu local đã được sao lưu trước khi cập nhật.

## Kiểm tra tự động

- Backend: 25/25 kiểm thử liên quan ActivityAndOperations, SeedQuestionGradeBackfill, EduTwinSeedFactory đạt.
- Backend đợt feedback trước đó: 35/35 đạt. Đợt bổ sung 21/10: 78/78 đạt (AccountSecurityService, PlatformMeService, TeacherApproveAIProposal, EvidenceAssessmentFactory, TeacherOverrideReplayHardening, LearningPathConnectedExperience, GetAttemptFeedback). Các tập có thể trùng nhau, không cộng thành số test duy nhất.
- Sau bổ sung điều kiện center cho truy vấn refresh token, chạy lại AccountSecurityService + PlatformMeService: 12/12 đạt.
- Frontend: build thành công; 18/18 kiểm thử notifications đạt, gồm nhãn nguồn/sự kiện mới.
- Frontend đợt 21/10: 32/32 đạt gồm time limit/auto-submit, feedback actions, submitted review và 5 kiểm tra hồi quy nguồn UI mới. Các kiểm tra cấu trúc nguồn được dùng bổ trợ; bằng chứng hành vi chính là thao tác trực tiếp bên dưới.
- `git diff --check`: đạt.

## Kiểm thử trực tiếp

Mỗi nhóm được đánh giá cả tính đúng, tính liên kết dữ liệu và mức phù hợp cho học sinh. “Đạt” chỉ áp dụng tình huống đã thao tác, không thay cho kiểm thử mọi tổ hợp dữ liệu hoặc mọi thiết bị.

| Nhóm student | Thao tác và đối chiếu trực tiếp | Đánh giá |
|---|---|---|
| Đăng nhập/đăng xuất | Đổi phiên student01 → teacher → student02 → student01 nhiều lần, kiểm tra danh tính và vai trò | Đạt luồng thường |
| Phạm vi môn/lớp | Toán/Anh độc lập; thông báo riêng và bài giao student01 không lộ sang student02; URL bài không được giao bị từ chối | Đạt các trường hợp đã kiểm tra |
| Lịch sử lớp | Chuyển xem lịch sử, hiển thị rỗng khi chưa có lớp lưu trữ; quay về đang học | Phù hợp dữ liệu hiện có; chưa có lớp cũ thật để kiểm tra nội dung |
| Tổng quan/Atlas/Radar | Đối chiếu dữ liệu theo môn, liên kết sang bài và lộ trình, đổi kiểu biểu đồ | Hoạt động; Twin tích lũy toàn môn được giải thích riêng với giáo trình lớp |
| Mục tiêu và nguy cơ | Tạm đổi Toán 10 điểm/1 ngày, student nguy cơ 93%, teacher thấy 1 học sinh nguy cơ cao 93%; khôi phục và xác nhận 8 điểm/90 ngày | Đạt; không còn hiển thị 9300% |
| Danh sách bài tập | Lọc chưa bắt đầu/hoàn thành, tìm tiêu đề có/không có kết quả, xóa tìm kiếm | Đạt sau khi tải xong |
| Bài được giao | Mở bài đúng scope; chưa bắt đầu không lộ đề; bắt đầu, đổi câu và xem tiến độ | Đạt |
| Đáp án và lập luận | MCQ đúng/sai, trả lời ngắn `(1,1)`, lưu công thức/lập luận, confidence 15% và 85% | Lưu và phục hồi đúng; có kiểm tra cảnh báo thiếu lập luận |
| Công cụ toán | Chèn công thức `y=7`; máy tính tích hợp `2×3+1=7` và chèn kết quả; đồ thị `2x+1` | Đạt công cụ tích hợp; mô phỏng CalcES nhúng chưa xác nhận trọn vẹn |
| Bảng nháp | Đổi nền, vẽ hai nét, phóng toàn màn hình, đính kèm PNG; thoát/mở lại; teacher thấy cùng ảnh | Đạt bảo toàn bản nháp và liên kết ảnh |
| Nộp toàn bài | Bài 3 câu cố ý 1 sai + 1 đúng + 1 bỏ qua; một lần nộp, khóa chỉnh sửa, khôi phục sau đổi trang | Điểm sơ bộ 3,33/10 đúng chính sách trọng số bằng nhau |
| Hết giờ | Bài 1 phút; không trả lời, tải lại giữa chừng, đồng hồ không reset; đúng 1 phút tự nộp, tự bỏ qua, 0 điểm, khóa bài | Đạt; thời gian nộp 07:18:06 21/10 |
| AI/phân tích | Câu đúng/sai có nhận xét riêng, lỗi tính toán cố ý được chỉ ra; skip không gọi AI; tutor có tình huống unavailable và thử lại sau sinh nhận xét được | Đạt trường hợp xử lý thường và không mất bài khi AI chưa có |
| Giáo viên xác nhận | Teacher duyệt nguyên trạng câu 2; student thấy “Giáo viên đã xác nhận kết quả câu hỏi”, 10/10, người duyệt, thời điểm, confidence 15% còn đúng | Đạt bản sửa phân loại |
| Chốt toàn bài | Teacher chốt QA gốc 10/10; sau xử lý xem xét chốt lại 9/10 với tổng kết mới; student thấy điểm chính thức và ghi chú | Đạt liên kết hai vai trò |
| Yêu cầu xem xét | Kiểm tra tối thiểu 10 ký tự, gửi yêu cầu, chặn yêu cầu trùng đang chờ; teacher đọc đúng nội dung, mở khóa bằng lý do bắt buộc, điều chỉnh và giải quyết | Đạt luồng đầy đủ trên bài QA |
| Báo lỗi đề/hủy câu | Student báo rõ là QA; teacher hủy câu 1 riêng trong bài, bỏ chọn cách ly ngân hàng; đề gốc vẫn hiện trong danh sách câu dùng được | Đúng phạm vi bài; không cách ly nhầm đề gốc |
| Điểm sau hủy | Câu 1 được bù 10, câu 2 được 10, câu 3 được 0; teacher và student cùng 6,67/10; báo lỗi đã giải quyết | Đạt tính điểm; các lỗi nhãn/định dạng phát hiện đã có bản sửa |
| Thông báo riêng | Teacher gửi student01 yêu cầu xác nhận + phản hồi; student đọc, xác nhận, gửi trả lời; teacher thấy nhận1/đọc1/xác nhận1 và đúng câu trả lời | Đạt; student02 không thấy |
| Thông báo kết quả | Mở liên kết đúng bài/lớp/môn; số chưa đọc giảm sau khi đọc | Đạt; chưa thực hiện ẩn vĩnh viễn thông báo |
| Nhật ký | Có nguồn nộp bài/phân tích và teacher review; lọc nguồn, ngày có/không có dữ liệu; mở liên kết bài | Đạt bản sửa; chỉ thấy dữ liệu đúng người dùng |
| Lộ trình nền tảng | Chọn Hàm số, xem phạm vi và thời gian, kích hoạt, mở ví dụ từng bước, lưu giải thích, tạm dừng/thoát/mở lại/nộp | Nội dung và tiến độ giữ đúng; đồng hồ qua lần mở còn vấn đề ghi bên dưới |
| Kiểm tra độc lập | Ban đầu thiếu 4/5 câu mới được chặn; thêm 1 câu QA đúng khối; bắt đầu 5 câu, thoát và tiếp tục; chưa nộp cả bộ không lộ đáp án/gợi ý | Đạt bảo vệ đề và khôi phục tiến độ |
| Kết quả độc lập | Nộp 5/5 đúng, đạt; tải lời giải sau nộp; Hàm số xác nhận 5 bằng chứng, 1/1 kỹ năng; hẹn ôn 23/10 | Đạt; không kiểm tra thời điểm ôn tương lai bằng cách đổi đồng hồ |
| Luyện có hướng dẫn | Trọn bộ 5 câu: 1 gợi ý rồi đúng, 1 cố ý sai, 3 đúng; đọc giải thích/tutor và tổng kết | 4/5 đúng, 70% do câu dùng gợi ý tính 0,5; giải thích phù hợp, không tự cấp bằng chứng độc lập |
| Luyện tập thích ứng tự do | Mở từ Twin và menu với classId, ở cả Toán và Anh | Chưa dùng được với tuyến V4 hiện tại; xem lỗi mở bên dưới |
| Bảo mật | Mở modal, form trống bị chặn; sửa route sai; hồ sơ student01 hiện đúng; thu hồi 4 phiên về trang đăng nhập; đăng nhập lại với mật khẩu cũ, modal hiện AuthVersion2 và 1 phiên | Đạt hồ sơ/thu hồi/đăng nhập lại; chờ người dùng tự đổi mật khẩu thật |

## Dữ liệu QA để đối chiếu

- Bài gốc: `d63c5559-1404-406f-aa14-4d3aeabcfa9c`, **QA Student E2E – Bài tập Hàm số**, 1 câu #20030, kết quả chính thức **9/10** sau yêu cầu xem xét.
- Bài nhiều dạng: `8b35615c-b8cb-4cf7-bf5a-6576e7289030`, **QA Student Regression – Nhiều dạng câu**, thứ tự #10002/#10003/#10001, kết quả chính thức **6,67/10**. Báo lỗi câu 1 đã giải quyết; không cách ly ngân hàng.
- Bài hết giờ: `5323940d-bc6d-44c6-9f24-aa89b253295f`, **QA Student – Hết giờ tự nộp 1 phút**, câu #20031, tự nộp 0 điểm. Teacher đã duyệt, mở lại/lưu cùng điểm 0 và chốt lần hai lúc 07:40:37 21/10. Student đã thấy điểm chính thức 0/10 và đúng tổng kết lần hai.
- Câu QA mới #20031: `f(x)=3x-2`, tính `f(4)`, đáp án B=10; Active, khối10, Hàm số. Được dùng bổ sung ngân hàng và bài kiểm thử.
- Thông báo **QA Student – xác nhận và phản hồi** gửi riêng student01: đã đọc/xác nhận/trả lời, teacher đã đối chiếu.
- Các bài được đánh dấu rõ không dùng cho học kỳ. Giữ lại để kiểm chứng; không tự xóa lịch sử. Mục tiêu Toán đã khôi phục **8/90**.

## Còn mở / giới hạn kiểm thử

1. **P2 – Đồng hồ nhiệm vụ mất thời gian sau lưu/thoát/mở lại.** Ví dụ tìm hiểu có 21s trước khi thoát, mở lại đếm từ 0; câu trả lời vẫn giữ. `MissionPlayerModal` giữ activeSeconds/activeMs trong bộ nhớ, không lưu trong draft. Cần lưu thời gian hoạt động theo activity/run, bao gồm thao tác đóng và tạm dừng; không cộng thời gian ngoài trang. Chưa sửa trong đợt này.
2. **P2 – Luyện tập thích ứng tự do chưa nối với tuyến V4.** Đã thử cả Toán và Anh có ngân hàng câu Active, từ cả link có/không có classId đều ra trạng thái không có câu. Nguồn hiện đọc current LearningPathItem kiểu cũ trong khi V4 dùng session/mission. Không kết luận là hết câu mới. Luồng luyện có hướng dẫn V4 dùng được; cần quyết định kết nối chức năng tự do hoặc thay điều hướng bằng nhiệm vụ luyện của V4.
3. **P3 – Nhật ký Twin còn thông điệp kỹ thuật tiếng Anh/UUID.** Ví dụ bản ghi teacher approval hiện ID giáo viên và “Replayed … attempts”; khó đọc đối với học sinh. Không ảnh hưởng điểm nhưng trải nghiệm thông tin chưa tốt.
4. **Bước cần người dùng:** thao tác nhập mật khẩu mới, xác nhận và gửi phải do người dùng thực hiện theo quy định công cụ computer-use. Chưa đổi mật khẩu thật. Backend đã kiểm tra bằng dữ liệu tổng hợp, không thay mật khẩu demo đang dùng.
5. Chưa đến ngày ôn 23/10 để chạy lượt ôn đúng hạn; chưa có lớp lịch sử thực tế; chưa kiểm thử toàn bộ CalcES nhúng, mất mạng thực tế, hai thiết bị cùng sửa draft, mọi kích thước màn hình hoặc mọi trạng thái admin. Không gắn nhãn “toàn hệ thống không còn lỗi”.

## Bằng chứng hình ảnh

- `evidence/student-qa-20261020/final-grade-and-teacher-note.png`: điểm 9/10 và tổng kết lần hai của teacher.
- `evidence/student-qa-20261020/timed-auto-submit.png`: tự nộp đúng 1 phút, 0 điểm và chỉ đọc; ảnh cũng cho thấy header hẹp trước bản sửa bố cục.
- `evidence/student-qa-20261020/report-dialog-narrow-overflow.png`: modal báo lỗi bị tràn trước bản sửa.

## Kết quả triển khai và hồi quy bản bổ sung

API và web đã build thành công và được cập nhật vào hai container local, không thay schema. Các bước xác nhận sau triển khai:

1. Student câu bỏ trống hiện **Đã bỏ qua**, 0/10; không còn cảnh báo AI lỗi/chưa hoàn tất; lời xác nhận teacher không tuyên bố AI đã phân tích câu này.
2. Student câu hủy hiện **Được miễn · Tính trọn điểm** và **+3,33 điểm**, tổng bài chính thức vẫn **6,67/10**; thông điệp báo lỗi không lộ prefix kỹ thuật.
3. Modal báo lỗi có top16px, bottom591px, clientHeight574px và scrollHeight767px trên viewport hẹp; nút Hủy bấm trực tiếp được, không cần Enter. Header player xuống dòng đúng và không chồng nội dung như ảnh trước sửa.
4. Bảo mật hiện đúng student01, tên và 4 phiên; thực hiện thu hồi toàn bộ → về đăng nhập. Đăng nhập lại thành công bằng mật khẩu hiện có; modal hiện AuthVersion2, **1 phiên**, không còn hồ sơ trống. Chưa thay mật khẩu thật.
5. Teacher duyệt câu bỏ qua của bài một phút: bằng chứng mới có trọng số0. Replay loại ảnh hưởng sai của câu bỏ qua cũ, phục hồi Hàm số **83,96%**, **12 bằng chứng**. Sau mở lại và lưu lại điểm0 bằng luồng điều chỉnh, vẫn **83,96% / 12**, không tính lặp. Student Twin hiển thị84% và dòng mới84,0%→84,0% (+0,0%); bản ghi sửa dữ liệu cũ74,1%→84,0% được giữ trong lịch sử. Điểm toàn bài vẫn0/10 sau chốt.
6. Lộ trình phân biệt rõ “Lịch sử cập nhật tuyến”, có ngày giờ UTC+7 của từng quyết định, bên cạnh trạng thái hiện tại **Đã xác nhận / 5 bằng chứng độc lập** và lịch ôn23/10. Không còn hiểu số0 bằng chứng lúc tạo tuyến là tiến độ hiện tại.

Bằng chứng sau sửa:

- `evidence/student-qa-20261020/regression-final-grade-after-fix.png`: 6,67/10 đã chốt, header không chồng nội dung.
- `evidence/student-qa-20261020/report-dialog-after-fix.png`: modal có tiêu đề/đóng và cuộn trong màn hình.
- `evidence/student-qa-20261020/student-security-profile-after-fix.png`: hồ sơ đúng student01 trước thu hồi.
- `evidence/student-qa-20261020/student-security-new-session-after-revoke.png`: AuthVersion2, chỉ1 phiên sau đăng nhập lại.

Đã mở sẵn biểu mẫu bảo mật và bàn giao cho người dùng thao tác đổi mật khẩu; chưa nhập mật khẩu mới. Vòng QA vẫn còn các lỗi/giới hạn nêu trên, chưa kết luận tất cả chức năng student đều đạt.
