# Trung tâm thông báo và Nhật ký hoạt động EduTwin

Ngày ghi nhận: 2026-10-06.

Trạng thái: **ĐỂ TRIỂN KHAI SAU**. Người dùng yêu cầu lưu định hướng vào tài liệu và tiếp tục xử lý các hạng mục khác trước. Tài liệu này không phải xác nhận tính năng đã được triển khai hoặc cho phép tự động bắt đầu triển khai.

## 1. Mục tiêu và hiện trạng

Các tác nhân cần biết ai đã thực hiện hành động, hành động tác động đến ai, trạng thái công việc hiện tại và việc nào cần xử lý. Một hệ thống chung có hai nguồn thông báo: sự kiện nghiệp vụ tự động và thông báo người dùng chủ động soạn.

Hiện trạng đã kiểm tra trong code:

- `TeacherReviewHistory` lưu một số lịch sử xác nhận/điều chỉnh câu hỏi.
- `AssignmentFinalReviewWorkflow` ghi các sự kiện chốt, mở lại kết quả và hủy câu vào nhật ký hiện có.
- Có nhật ký phân quyền và lịch sử thay đổi hồ sơ năng lực.
- Chưa có màn hình nhật ký nghiệp vụ thống nhất cho các tác nhân và chưa có hệ thống thông báo liên tác nhân đầy đủ.
- Chuông học sinh trong `StudentLayout.tsx` hiện là giao diện với chấm đỏ cố định, chưa có nguồn dữ liệu thông báo thật.
- Chưa có chức năng gửi thông báo chủ động hoàn chỉnh giữa Admin, Center Manager, Teacher và học sinh lớp được phụ trách.

Không mô tả hiện trạng thành "hoàn toàn chưa lưu lịch sử": lịch sử đã tồn tại một phần, nhưng còn rời rạc và thiếu giao diện/phân phối thông báo.

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
| Học sinh yêu cầu xem xét kết quả | Giáo viên phụ trách | Phân loại riêng với báo cáo đề sai; có liên kết đến yêu cầu cần xử lý |
| Học sinh báo cáo đề sai | Giáo viên phụ trách | Nêu câu/bài liên quan và loại sự cố, không phát cho học sinh khác |
| Giáo viên xử lý yêu cầu | Học sinh gửi yêu cầu | Nêu đã giải quyết/từ chối, lý do được phép xem và kết quả liên quan |
| Giáo viên chốt toàn bài | Học sinh tương ứng | Thông báo kết quả đã chốt; không gọi lần lưu điểm trung gian là kết quả cuối cùng |
| Mở lại kết quả đã chốt | Học sinh tương ứng | Nêu đang rà soát; bài nộp gốc vẫn được giữ, không phải yêu cầu làm lại |
| Điều chỉnh và chốt lại | Học sinh tương ứng | Lịch sử trước/sau, lý do phù hợp và phiên bản kết quả |
| Hủy câu do lỗi đề | Các học sinh được giao bài bị ảnh hưởng | Nêu phạm vi bài tập, cách tính điểm và trạng thái chờ chốt lại; không ảnh hưởng bài khác dùng chung câu |
| Xuất bản/gán hoặc thay đổi giáo trình ảnh hưởng lớp | Giáo viên/học sinh liên quan | Chỉ nội dung được phép công bố; không gửi bản nháp cho học sinh |
| Thêm/chuyển/rút học sinh khỏi lớp | Học sinh đó và giáo viên lớp liên quan | Chỉ thông báo thao tác thành công và đúng người bị ảnh hưởng |
| Đổi giáo viên phụ trách | Giáo viên cũ, mới và học sinh lớp | Thông báo bàn giao; việc đang chờ được định tuyến đúng người phụ trách mới |
| Đổi quyền/trạng thái tài khoản | Người bị ảnh hưởng và người quản trị có thẩm quyền | Không chứa mật khẩu, token hoặc thông tin bảo mật |
| Giáo viên tạo giáo trình/bài tập | Center Manager có quyền trong trung tâm | Nhật ký/thông báo tổng hợp về hoạt động; không tự cấp quyền mở học liệu |

