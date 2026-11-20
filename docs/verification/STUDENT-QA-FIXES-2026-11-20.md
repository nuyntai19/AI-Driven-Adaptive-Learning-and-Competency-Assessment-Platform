# Bản sửa sau QA student — 20/11/2026

Phạm vi: 13 vấn đề trong `STUDENT-DEEP-QA-2026-11-20.md`, yêu cầu chọn kiểu nhập/trích đáp án bằng mã môn và chặn đường lộ đáp án kỳ thi chưa công bố qua bookmark.

## Các thay đổi

| Vấn đề | Cách sửa |
|---|---|
| Kiểu nhập phụ thuộc tên môn | Backend trả `subjectCode` trong chi tiết bài tập và các DTO luyện tập. Ba màn hình bài tập, hoạt động lộ trình và luyện tập dùng mã để gọi `usesMathInput`; không dùng tên hiển thị hoặc ngôn ngữ câu hỏi để chọn editor. `ENG` dùng textarea và chính sách lấy đủ nội dung trả lời, kể cả khi đổi tên môn. |
| UI-CONTENT-01 | Đổi quy tắc xét dấu thành ngoài hai nghiệm cùng dấu a, trong hai nghiệm trái dấu a, nêu điều kiện Δ>0. Có data migration chỉ thay đoạn sai đã biết trong description của seed, giữ phần nội dung còn lại. |
| UI-PRACTICE-05 | Nháp văn bản/lựa chọn lưu trong sessionStorage theo trung tâm + tài khoản + phiên + câu; khôi phục khi quay lại/reload, xóa sau nộp thành công. Báo lỗi khi không lưu được; không tuyên bố có đồng bộ nháp đa thiết bị. |
| UI-DATE-01 | Định dạng ngày từ ISO thay vì đảo toàn bộ chuỗi timestamp bằng split. |
| UI-FLOW-01 | Nhãn chuyển phần đầu vào xét phần còn lại chưa hoàn thành, không dựa vào `canStart` đang bị khóa trong agenda cũ. |
| UI-A11Y-01 | Modal làm bài, mục tiêu, mở rộng dùng hook focus/Escape/scroll chung. Popup xác nhận nền tảng và cảnh báo deadline có vùng focus riêng; khi unmount trả focus về nút mở. |
| UI-DASH-LOGIC-01 | Dashboard dùng forecast có bằng chứng cho cả mục tiêu cũ và mới, trả `hasPrediction=false` khi chưa đủ dữ liệu. UI không hiển thị điểm dự đoán/khoảng cách/rủi ro như những con số đã biết trong trạng thái này. |
| UI-GOAL-04 | Kiểm tra lại ước lượng sau mỗi lần tự điều chỉnh thời lượng; dừng khi không còn cần tăng hoặc chạm trần. Lời hiển thị nói rõ đây là điều chỉnh theo ước lượng ban đầu và kết luận khả thi mới nhất nằm bên dưới. Bỏ cách diễn đạt tăng từ điểm hiện tại lên chính điểm đó. |
| UI-CARDS-LOGIC-01 | Bookmark ghép nhãn với nội dung lựa chọn để thẻ ôn có thể đối chiếu đáp án đầy đủ. Không đưa đáp án/lời giải vào response khi chưa được phép công bố. |
| UI-NOTIFY-LOGIC-01 | Thông báo hoàn tất xử lý mới phân biệt phân tích AI và chấm dự phòng, dùng lời dễ hiểu, không gọi fallback/terminal là AI đã hoàn tất. Không sửa lịch sử thông báo cũ. |
| UI-NOTIFY-02 | Hộp thư/nhật ký xác thực khoảng ngày trước khi gửi request, hiện lỗi cụ thể và tiếp tục dùng bộ lọc hợp lệ gần nhất. |
| UI-EXAM-STATE-01 | Kỳ thi chưa công bố có khối trạng thái riêng; không hiển thị bảng số câu đúng/AI đang xử lý suy từ dữ liệu đã bị ẩn. |
| UI-REVIEW-COPY-01 | Khi giáo viên đã xác nhận và không có yêu cầu pending mới, ưu tiên nhận xét giáo viên thay cho đoạn fallback nói còn cần xem xét. Tên popup không giả định kết quả do AI chấm. |
| UI-EMPTY-SCOPE-01 | Giao diện lộ trình chỉ chọn trong môn thuộc lớp của học sinh, dùng bộ nhớ phạm vi theo tài khoản. Chưa có lớp thì giải thích cần được phân công; luyện tập không hiển thị query bị disable như đang tải mãi. Không thay đổi API tự học theo thư viện khối đã được duyệt. |
| Bổ sung: đáp án qua bookmark | Kỳ thi Midterm/Final đã đóng nhưng chưa có `resultsPublishedAt` vẫn phải giấu đáp án/lời giải trong thư viện thẻ. |

`usesMathInput` tập trung chính sách theo mã: ENG/EN/ENGLISH và LIT/VAN/LITERATURE là văn bản. Các mã còn lại giữ cách nhập toán hiện có. Tên môn chỉ dùng để hiển thị. Nếu thêm môn ngôn ngữ với mã khác thì bổ sung mã vào chính sách này.

## Kiểm tra đã chạy

- Frontend: **951 PASS, 0 FAIL**; chạy bổ sung nhóm hồi quy mã môn/nháp/ngày/popup trên mã cuối: **13 PASS** (không cộng nhóm trùng vào tổng 951).
- Backend liên quan: **845 PASS, 48 SKIP, 0 FAIL**, tổng 893. Các test bỏ qua yêu cầu cấu hình MySQL integration riêng, không được coi là đạt.
- Backend gồm hai hồi quy bookmark mới: đáp án trắc nghiệm có nội dung và kỳ thi đã đóng vẫn giấu đáp án trước công bố.
- Data migration sửa dữ liệu cũ; không cần thay đổi schema cho subjectCode vì cột mã môn đã tồn tại.

HTTP/MySQL, kiểm thử trình duyệt và trạng thái runtime được ghi ở phần xác minh sau khi hoàn tất. Raw evidence thuộc thư mục bị ignore `docs/verification/evidence/student-fixes-2026-11-20/`.
