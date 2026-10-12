# Trung tâm thông báo và Nhật ký hoạt động EduTwin

Ngày ghi nhận: 2026-10-06.

Rà soát lại: **2026-10-10**, nhánh `student/answer`, baseline `61ef80a7830772c6aec10d3cd0053e61d313cfe7`.

Trạng thái: **ĐÃ TRIỂN KHAI LOCAL, CÒN KIỂM CHỨNG UI BỐN TÁC NHÂN (2026-10-11)**. NF-01/NF-03a có báo cáo checkpoint; NF-02/NF-03b/NF-04/NF-05 đã có source, gate regression/MySQL/benchmark và rollout local/readiness/compare-live đạt. Người dùng chọn nhập tự do với bộ lọc dấu hiệu nhạy cảm phía server cho kênh hỗ trợ. Không đồng nhất source/test tự động với nghiệm thu đầy đủ UI hoặc production. Theo dõi bằng chứng hiện tại tại [báo cáo hoàn thiện](../verification/NOTIFICATIONS-COMPLETION-REPORT-2026-10-10.md), hợp đồng tại [Completion SPEC](NOTIFICATIONS-COMPLETION-SPEC.md). Không commit/push trong yêu cầu này.

Chủ sở hữu quyết định sản phẩm: người dùng/nhóm EduTwin. Người thực hiện và reviewer từng gate cần được ghi khi mở task; không tự gán tên thành viên hoặc ghi đã nghiệm thu.

## 1. Mục tiêu và hiện trạng

Cập nhật yêu cầu tiếp tục 2026-10-10: người dùng cho phép hoàn thiện toàn bộ các gate còn lại; hợp đồng triển khai tại [Completion SPEC](NOTIFICATIONS-COMPLETION-SPEC.md). Không coi yêu cầu này là đã nghiệm thu; không push, không purge production hoặc phát test notice thật chưa duyệt.

Các tác nhân cần biết ai đã thực hiện hành động, hành động tác động đến ai, trạng thái công việc hiện tại và việc nào cần xử lý. Một hệ thống chung có hai nguồn thông báo: sự kiện nghiệp vụ tự động và thông báo người dùng chủ động soạn.

Hiện trạng baseline đã kiểm tra trước NF-01 (không phải trạng thái runtime mới):

- `TeacherReviewHistory` lưu một số lịch sử xác nhận/điều chỉnh câu hỏi.
- `AssignmentFinalReviewWorkflow` ghi các sự kiện chốt, mở lại kết quả và hủy câu vào nhật ký hiện có.
- Có nhật ký phân quyền và lịch sử thay đổi hồ sơ năng lực.
- Chưa có màn hình nhật ký nghiệp vụ thống nhất cho các tác nhân và chưa có hệ thống thông báo liên tác nhân đầy đủ.
- Chuông học sinh trong `StudentLayout.tsx` hiện là giao diện với chấm đỏ cố định, chưa có nguồn dữ liệu thông báo thật.
- Chưa có chức năng gửi thông báo chủ động hoàn chỉnh giữa Admin, Center Manager, Teacher và học sinh lớp được phụ trách.
- Sau các ca sửa 08–10/10, đã có nhật ký vòng đời lớp, ledger giáo trình áp dụng, audit giáo trình/nút/cạnh và báo cáo lớp Manager chỉ xem. Đây là nguồn lịch sử cần tái sử dụng, không tạo một bộ lịch sử song song giả định rằng chưa có gì.
- Code/catalog hiện chưa có inbox/outbox thông báo, API thông báo hay SignalR Hub. Toast lỗi/thành công trong biểu mẫu không phải thông báo đã lưu của người nhận.
- `SubmitStudentAssignmentUseCase` ghi nhận nộp toàn bài trước khi AI hoàn tất; `TeacherFinalReviewStatus`/`FinalReviewVersion` là chiều chốt điểm riêng. Không suy ra ba trạng thái này từ một trường `Completed`.

Không mô tả hiện trạng thành "hoàn toàn chưa lưu lịch sử": lịch sử đã tồn tại một phần, nhưng còn rời rạc và thiếu giao diện/phân phối thông báo.

Cập nhật checkpoint 2026-10-10: NF-01 đã có outbox/dispatch/delivery, API own inbox và chuông/hộp thư bốn layout; bản local đã áp dụng migration có backup. Gate tự động và giao diện trống Teacher Math đã kiểm tra; chưa nối sự kiện nghiệp vụ hoặc gửi chủ động/nhật ký chung. Xem [báo cáo NF-01](../verification/NOTIFICATIONS-NF01-REPORT-2026-10-10.md) cho bằng chứng và danh sách phần còn thiếu; các ma trận dưới đây vẫn là kế hoạch, không phải chức năng đã hoàn thành.

## 2. Phân biệt các khái niệm

- **Nhật ký hoạt động:** bằng chứng ai làm gì, lúc nào, trên đối tượng nào; chỉ đọc theo phạm vi quyền. Thay đổi quan trọng lưu trạng thái trước/sau và lý do.
- **Thông báo:** nội dung được gửi cho người thực sự liên quan hoặc cần xử lý; không phải mọi nhật ký đều tạo thông báo.
- **Đã đọc:** người nhận đã mở thông báo.
- **Đã xác nhận nhận:** xác nhận rõ ràng đối với thông báo yêu cầu xác nhận.
- **Đã xử lý:** trạng thái nghiệp vụ của yêu cầu liên quan; không suy ra từ việc đọc hoặc xác nhận nhận.

Không đồng nhất điểm câu hỏi/tổng bài thang 10 với chỉ số chất lượng lập luận thang 100. Các thông báo về điểm cũng phải dùng đơn vị đã thống nhất và phân biệt điểm tạm với kết quả đã chốt.

## 3. Ma trận thông báo tự động

