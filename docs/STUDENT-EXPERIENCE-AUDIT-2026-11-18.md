# EduTwin — rà soát sản phẩm và nâng cấp Learning Studio

Ngày: 18/11/2026. Phạm vi: đọc requirements, invariant và các tuyến/controller liên quan; rà soát frontend học sinh và thực hiện nâng cấp trải nghiệm. Đây là đánh giá từ mã nguồn và kiểm thử frontend, không phải chứng nhận toàn bộ nghiệp vụ backend đã nghiệm thu.

## 1. Đánh giá

EduTwin có độ phủ chức năng học tập tốt hơn một website giao/nộp bài thông thường. Điểm khác biệt đáng đầu tư là vòng lặp: biết mình đang ở đâu → chọn việc tiếp theo → làm bài → hiểu phản hồi → thấy năng lực thay đổi. Ưu tiên hoàn thiện vòng lặp này trước khi bổ sung diễn đàn, mạng xã hội hay bảng xếp hạng.

| Nhóm | Bằng chứng trong mã nguồn | Nhận định / điều còn cần kiểm chứng |
| --- | --- | --- |
| Đăng nhập, phiên, quyền | auth, ProtectedRoute, PermissionRoute, AccountSecurityController | Có nền tảng; cần chạy lại kịch bản hết phiên giữa lúc đang làm bài trong môi trường đầy đủ. |
| Phạm vi môn/lớp/giáo trình | StudentAcademicContext, studentAcademicNavigation, useStudentLearningAccess | Có xử lý lịch sử và chặn ghi; phải giữ nguyên khi làm mới giao diện. |
| Bài tập, deadline, tiếp tục làm | StudentAssignmentsPage, StudentAssignmentDetailPage, LearningPlayerPage | Có timeline, bộ lọc, draft, phản hồi; cần acceptance với bài chờ giáo viên chấm, quá hạn, mất mạng. |
| Lộ trình cá nhân | StudentLearningPathPage, learningPathV4, learningPathApi | Đã có agenda, đầu vào, nhiệm vụ, ôn, mục tiêu. Tổng quan phải dùng cùng agenda thay vì tạo bộ khuyến nghị thứ hai. |
| Luyện thích ứng | AdaptivePracticeOverview/Session/Summary, AdaptivePracticeController | Có bốn cách luyện, thời lượng, lịch sử. Ưu tiên giải thích lý do chọn chủ đề và đối chiếu kết quả thực tế. |
| Hồ sơ năng lực | StudentTwinPage, dashboard, trend, journey, history | Có bằng chứng và giải thích. Không coi chưa có dữ liệu là năng lực bằng 0; không coi mọi biến động là tiến bộ. |
| Công cụ học tập | MathLive, Casio, scratchpad, grapher | Là điểm mạnh sẵn có; giữ trong không gian làm bài, tránh dồn toàn bộ ra trang chủ. |
| Lưu câu để ôn | StudentQuestionBookmarksController, useQuestionBookmarks, BookmarkedQuestions | Đã có lưu server. Đợt này đưa vào vòng ôn chủ động ngay ở tổng quan. |
| Thông báo và nhật ký | NotificationBell, NotificationsPage, ActivityController | Đã có tuyến và capability. Cần kiểm tra thông báo dẫn vào đúng tài nguyên còn quyền. |
| Giáo viên | Question bank/import, curriculum, assignment, grading, review, twin | Có hệ sinh thái để vận hành vòng học. Không thay thế quyền chấm của giáo viên bằng điểm do UI hay AI tự tính. |
| Quản lý / nền tảng | Organization, Authorization, PlatformOperations và Audit | Đã có phân hệ; không suy rộng quyền đọc học thuật của PlatformAdmin. |

Kết luận về độ đầy đủ: các nhóm chức năng lõi đã hiện diện trong source. Chưa thể kết luận “đủ và hoạt động đúng toàn bộ” chỉ từ số trang hoặc README; cần nghiệm thu theo các hành trình ở mục 5.