Center Manager nhận thông tin quản trị tối thiểu: người thực hiện, loại hoạt động, thời gian, đối tượng/lớp khi được phép. Không gửi mặc định bài làm, ảnh nháp, đáp án mẫu hoặc chi tiết điểm riêng của học sinh cho Manager.

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
- Phản hồi theo ngữ cảnh của thông báo và đúng người được phép xem.
- Thu hồi/đính chính có lưu dấu vết; không sửa hoặc xóa âm thầm nội dung đã gửi.

### Nhật ký hoạt động

- Ai làm, làm gì, khi nào, trên đối tượng nào, kết quả thao tác.
- Thay đổi điểm/trạng thái quan trọng có dữ liệu trước/sau, lý do, phiên bản, các lần chốt/mở lại.
- Có bộ lọc và liên kết đến tài nguyên còn được phép truy cập.
- Học sinh chỉ xem phần của mình; giáo viên chỉ xem phần công việc được phụ trách; Manager/Admin xem theo quyền quản trị tương ứng.
- Không mở nguyên bảng nhật ký kỹ thuật/phân quyền cho mọi tác nhân; cần API trả dữ liệu đã lọc và che thông tin nhạy cảm.

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
- Lịch sử cũ chỉ backfill từ bằng chứng có thật trong các bảng lịch sử; không dựng sự kiện giả từ `UpdatedAt` hoặc dữ liệu mẫu.
- Chưa mở rộng sang email, SMS hoặc push bên ngoài trong phạm vi ban đầu.
- Không coi việc thông báo/nhật ký là lý do khôi phục các quyền học thuật đã bỏ của Center Manager.

## 8. Thứ tự triển khai khi người dùng yêu cầu tiếp tục

1. Chốt danh mục sự kiện, quyền gửi/xem và chính sách người nhận.
2. Xây nền tảng lưu sự kiện/phân phối thông báo và API đọc/đã đọc.
3. Hoàn thiện luồng giao bài → nộp bài → yêu cầu xem xét/báo lỗi → xử lý → chốt/mở lại/chốt lại/hủy câu.
4. Xây màn hình chung theo tác nhân và nhật ký theo bài tập/kết quả.
5. Bổ sung gửi thông báo chủ động, đặc biệt Giáo viên → Học sinh lớp phụ trách, và phản hồi có phạm vi.
6. Bổ sung giáo trình, chuyển lớp, phân công giáo viên và quản trị tài khoản.
7. Kiểm tra đủ actor isolation, độ bền, chống trùng và tính riêng tư trước khi nghiệm thu.

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

- Chính sách giữ lịch sử/notification, thời hạn và lưu trữ.
- Phạm vi phản hồi, mức ưu tiên, xác nhận nhận và giới hạn gửi hàng loạt.
- Có mở liên lạc Teacher–Teacher hoặc Admin–Teacher không; mặc định chưa mở.
- Cách tổng hợp thông báo cho Manager và giáo viên khi có nhiều sự kiện.
- Cơ chế cập nhật giao diện và xử lý người nhận chuyển lớp/đổi vai trò sau lúc phát sự kiện.

Chỉ bắt đầu triển khai khi người dùng yêu cầu tiếp tục hạng mục này.

## 11. Phạm vi đã chốt ngày 2026-10-06

Người dùng loại bỏ **giám sát học sinh thời gian thực** khỏi hạng mục nâng cấp học thuật: không xây presence/heartbeat, đếm online hoặc theo dõi câu đang làm. Giữ màn hình tiến độ dựa trên dữ liệu bài nộp hiện có. Quyết định này không đồng nghĩa xóa các thông báo nghiệp vụ đã lưu trong kế hoạch; việc triển khai thông báo/nhật ký vẫn để sau.