| Sự kiện | Người nhận và phạm vi | Ghi chú |
| --- | --- | --- |
| Giáo viên xuất bản/giao bài | Đúng học sinh được giao | Dùng tập người nhận đã được nghiệp vụ giao bài xác định; không phát toàn trung tâm |
| Sửa hạn nộp/hướng dẫn hoặc đóng bài đã giao | Học sinh bị ảnh hưởng | Nêu thay đổi có ý nghĩa; không thông báo việc lưu bản nháp |
| Học sinh nộp xong toàn bài | Giáo viên đang phụ trách lớp | Tránh thông báo riêng mỗi lần gõ hoặc nộp từng câu; có thể gom nhóm theo bài tập |
| AI phân tích xong toàn bài | Học sinh sở hữu bài; giáo viên có việc cần duyệt | Một thông báo tổng hợp theo bài/học sinh/lượt xử lý; phân biệt đề xuất AI, fallback và điểm đã chốt; không phát 50 thông báo cho 50 câu |
| AI chấm lại thủ công hoàn tất | Người yêu cầu có quyền và chủ bài tương ứng | Chỉ khi có kết quả/lỗi terminal mới của lượt retry; không phát cho từng lần xoay key, quota wait hoặc retry nội bộ |
| Học sinh yêu cầu xem xét kết quả | Giáo viên phụ trách | Phân loại riêng với báo cáo đề sai; có liên kết đến yêu cầu cần xử lý |
| Học sinh báo cáo đề sai | Giáo viên phụ trách | Nêu câu/bài liên quan và loại sự cố, không phát cho học sinh khác |
| Giáo viên xử lý yêu cầu | Học sinh gửi yêu cầu | Nêu đã giải quyết/từ chối, lý do được phép xem và kết quả liên quan |
| Giáo viên chốt toàn bài | Học sinh tương ứng | Thông báo kết quả đã chốt; không gọi lần lưu điểm trung gian là kết quả cuối cùng |
| Mở lại kết quả đã chốt | Học sinh tương ứng | Nêu đang rà soát; bài nộp gốc vẫn được giữ, không phải yêu cầu làm lại |
| Điều chỉnh và chốt lại | Học sinh tương ứng | Lịch sử trước/sau, lý do phù hợp và phiên bản kết quả |
| Hủy câu do lỗi đề | Các học sinh được giao bài bị ảnh hưởng | Nêu phạm vi bài tập, cách tính điểm và trạng thái chờ chốt lại; không ảnh hưởng bài khác dùng chung câu |
| Xuất bản/gán hoặc thay đổi giáo trình ảnh hưởng lớp | Giáo viên/học sinh liên quan | Chỉ nội dung được phép công bố; không gửi bản nháp cho học sinh |
| Ngừng/thay giáo trình đang áp dụng | Giáo viên lớp và học sinh bị ảnh hưởng | Lấy ledger hiện hành và phiên bản thay đổi; bản nháp/clone chưa gán không tạo thông báo học sinh |
| Lưu trữ/mở lại lớp | Giáo viên lớp và thành viên bị ảnh hưởng; quản lý có thẩm quyền | Nêu trạng thái chỉ xem hoặc mở lại; thông báo không tự phục hồi ứng dụng giáo trình đã kết thúc |
| Thêm/chuyển/rút học sinh khỏi lớp | Học sinh đó và giáo viên lớp liên quan | Chỉ thông báo thao tác thành công và đúng người bị ảnh hưởng |
| Đổi giáo viên phụ trách | Giáo viên cũ, mới và học sinh lớp | Thông báo bàn giao; việc đang chờ được định tuyến đúng người phụ trách mới |
| Đổi quyền/trạng thái tài khoản | Người bị ảnh hưởng và người quản trị có thẩm quyền | Không chứa mật khẩu, token hoặc thông tin bảo mật |
| Giáo viên tạo giáo trình/bài tập | Center Manager có quyền trong trung tâm | Nhật ký/thông báo tổng hợp về hoạt động; không tự cấp quyền mở học liệu |
| Sửa/tắt/xóa mềm nút hoặc thay liên kết tri thức hợp lệ | Giáo viên học thuật có phạm vi; quản lý nhận metadata nếu được cấp | Ưu tiên nhật ký/tổng hợp, không spam học sinh từng nút/cạnh; thao tác bị ràng buộc chặn không phát sự kiện thành công |

Center Manager nhận thông tin quản trị tối thiểu: người thực hiện, loại hoạt động, thời gian, đối tượng/lớp khi được phép. Không gửi mặc định bài làm, ảnh nháp, đáp án mẫu hoặc chi tiết điểm riêng của học sinh cho Manager.

Manager đã có báo cáo lớp chỉ xem riêng; liên kết từ thông báo quản trị phải đi tới báo cáo/nhật ký Manager được phép, không đi tới route `/giao-vien/*`. Nếu thiếu capability đọc đích thì chỉ hiển thị tóm tắt an toàn, không cấp thêm quyền học thuật.

Platform Admin chỉ nhận sự kiện quản trị nền tảng phù hợp; không nhận mọi bài nộp, chấm điểm hoặc phản ánh học thuật của các trung tâm.

## 4. Ma trận gửi thông báo chủ động

| Người gửi → người nhận | Phạm vi đề xuất |
| --- | --- |
| Platform Admin → Center Manager | Từng trung tâm hoặc nhóm trung tâm theo quyền quản trị nền tảng; bảo trì, sự cố, yêu cầu quản trị |
| Center Manager → Platform Admin | Yêu cầu hỗ trợ, báo sự cố, phản hồi trong kênh hỗ trợ được phép |
| Center Manager → Giáo viên | Cá nhân hoặc nhóm giáo viên của chính trung tâm mình |
| Giáo viên → Center Manager | Quản lý của chính trung tâm mình; báo vấn đề/đề nghị hỗ trợ nghiệp vụ |
| Giáo viên → Học sinh | Một lớp, nhiều lớp mình phụ trách hoặc một số học sinh cụ thể trong các lớp đó |
| Giáo viên → Giáo viên | Chưa mở mặc định; xem xét sau khi chốt nhu cầu phối hợp và quyền |
| Platform Admin ↔ Giáo viên | Chưa mở mặc định; chỉ bổ sung khi có luồng hỗ trợ và quyền riêng |

Thông báo giáo viên gửi học sinh có thể dùng cho nhắc hạn nộp, hướng dẫn, thay đổi kế hoạch học tập hoặc nhắc bổ sung bài thiếu.