## 2. Thay đổi đã triển khai

- Tổng quan theo hướng Learning Studio: nền giấy nhẹ, khối nhiệm vụ xanh rừng, CTA màu kem, nhịp bố cục bất đối xứng và thẻ khám phá có màu riêng. Giữ màu thương hiệu đỏ cam trong điều hướng và hành động thông thường.
- Đưa kế hoạch, phần cần luyện và mục tiêu lên trước biểu đồ. Thứ tự DOM đi cùng thứ tự thị giác để bàn phím và trình đọc màn hình đi đúng luồng.
- Ba lối khám phá dẫn tới lộ trình, luyện thích ứng, hồ sơ năng lực và giữ nguyên môn/lớp trên URL.
- Hướng dẫn khởi động ba bước xuất hiện khi chưa có mục tiêu hoặc bằng chứng. Trạng thái dựa trên goal, evidence và studiedToday từ hệ thống; không tự đánh dấu hoàn thành bằng thao tác click.
- Góc tập trung dùng xuyên các trang học sinh: chọn 10/25/45 phút, bắt đầu, tạm dừng, tiếp tục, đặt lại, thu gọn, Escape, giữ focus trong dialog. Dùng deadline theo thời gian thực để không chậm khi tab nền bị giảm nhịp. Không cộng thành điểm hay bằng chứng nộp bài. Tải lại trang sẽ đặt lại bộ đếm.
- Góc ôn cá nhân: lấy câu đã đánh dấu và đã trả lời thuộc môn đang xem. Học sinh tự nhớ trước, sau đó mở đáp án/lời giải và chuyển câu. Chỉ trình bày dữ liệu đã được server cấp; không sinh hay suy ra đáp án chưa công bố. Đây là luyện nhớ tự chủ, chưa phải thuật toán lên lịch spaced repetition mới.
- Header chung cho bài tập, lộ trình, luyện tập, Twin; nền, card, focus ring, responsive và giảm chuyển động đồng bộ.
- Skip link đến nội dung chính; nút tập trung đặt trên thanh điều hướng điện thoại và dưới lớp modal.
- Chưa tải chuỗi học hiển thị “—”; lỗi tải bài tập có thông báo và nút thử lại; nhãn biến động không khẳng định sai là tiến bộ; mục cần chú ý chỉ liệt kê chuyên đề có bằng chứng và dưới 75%.
- Tăng cửa sổ lấy bài tập trên tổng quan từ 10 lên 100. Đây vẫn là danh sách phân trang, không cam kết tìm deadline nhỏ nhất trên toàn bộ lịch sử nếu vượt quá 100 bản ghi.

## 3. Lý do lựa chọn

Mục tiêu nhỏ và chuỗi ngày là ý tưởng tham khảo từ [Duolingo về mục tiêu và động lực](https://blog.duolingo.com/sticking-with-it-tips-for-staying-motivated/). Thẻ ôn dựa trên nguyên tắc tự truy hồi trước khi xem lại, tham khảo [Retrieval Practice FAQ](https://www.retrievalpractice.org/faq). Áp dụng vào EduTwin là quyết định thiết kế của đợt này; chưa có nghiên cứu người dùng hay thử nghiệm A/B chứng minh tác động giữ chân trên EduTwin.

Không thêm XP giả, huy hiệu không có dữ liệu, đếm thời gian như bằng chứng mastery, hoặc bảng xếp hạng lấy điểm AI. Những cơ chế này có thể khiến học sinh tối ưu chỉ số thay vì hiểu bài.

## 4. Cải tiến tiếp theo theo ưu tiên

| Ưu tiên | Cải tiến | Tiêu chí nghiệm thu |
| --- | --- | --- |
| P1 | API work queue gọn, sắp deadline tại server | Tổng quan trả đúng bài có thể làm và gần hạn nhất ngay cả khi có hơn 100 bài; có trạng thái chưa tải/lỗi riêng. |
| P1 | Báo cáo tuần có giải thích | Tổng hợp bài đã hoàn thành, năng lực có evidence, phần còn yếu và một hành động tiếp theo, theo timezone trung tâm. |
| P1 | Sổ lỗi sai có phân loại | Chỉ tổng hợp sai sót được xác nhận; dẫn về lời giải/feedback đã được công bố; phân biệt bài chờ review. |
| P1 | Nghiệm thu phục hồi phiên làm bài | Mất mạng/refresh/hết phiên không mất draft; submit lại không tạo attempt trùng. |
| P2 | Lịch học thống nhất | Kết hợp deadline giáo viên, lịch thi và agenda cá nhân; không tự tạo lịch thi từ suy đoán. |
| P2 | Thành tựu có căn cứ | Milestone từ hoàn thành nhiệm vụ và evidence, server chống ghi nhận trùng; không dùng bộ đếm cá nhân. |
| P2 | Trợ giúp theo ngữ cảnh | Hướng dẫn ngắn cho lần đầu nhập công thức, xem Twin, đọc feedback; có thể mở lại. |
| P2 | Tối ưu thư viện nặng | Tiếp tục tải MathLive, đồ thị và trình soạn khi thực sự mở; theo dõi bundle budget, thời gian tương tác. |
| P3 | Thử nghiệm người dùng | 5–8 học sinh làm tác vụ không được chỉ dẫn; đo thời gian tìm bài tiếp theo, tỷ lệ hoàn thành, hiểu nhầm điểm dự đoán. |

## 5. Checklist nghiệm thu toàn hệ thống

1. Học sinh mới: chọn môn/lớp hợp lệ → mục tiêu → đầu vào → nhiệm vụ đầu tiên → feedback → Twin có evidence.
2. Học sinh quay lại: tìm đúng bài đang làm, deadline và phiên luyện; chuyển trang không mất trạng thái thuộc luồng đang học.
3. Bài tự luận: chờ giáo viên chấm hiển thị đúng; không tự biến preliminary thành điểm cuối hoặc mastery.
4. Lớp lưu trữ/rời lớp: chỉ xem lịch sử, không tạo phiên hay nộp mới; chuyển môn không mang theo topic/lớp không hợp lệ.
5. Gemini lỗi: fallback vẫn hoàn tất theo invariant; UI giải thích nguồn phản hồi.
6. Bookmark: câu chưa công bố đáp án không lộ lời giải; ôn đúng môn, đúng tài khoản.
7. Điện thoại và bàn phím: không tràn ngang, dialog cuộn được, Tab/Escape/khôi phục focus; kiểm tra thêm với tên dài và công thức dài.
8. Giáo viên: tạo → publish → giao → nhận bài → review/override → học sinh thấy kết quả cập nhật có lịch sử.
9. Tenant isolation: account khác trung tâm và PlatformAdmin không đọc bài/Twin ngoài quyền.

## 6. Giới hạn kiểm chứng

- Kiểm tra giao diện mới bằng fixture riêng có nhãn dữ liệu mẫu, render component thật; không dùng dữ liệu mẫu trong sản phẩm.
- Không thay schema, endpoint, thuật toán mastery hoặc phân quyền backend trong đợt này.
- Kết quả kiểm thử và ảnh được ghi trong `docs/verification/student-studio-2026-11-18/README.md`.

## Cập nhật tiếp theo: Sổ tay học tập (18/11/2026)

Đã triển khai trang sổ tay riêng, phân tích 30 ngày từ API có sẵn, câu cần xem lại với nguồn dữ liệu rõ ràng, và thư viện/lượt ôn chủ động. Đồng thời cải tiến bộ chọn phiên và các trường hợp lỗi, đồng bộ cache sau phiên, tách tải bộ đếm để giữ ngân sách bundle. Báo cáo tuần và lịch ôn bền vững trong roadmap vẫn là các mục riêng, chưa được coi là hoàn thành bởi chức năng tự đánh giá thẻ ôn này.

Chi tiết và bằng chứng: [Learning Notebook](verification/learning-notebook-2026-11-18/README.md).