- Server kiểm tra giáo viên vẫn phụ trách lớp tại thời điểm gửi.
- Gửi thông báo mới cho lớp chỉ áp dụng lớp Active/Current và thành viên hợp lệ. Lớp Archived/History không được dùng làm danh sách phát nhắc làm bài/luyện tập mới. Sự kiện chốt bài cũ và phản hồi một yêu cầu cũ còn được phép là luồng khác, kiểm tra guard đọc/chấm lịch sử tương ứng.
- Không gửi cho học sinh lớp người khác hoặc trung tâm khác.
- Học sinh không thấy danh sách người nhận hoặc thông tin cá nhân của bạn khác.
- Không đưa điểm/nhận xét riêng của một học sinh vào thông báo chung của lớp.
- Học sinh có thể xác nhận đã nhận. Nếu có phản hồi, phản hồi đi riêng về giáo viên, không biến thành trò chuyện công khai cả lớp.
- Không mặc định mở hộp thư tự do cho học sinh; yêu cầu xem xét/báo lỗi đã có luồng nghiệp vụ riêng.

## 5. Giao diện

Tận dụng design system chung, có adapter quyền/phạm vi theo tác nhân, không sao chép thành bốn hệ thống độc lập.

### Thông báo

- Chuông và số chưa đọc lấy từ dữ liệu thật; bỏ chấm đỏ/số đếm giả.
- Hộp Đã nhận / Đã gửi; bộ lọc loại, thời gian, mức ưu tiên, chưa đọc và cần xử lý.
- Nút Gửi thông báo chỉ hiện theo quyền; chọn người nhận từ danh sách server cho phép.
- Nội dung: tiêu đề, nội dung, loại, ưu tiên, người gửi, thời gian, đối tượng liên quan.
- Gửi hàng loạt cần bước kiểm tra phạm vi/số người nhận trước khi xác nhận.
- Liên kết đến đúng đối tượng; thông báo không mở rộng quyền truy cập đối tượng đó.
- Inbox toàn tài khoản trong tenant; việc đổi môn/lớp trên dashboard không làm mất thông báo môn khác. Bộ lọc môn/lớp trong inbox chỉ là cách xem, không thay quyền nhận.
- Mở preview chuông không đánh dấu đọc toàn bộ. Đọc một thông báo và “đánh dấu tất cả đã đọc” là thao tác riêng, có giới hạn mốc dữ liệu đã hiển thị.
- Phản hồi theo ngữ cảnh của thông báo và đúng người được phép xem.
- Thu hồi/đính chính có lưu dấu vết; không sửa hoặc xóa âm thầm nội dung đã gửi.

### Nhật ký hoạt động

- Ai làm, làm gì, khi nào, trên đối tượng nào, kết quả thao tác.
- Thay đổi điểm/trạng thái quan trọng có dữ liệu trước/sau, lý do, phiên bản, các lần chốt/mở lại.
- Có bộ lọc và liên kết đến tài nguyên còn được phép truy cập.
- Học sinh chỉ xem phần của mình; giáo viên chỉ xem phần công việc được phụ trách; Manager/Admin xem theo quyền quản trị tương ứng.
- Không mở nguyên bảng nhật ký kỹ thuật/phân quyền cho mọi tác nhân; cần API trả dữ liệu đã lọc và che thông tin nhạy cảm.
- Không sao chép `BeforeData`/`AfterData`/`Reason` thô từ audit vào inbox. Nội dung gửi dùng template tiếng Việt với trường allow-list theo actor; trạng thái “Hệ thống/Migration” không được gán thành thao tác của một người.

## 6. Quyền và tính riêng tư

1. Mọi truy vấn và phân phối được kiểm tra ở server: trung tâm, người nhận, vai trò, quyền và quan hệ nghiệp vụ.
2. Không gửi tùy ý giữa hai trung tâm. Ngoại lệ quản trị nền tảng/hỗ trợ Admin–Manager phải có quyền và kênh rõ ràng.
3. Không tiết lộ điểm, bài làm, ảnh nháp hoặc khiếu nại của học sinh khác; không lộ đáp án/lời giải trước khi được phép.
4. Kiểm tra lại quyền khi mở tài nguyên liên quan, kể cả qua URL trực tiếp hoặc thông báo cũ.
5. Khi đổi người phụ trách, chuyển việc đang chờ phù hợp. Giáo viên cũ không giữ quyền truy cập lớp qua liên kết cũ.
6. Có thể giữ lịch sử tóm tắt người nhận đã nhận, nhưng không giữ quyền đọc dữ liệu chi tiết đã bị thu hồi.
7. Không gửi mặc định thông báo về mỗi lần gõ, lưu nháp, đổi đáp án hoặc lần AI thử lại nội bộ.
8. Chặn gửi hàng loạt ngoài phạm vi, xử lý nội dung an toàn và giới hạn chống spam.

## 7. Nguyên tắc kỹ thuật

- Lưu sự kiện nghiệp vụ cùng giao dịch làm thay đổi dữ liệu; giao dịch thất bại không phát thông báo thành công.
- Phân phối bền vững sau commit, có thử lại và chống trùng (outbox/idempotency hoặc cơ chế tương đương).
- Tập người nhận phải phù hợp nghiệp vụ: tập học sinh được giao bài không được thay bằng toàn bộ thành viên lớp hiện tại một cách tùy ý.
- Lưu phiên bản/snapshot cần thiết để lịch sử không bị thay theo dữ liệu tài nguyên đã sửa sau đó.
- Gom thông báo lặp lại theo bài tập/lớp khi phù hợp; vẫn giữ bằng chứng sự kiện riêng.
- Kênh cập nhật giao diện không thay thế nơi lưu thông báo. Chi tiết lựa chọn polling/realtime cần chốt khi triển khai.
- Đề xuất giai đoạn đầu dùng API + polling thưa khi tab hoạt động; không thêm SignalR/SSE/broker, không sửa hàng đợi AI trong cùng feature. ADR 28/09 là đề xuất lịch sử, không phải lệnh triển khai các phase của ADR.
- Lịch sử cũ chỉ backfill từ bằng chứng có thật trong các bảng lịch sử; không dựng sự kiện giả từ `UpdatedAt` hoặc dữ liệu mẫu.
- Chưa mở rộng sang email, SMS hoặc push bên ngoài trong phạm vi ban đầu.
- Không coi việc thông báo/nhật ký là lý do khôi phục các quyền học thuật đã bỏ của Center Manager.

## 8. Thứ tự triển khai khi người dùng yêu cầu tiếp tục

| Gate | Phạm vi | Điều kiện bàn giao |
| --- | --- | --- |
| NF-00 | Duyệt kế hoạch; chốt requirement, recipient policy, quyền, schema/API, UI và retention | Cập nhật specification được duyệt trước source; chưa có migration khi gate còn pending |
| NF-01 | Outbox bền vững, worker phân phối riêng, inbox/đếm chưa đọc/đánh dấu đọc; chuông thật trên bốn layout | Transaction/unique/lease/restart và tenant isolation đạt; chưa tích hợp toàn bộ nghiệp vụ |
| NF-02 | Giao/nộp bài, AI hoàn tất/fallback, review/báo đề, chốt/mở lại/hủy; lớp và giáo trình áp dụng | Không trùng; không báo điểm cuối khi mới có AI; lịch sử chỉ xem đúng nghĩa |
| NF-03 | Gửi chủ động, preview người nhận, đã gửi, thu hồi/đính chính, xác nhận nhận; phản hồi riêng có phạm vi nếu được duyệt | Ma trận mục 4 và kiểm thử thu hồi quyền/hàng loạt đạt; không mở chat tự do |
| NF-04 | Nhật ký chung bằng adapter nguồn đã có; phân công/tài khoản/đồ thị và tổng hợp Manager/Admin | Không đọc audit thô hoặc vượt scope; backfill lịch sử có nguồn nhưng không phát thông báo hồi tố |
| NF-05 | Kiểm thử tích hợp, UI bốn tác nhân, tải/chậm AI, rollout/khôi phục và báo cáo | Đạt checklist mục 18; người dùng xem từng checkpoint rồi mới tiếp tục |

Nhắc hạn tự động, email/SMS/browser push, presence và đổi transport sang realtime không nằm trong NF-01/NF-02. Nhắc thủ công của giáo viên thuộc NF-03; bộ lập lịch nhắc tự động cần task/policy riêng.

Đây là lộ trình đề xuất, không yêu cầu hoàn thành toàn bộ trước các sửa lỗi khác hoặc trước tính năng import PDF.

## 9. Tiêu chí nghiệm thu

- Giao bài chỉ thông báo đúng học sinh được giao; không có thông báo khi mới tạo bản nháp.
- Nộp toàn bài chỉ phát một sự kiện hợp lệ; thao tác gửi lại/retry không tạo thông báo trùng.
- Yêu cầu xem xét và báo đề sai đi đúng giáo viên phụ trách, có phân loại và liên kết phù hợp.
- Chốt/mở lại/chốt lại thể hiện đúng trạng thái, điểm thang 10 và lý do được phép xem.
- Hủy câu chỉ ảnh hưởng đúng bài tập, thông báo đúng người và không lộ học liệu.
- Giáo viên gửi được cho một/nhiều lớp của mình hoặc học sinh được chọn; không vượt quyền qua API sửa tay.
- Manager/Admin nhận đúng phạm vi; học sinh không đọc được thông báo/nhật ký của người khác.
- Đổi giáo viên/thu hồi quyền không để liên kết cũ vượt quyền; công việc còn chờ định tuyến đúng.
- Mất kết nối/khởi động lại không làm mất thông báo đã ghi nhận; thử lại không nhân bản người nhận.
- Đã đọc/đã xác nhận nhận/đã xử lý độc lập; badge phản ánh dữ liệu thật.
- Thu hồi/đính chính và thay đổi quan trọng giữ lịch sử; không xóa bằng chứng gốc.
- Không tạo dữ liệu mẫu hoặc dựng lịch sử giả trong dữ liệu người dùng để chứng minh tính năng.

## 10. Các lựa chọn còn cần chốt khi triển khai

NF-01 đã chốt mặc định kỹ thuật tại [NF-01 SPEC](NOTIFICATIONS-NF01-SPEC.md) sau yêu cầu tiếp tục. Các lựa chọn gửi chủ động, phản hồi và retention production trong bảng sau vẫn chờ gate tương ứng, không phải chức năng đã thực hiện:

| Lựa chọn | Mặc định đề xuất | Không được tự suy ra |
| --- | --- | --- |
| Kênh ban đầu | In-app; polling nền 30 giây khi tab hiện, refresh khi mở chuông | Không thêm SignalR/SSE/Redis/RabbitMQ hoặc browser push |
| Gửi/ưu tiên | Ba mức Thông thường/Quan trọng/Khẩn; quyền server kiểm soát, giới hạn cấu hình | Không cho người gửi tự nâng “Khẩn” để vượt rate limit |
| Phản hồi/xác nhận | Xác nhận riêng nếu người gửi yêu cầu; một luồng phản hồi riêng có ngữ cảnh, không phát cho cả lớp | Không biến xác nhận thành chốt điểm hay đọc thành xác nhận |
| Hạn mức gửi | Bounded fan-out và preview số người nhận; giá trị tối đa/tần suất chốt ở NF-00 theo quy mô thật | Không lấy một con số bất kỳ làm quota sản phẩm đã được duyệt |
| Retention | Thông báo có thể ẩn/lưu trữ khỏi inbox; sự kiện/audit vẫn bảo toàn; không tự purge ở bản đầu | Không mặc định giữ mọi nội dung cá nhân vô thời hạn; lịch lưu giữ/ẩn danh phải được chủ sở hữu duyệt trước production |
| Tổng hợp | Theo bài/học sinh/lượt AI; giáo viên có thể gộp theo bài/lớp; Manager đọc digest metadata | Không gộp mất các phiên bản chốt/mở lại hay bỏ request cần xử lý |
| Liên lạc mở rộng | Teacher–Teacher, Admin–Teacher và Student gửi tự do vẫn đóng | Không coi “hệ thống chung” là mọi người nhắn mọi người |
| Trung tâm/tài khoản bị khóa | Giữ notification chưa phân phối/đã lưu theo trạng thái; tài khoản không được bypass auth để xem | Nếu cần Manager liên hệ Admin khi tenant Suspended phải thiết kế kênh hỗ trợ riêng và duyệt, không nới guard hiện có |

Chỉ bắt đầu triển khai khi người dùng yêu cầu tiếp tục hạng mục này.

## 11. Phạm vi đã chốt ngày 2026-10-06

Người dùng loại bỏ **giám sát học sinh thời gian thực** khỏi hạng mục nâng cấp học thuật: không xây presence/heartbeat, đếm online hoặc theo dõi câu đang làm. Giữ màn hình tiến độ dựa trên dữ liệu bài nộp hiện có. Quyết định này không đồng nghĩa xóa các thông báo nghiệp vụ đã lưu trong kế hoạch; việc triển khai thông báo/nhật ký vẫn để sau.

## 12. Nộp bài, AI và điểm: hợp đồng sự kiện cần khóa

1. **Nộp toàn bài:** phát khi transaction `SubmitStudentAssignmentUseCase` thực sự commit lần nộp hợp lệ. Bài có câu bỏ qua vẫn là bài đã nộp; không yêu cầu `CompletedQuestionCount == TotalQuestionCount`. Retry cùng submission trả lại kết quả cũ thì không có sự kiện nộp mới. Không dùng thao tác hủy câu làm bằng chứng rằng học sinh vừa nộp bài.
2. **Phân tích toàn bài hoàn tất:** kiểm tra toàn bộ attempt có hiệu lực của lượt nộp, loại câu void theo đúng assignment; không chỉ kiểm tra job cuối cùng kết thúc. Mỗi attempt trong tập cần đã terminal, không còn job đang Processing/Pending hoặc result chưa commit từ checkpoint. Đếm số AI hợp lệ/fallback/failed; terminal không đồng nghĩa AI thành công.
3. **Nhận xét/lộ trình:** phần tổng hợp là derived state có revision và có thể chậm/lỗi sau khi analysis commit. Thông báo “phân tích xong” không được nói “nhận xét/lộ trình mới đã sẵn sàng” khi chưa có revision hợp lệ. Không chờ một request Gemini mới chỉ để soạn thông báo.
4. **Cần giáo viên duyệt:** phân biệt cấu hình Manual, cách giải khác, evidence thiếu/không chắc hoặc fallback. Không suy đoán key hết quota chỉ từ fallback; không gửi key, prompt, provider response hay ảnh nháp trong notification.
5. **Điểm cuối:** phát sau `ApproveAssignmentResultUseCase` lưu Approved cùng `FinalReviewVersion`; thông báo mở lại dùng version mới. Một lần sửa điểm câu không được gọi là toàn bài đã chốt.
6. **AI retry:** generation/lượt retry là khóa độc lập với final-review version. Nếu kết quả cũ đã bị thay thế, worker trễ không phát “thành công” cho generation cũ. Nếu thế hệ mới hoàn tất sau thông báo cũ, ghi sự kiện cập nhật có nguồn rõ; không sửa im lặng một thông báo đã gửi.
7. **Free-practice:** không tự gửi vào Teacher Review Queue hoặc nhận toàn bài của assignment khi `AssignmentId == null`. Inbox học sinh có thể nhận tóm tắt hoàn tất phiên thật nếu có định danh phiên authoritative; chưa có thì giữ trạng thái tại trang luyện tập, không tự tạo khái niệm “phiên” bằng thời gian đoán.

Mẫu tiếng Việt: “AI đã phân tích bài Test X: 48 câu có phân tích, 2 câu cần xem xét. Điểm hiện là đề xuất, chưa được giáo viên chốt.” Chỉ dùng khi các số đếm/trạng thái đúng; không lưu mẫu này làm dữ liệu demo cho tài khoản thật.

## 13. Điểm tích hợp và nguồn bằng chứng hiện có

| Nghiệp vụ | Nguồn/hook đã kiểm tra ở baseline | Điều cần bổ sung khi triển khai |
| --- | --- | --- |
| Giao bài | `Assignments/PublishAssignmentUseCase.cs` | Event trong transaction Published + materialized targets; snapshot đúng targets, không broadcast roster mới |
| Nộp toàn bài | `Assignments/SubmitStudentAssignmentUseCase.cs` | Event cùng commit progress/attempt/job/ảnh; không phát trước khi promote ảnh và commit thành công |
| AI có hiệu lực | `AssessmentAndReasoning/Processing/AIAnalysisJobProcessor.cs`, checkpoint và hàng đợi hậu xử lý | Outbox marker nhỏ trong transaction kết quả; đánh giá readiness/fan-out sau đó ở worker riêng, không thông báo chỉ vì checkpoint tồn tại |
| Yêu cầu xem xét/báo đề | `ReviewRequest/CreateStudentReviewRequestUseCase.cs` | Event cùng request mới; nhận lại request Pending cũ không gửi lần nữa; category legacy cần mapping rõ |
| Giải quyết request | Teacher approve/override/void hiện cập nhật `StudentReviewRequests` | Hook trạng thái thật; không giả định đã có endpoint “từ chối request” riêng |
| Chốt/mở lại/hủy | `ReviewQueue/AssignmentFinalReviewWorkflow.cs` và các use case gọi nó | Dùng audit/version thật; notification là projection, không đổi policy chốt/void |
| Lớp | `Organization/UpdateClassUseCase.cs`, `ClassReportsUseCase.cs` | Giữ reason/actor/scope; lưu trữ/mở lại chỉ thông báo sau commit, link read-only phù hợp |
| Giáo trình/đồ thị | `CurriculumAndQuestions/CurriculumApplicationUseCase.cs`, `AcademicDependencyGuards.cs`, graph mutations | Ledger + audit hiện có; không phát cho Draft plan hoặc thao tác dependency bị chặn |
| Phân quyền/nền tảng | `IdentityAndTenancy/*`, `Platform/PlatformAuditService.cs`/sanitizer | Dùng adapter allow-list, auth_version thật; không sao chép toàn bộ JSON audit vào hộp thư |

Đường dẫn trên tương đối với `src/EduTwin.BLL/`. Không phải danh sách file được phép sửa ngay; allow-list theo từng gate được chốt trước implementation.

## 14. Dữ liệu và độ bền đề xuất

### 14.1. Vai trò dữ liệu, chưa khóa DDL

- **Event/outbox:** `EventId`, tenant nguồn, loại/version schema, actor người hoặc System, đối tượng/version nghiệp vụ, thời điểm, payload allow-list, idempotency key; event và dữ liệu nghiệp vụ commit cùng nhau.
- **Dispatch state:** lease, fencing/version, retry count, AvailableAt, lỗi an toàn và cursor phân phối. Có thể cùng bảng outbox hoặc bảng worker riêng sau khi freeze schema; không biến audit append-only thành queue mutable.
- **Thông báo/nội dung:** template/version, snapshot tiêu đề/nội dung an toàn, phạm vi, thời điểm, nguồn event hoặc message chủ động. Sự kiện thực tế và bản tổng hợp không đồng nhất; digest giữ liên kết tới các event gốc.
- **Người nhận:** quan hệ chuẩn hóa tenant/user/notification, trạng thái delivery, `ReadAt`, `AcknowledgedAt`, ẩn khỏi inbox. Unique delivery chống trùng tại database, không chỉ kiểm tra `AnyAsync` trước insert.
- **Gửi chủ động:** draft và bản gửi bất biến; phiên bản đính chính/thu hồi nối bản gốc. Snapshot người nhận/selector có giới hạn, lý do thu hồi và lịch sử gửi; phản hồi riêng chỉ thêm nếu NF-03 được duyệt.
- **Activity view:** adapter từ lịch sử đã có hoặc event nghiệp vụ mới; lưu source type/source ID. Không nhân bản tất cả `TeacherReviewHistory`, Twin history hay audit để có thêm một bản “authoritative”.

Tên bảng/cột/type/index/API chưa đóng băng. Trước migration cần cập nhật `DATABASE_SCHEMA.md` bằng Change Proposal; không coi các bullet trên là schema thứ hai ngoài specification.

### 14.2. Chống mất/trùng/đảo thứ tự

- Bảo đảm **at-least-once + idempotency**, không quảng cáo exactly-once end-to-end. Worker có thể đọc lại event sau crash; unique `(tenant, event/message, recipient)` và business version phải ngăn inbox trùng.
- Idempotency dựa trên định danh hành động/lượt xử lý/version thật, không chỉ timestamp, câu chữ hoặc raw `RowVersion` có thể tăng vì thao tác quan sát không liên quan.
- Event của kết quả có khóa bài + học sinh + version; khóa lớp có transition/version. Phân phối theo thứ tự aggregate, không cần khóa toàn bộ tenant/hệ thống.
- Trước gửi một sự kiện trễ phải đối chiếu version/scope: phân tích cũ không lấn kết quả mới; lịch sử chính xác vẫn được giữ, notification obsolete có thể không phát nhưng ghi lý do.
- Lease hết hạn/lost race không tạo delivery mới; outbox đã commit phải phục hồi sau restart. Dead-letter là trạng thái lỗi có thể xem lại/re-drive có quyền, không xóa sự kiện gốc.
- Business commit thất bại khi ghi event thì rollback cả hai; không chạy email/network sau SaveChanges như một thay thế cho outbox. Nếu worker thông báo dừng, bài đã commit và AI vẫn chạy, các sự kiện chờ được phân phối khi phục hồi.
- Mark-read idempotent; mark-all chỉ áp dụng mốc inbox snapshot đã thấy, không đánh dấu các thông báo đến sau. Badge đếm inbox người nhận thực, không giảm optimistic tới số âm.
- Thu hồi gỡ nội dung/hành động khỏi view theo policy nhưng giữ dấu vết “đã thu hồi”; không cam kết thu hồi được nội dung người nhận đã đọc/chụp lại. Digest đổi số đếm được phân biệt với sửa một thông điệp đã gửi.

## 15. API, quyền và UI đề xuất để duyệt

### 15.1. Khả năng API, chưa phải endpoint đã tồn tại

1. Inbox list/detail/count, phân trang tối đa 100, allow-list bộ lọc; IDs string/UTC/Problem Details theo chuẩn hiện hành.
2. Đánh dấu đọc một/tất cả theo watermark; ẩn khỏi inbox và xác nhận nhận nếu notification yêu cầu. Các thao tác này không sửa status bài tập, review request hoặc điểm.
3. Preview người nhận theo selector được phép, gửi với `ClientMessageId` để chống double-click/retry; server tính lại scope khi gửi. Recipient list đổi giữa preview và send phải trả lại phạm vi mới cần xác nhận, không âm thầm phát thêm người.
4. Sent list/delivery summary và thu hồi/đính chính theo ownership + version. Người gửi chỉ xem receipt theo quyền; học sinh không thấy roster hay ai đã đọc trong lớp.
5. Activity list/detail theo actor và object scope; DTO allow-list, không expose audit raw. Action eligibility là projection từ nghiệp vụ hiện tại, không cột “đã xử lý” cho người nhận tự bật.
6. Truy cập notification ID người khác/tenant khác trả lỗi theo chính sách chống enumeration; truy vấn inbox rỗng trả 200/data rỗng. Không cho body tự đặt sender/recipient tenant hay tên route tùy ý.

Freeze route/method/error code ở `API_CONTRACTS.md` trước code; không tự tạo endpoint support/reject/retry nghiệp vụ vốn chưa tồn tại chỉ để thông báo có nút.

### 15.2. Quyền mới cần proposal, không mượn quyền quản trị có sẵn

Khả năng cần tách: đọc/update inbox của mình, gửi/read-sent/thu hồi thông báo trong scope, phản hồi/xác nhận nhận, xem activity own/scoped/administrative, và vận hành re-drive lỗi. Tên permission cuối cùng, applicability, delegable/sensitive, default system roles và policy sẽ khóa ở NF-00.

- Student: own inbox/receipt và activity an toàn của mình; không quyền gửi broadcast hoặc xem người khác.
- Teacher: own inbox, gửi lớp đang phụ trách, own sent, activity học thuật trong ownership; không đọc audit toàn tenant.
- Manager: own inbox, gửi giáo viên chính tenant, activity quản trị tối thiểu; không dùng `authorization.audit.read` làm quyền đọc tất cả nội dung học thuật.
- PlatformAdmin: quyền platform riêng, bất khả ủy quyền; inbox/gửi hỗ trợ vận hành không chứa dữ liệu học thuật đối tác.
- Role tùy chỉnh không được auto cấp toàn bộ quyền gửi mới; cập nhật system roles cần migration/backfill chính xác được duyệt, không khôi phục role đã bị thu hồi hay phá LastTenantAdmin.

### 15.3. Trạng thái giao diện

Chuông thật trên Student/Teacher/Manager/Platform layout; popup gọn có preview và “Xem tất cả”, trang inbox đầy đủ responsive. Chưa đọc mới có badge; loading/error không hiển thị số 0 như dữ liệu đã xác nhận. Nội dung tiếng Việt, không dùng AI soạn từng notification; formula nếu có dùng renderer an toàn hiện có.

Cần loading/empty/offline/stale/401/403/deleted-target/retracted state, focus trap/ESC/keyboard và aria-live không đọc lặp mỗi poll. Nhấp thông báo cũ hiển thị “tài nguyên không còn được phép truy cập” thay vì tự điều hướng sang lớp đang học khác. Cache key có tenant/user; đổi tài khoản/tenant/logout phải hủy request/timer và xóa dữ liệu riêng cũ.

## 16. Scope người nhận khi dữ liệu thay đổi

- **Bài được giao:** giữ target snapshot của sự kiện. Không thay bằng toàn lớp lúc worker chạy; học sinh mới vào lớp không mặc nhiên được giao bài cũ. Revalidate active account và quyền đọc đối tượng trước delivery/detail.
- **Học sinh rời lớp:** sự kiện thông báo rời lớp/nhận kết quả cũ chỉ là tóm tắt riêng được phép; link chỉ tới lịch sử của chính mình nếu guard cho phép. Không nhận thông báo bài mới của lớp đã rời.
- **Đổi giáo viên:** actor snapshot là người làm lúc phát sinh; người xử lý việc Pending là giáo viên hiện tại có quyền. Thông báo bàn giao cho giáo viên cũ chỉ có tóm tắt tối thiểu, không giữ bản sao bài làm/điểm giúp vượt việc thu hồi ownership.
- **Lớp lưu trữ:** không phát nhắc làm bài mới, không nhận gửi broadcast học tập mới; vẫn được gửi chốt/mở lại/review bài đã nộp theo policy lịch sử. Mở lại không khôi phục notification đã obsolete, bài chưa giao hoặc giáo trình ledger đã kết thúc.
- **Giáo trình:** người nhận học sinh theo lớp hiện đang bị ảnh hưởng và membership hợp lệ; nội dung học sinh chỉ tên/trạng thái/phạm vi được phép, không gửi Draft hoặc danh sách private material.
- **Tài khoản bị khóa/xóa mềm/tenant Suspended:** không đăng nhập/inbox bằng notification token bypass. Worker giữ/suppress có lý do; kích hoạt lại phải revalidate, không mặc định phát một loạt hành động cũ đã hết hiệu lực.
- **Liên tenant Platform–Manager:** cần envelope/relay explicit source `PLATFORM` và recipient tenant đã kiểm tra; không gán TargetUserId của tenant khác vào FK audit PLATFORM. DTO hỗ trợ chỉ metadata vận hành; nếu người dùng tự đưa bài làm/ảnh/điểm vào support message thì phải chặn/redact theo policy, không tạo backdoor học thuật.

## 17. Tốc độ, vận hành và an toàn nội dung

- Không đổi model/quota/microbatch/concurrency của AI. Luồng chấm/giao/nộp chỉ ghi event nhỏ, không render template hoặc materialize hàng nghìn delivery trong transaction chấm.
- Worker notification có batch/lease/retry/backoff/budget riêng, fair theo tenant. Index cho recipient unread/time và outbox eligible; không chạy N+1 quét toàn center theo từng recipient. Không giữ transaction khi chờ network.
- UI chỉ poll nhẹ summary/unread ở nền; mở popup/list mới lấy nội dung. Đề xuất 30 giây + jitter khi visible; pause hidden/offline, refresh khi focus, backoff lỗi. Không cộng polling 1–3 giây cho mỗi câu vào chuông.
- Polling summary tài khoản đang hoạt động không phải telemetry presence. Không lưu online heartbeat, “đang làm câu N” hoặc thời gian gõ.
- Giới hạn số người nhận/body/batch/tần suất; kiểm tra rate limit server; danh sách gửi nhiều lớp dedupe cùng học sinh. Nội dung plain text/rich text allow-list, escape XSS; không HTML tùy ý, iframe, raw file URL hoặc external tracking image.
- Link đích do server dựng từ type/ID và allow-list route; không nhận arbitrary URL để tạo open redirect/phishing. Chưa cho đính kèm ảnh/file vào thông báo chủ động trong bản đầu.
- Đo lag oldest-outbox, dispatched/retry/dead-letter/suppressed/duplicate prevented, query time và chi phí submission/AI commit. Log IDs/count/error code an toàn, không body/đáp án/key/token.
- Gate tải dùng cùng fixture/cấu hình trước–sau: 50/100 câu nhiều học sinh + nhiều thông báo gửi đồng thời. Ghi p50/p95 submission, DB commit, AI latency, notification lag và query count; ngưỡng regression chốt trước test, không hứa số giây chưa đo.

## 18. Checklist kiểm thử bổ sung

| Nhóm | Ca bắt buộc |
| --- | --- |
| Atomicity | Publish/submit/grade/void rollback không có success-event; outbox insert fail rollback; worker dừng không làm mất sự kiện đã commit |
| Chống trùng | Double-click gửi, submission ID cũ, request Pending cũ, hai worker, crash giữa fan-out và ack, lease hết hạn; unique DB giữ một delivery |
| AI | 50 câu chỉ một tổng hợp; last job về trước câu còn chạy; fallback/failed khác success; retry generation cũ về trễ; hai bước ảnh không thành hai notification; derived comment chưa sẵn sàng |
| Điểm | Đề xuất/đã chốt riêng; chốt–mở lại–chốt lại có version/thứ tự; hủy câu đúng assignment; điểm 10 và reasoning 100 không trộn |
| Nhận bài | Bài nộp có skipped vẫn được nhận; hủy hết câu không giả một submission mới; draft/draft-save không phát inbox |
| Scope | Hai tenant/bốn actor, Teacher không phụ trách, targets SelectedStudents, thêm/rút sau snapshot, đổi giáo viên/role/user status sau preview hoặc trong dispatch |
| Lịch sử | Archived chặn bài/gửi mới nhưng xem/chốt cũ hợp lệ; reopen không resurrect ledger; curriculum còn lớp dùng bị chặn không phát notification; lớp khác môn/khối không tự nhận |
| Riêng tư | Manager chỉ metadata + link báo cáo được phép; Platform không nhận học thuật; inbox/snippet/sent receipts không lộ bạn khác; audit raw/token/ảnh không vào payload |
| Trạng thái UI | Polling bị pause/reconnect; 401/403/0 thật khác lỗi; badge đúng sau read/ack; mark-all snapshot không đọc notification mới; cache không lẫn tài khoản; responsive/keyboard |
| Gửi/thu hồi | Preview thay scope phải xác nhận lại; dedupe nhiều lớp; reply riêng; XSS/open redirect/rate limit; thu hồi có dấu vết, không tự chốt business task |
| MySQL/triển khai | FK composite/unique/index/EXPLAIN, lost race/re-drive, migrations mới và upgrade bản sao; không hard-delete evidence/audit; không gửi thông báo cũ hàng loạt khi backfill |
| Hiệu năng | Đo trước–sau bulk AI + fan-out; notification không tiêu request Gemini, không đổi admission/lease của AI; budget worker không chiếm connection pool vô hạn |

Unit tests không gọi Gemini thật; dùng TimeProvider và schema MySQL cô lập. Nếu cần gửi thông báo thật/test UI trên tài khoản localhost phải xác nhận recipient và nội dung trước; không tự phát broadcast toàn trung tâm để làm bằng chứng.

## 19. Rà soát tài liệu và Change Proposal cần duyệt

Đã tìm kiếm toàn bộ 71 Markdown tracked/unignored của repository theo chủ đề thông báo, nhật ký, quyền, vòng đời và AI; đọc trọn kế hoạch này và ADR queue, đối chiếu các phần authoritative và báo cáo release/lifecycle liên quan. Không coi mọi báo cáo lịch sử là specification hiện hành hay tuyên bố đã đọc lại từng dòng của toàn bộ 71 tệp.

| Tài liệu | Kết luận của lượt rà soát | Việc trước implementation |
| --- | --- | --- |
| Kế hoạch này (06/10) | Nhu cầu còn đúng nhưng thiếu scope/lifecycle/AI-generation và contract/test cụ thể | Bản bổ sung 10/10 đang chờ duyệt |
| ADR queue/notifications (28/09) | Là đề xuất push trạng thái AI, không phải hệ thống inbox; số mô phỏng không phải benchmark hiện tại | Gắn ghi chú historical; không triển khai SignalR/broker từ ADR trong task này |
| `CONSTITUTION.md` §3.2/4.2/17–20/27 | Baseline MVP chưa mở SignalR/broker; outbox/new module là thay đổi có kiểm soát; các mục cũ còn thông tin số bảng/language lỗi thời | Amendment được duyệt cho thông báo, giữ modular monolith/tenant/transaction; không dùng notification để sửa thuật toán hay đại tu các scope khác |
| `DATABASE_SCHEMA.md` §50 | Vẫn liệt kê Notification/email/chat ngoài MVP và header số bảng lịch sử | Khóa additive schema NF-00, cập nhật số bảng từ model/migration thực; không tự coi bảng mới đã được duyệt |
| `API_CONTRACTS.md` | Có conventions/audit endpoints, chưa có notification API | Freeze API mới không drift route cũ, phân tách Platform–tenant rõ |
| `PROJECT_REQUIREMENTS.md`, `UI_UX_SPEC.md` | Có invariant account/tenant/evidence, chưa có requirement và sitemap thông báo đầy đủ | Thêm IDs/scope/acceptance cho feature sau duyệt; không auto chuyển PENDING thành VERIFIED |
| `MASTER_PLAN.md`, `PROJECT_TRACKING.md`, `TEAM_ASSIGNMENT.md`, `CODEBASE_CHANGE_PLAN.md`, `PROMPT_TEMPLATES.md`, `README.md` | Nhiều đoạn là baseline/provenance, không được biến thành hiện trạng 10/10 hoặc approval | Ghi gate, human owner, allow-list, dependencies và state đúng; tracking chỉ append review này |
| Báo cáo release 10/10 và các báo cáo lớp/giáo trình/đồ thị 08–09/10 | Nguồn evidence của các guard/scope đã thay đổi; đều nói feature thông báo chưa làm | Tái sử dụng source thật, không sửa các kết quả kiểm chứng lịch sử để giả đã nghiệm thu notification |

### CP-20261010-NOTIFICATIONS — PRODUCT SCOPE APPROVED; IMPLEMENTING NF-00/NF-01

- **Vấn đề:** chuông chưa có dữ liệu; nghiệp vụ có audit rời rạc, thiếu phân phối đúng người và tóm tắt trạng thái sau các thay đổi học thuật.
- **Phương án A:** chỉ nối UI tới các bảng history hiện có. Ít thay đổi nhưng không giải quyết gửi chủ động, read receipt, durability/fan-out hoặc chống trùng.
- **Phương án B (khuyến nghị):** feature in-app riêng trong monolith, event/outbox giao dịch + worker bounded + inbox + adapter activity; rollout theo NF-00…05. Không broker/realtime/AI mới.
- **Tác động:** thêm schema additive/index/unique/permission và API/UI được freeze; sửa các business hooks trong allow-list gate. Cần test transaction, isolation, migration và bulk grading regression. Không reset dữ liệu hoặc tạo lại các bài Test 4–9.
- **Quyết định:** người dùng cho phép bắt đầu NF-00/NF-01 sau khi trao đổi UI/SQL. Schema/API/permissions của checkpoint đầu được khóa tại NF-01 SPEC; gửi chủ động/retention production vẫn chờ gate sau. Chưa ghi timeline chắc chắn hoặc tên reviewer chưa được giao.

## 20. Tóm tắt phần bổ sung ngày 10/10

1. Cập nhật hiện trạng lịch sử có thật, baseline và trạng thái chỉ làm tài liệu.
2. Tách nộp bài, AI hoàn tất, cần duyệt và điểm chốt; chống stale retry/generation và thông báo trùng.
3. Thêm lớp Archived/History, ledger giáo trình, graph dependencies và đường dẫn Manager chỉ xem.
4. Bổ sung outbox/fan-out/lease/idempotency/mark-all/thu hồi và recipient revalidation.
5. Phác API/permissions/UI, vận hành an toàn và test; giữ mọi đề xuất mới ở trạng thái pending.
6. Giữ tốc độ chấm: không thêm Gemini, không đổi AI queue, worker riêng/polling thưa; benchmark trước–sau khi implementation.
7. Chỉ sửa kế hoạch này, ghi chú ADR lịch sử và tracking; chưa sửa source, schema, API hiện hành, `.env`, dữ liệu hay quyền.

**Checkpoint hiện tại:** hoàn thiện kiểm chứng và rollout NF-02/NF-03b/NF-04/NF-05 theo Completion SPEC; ghi riêng ca đạt, ca lỗi đã sửa nhưng chưa chạy lại, và ca chưa kiểm tra. Chủ sở hữu sản phẩm là người dùng; chưa có reviewer con người được giao, không ghi tự nghiệm thu thay nhóm. Các đoạn hiện trạng NF-01 và bổ sung kế hoạch phía trên là provenance của checkpoint cũ, không phải tuyên bố runtime hiện tại chưa có gửi chủ động.
