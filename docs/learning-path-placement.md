# Kiểm tra đầu vào và cập nhật lộ trình

> Bản nghiệp vụ mới: [Lộ trình học tập V4](plans/learning-path-v4-revision.md). Trạng thái triển khai: [báo cáo V4](plans/learning-path-v4-implementation.md).

Trang lộ trình có một nút “Bắt đầu lộ trình”; trong hộp thoại, học sinh chọn kiểm tra đầu vào theo phạm vi được chọn hoặc bỏ qua để học ngay từ nền tảng. Checklist gom các kỹ năng lá theo chủ đề trong giáo trình; chọn một chủ đề chọn các kỹ năng con. Quan hệ tiên quyết lấy từ Knowledge Graph, bao gồm quan hệ giữa các chủ đề cha được mở rộng xuống kỹ năng lá. Với nền tảng chưa có bằng chứng trên web, học sinh chọn bổ sung vào tuyến hoặc tự xác nhận đã học ở trường và bỏ qua. Không tự mở rộng tuyến qua các nền tảng đã chọn bỏ qua.

## Luồng kiểm tra đầu vào → lộ trình

1. Bấm “Bắt đầu lộ trình”, giữ lựa chọn “Kiểm tra đầu vào · Đề xuất”, chọn một, vài hoặc tất cả các phần (chủ đề) rồi bấm “Bắt đầu kiểm tra”. Không có bước xem trước: bài kiểm tra được tạo và mở ngay.
2. Các phần nối tiếp nhau trong một bài (`Kiểm tra đầu vào · phần x/y`). Đóng giữa chừng thì lưu lại; nút "Tiếp tục kiểm tra" làm tiếp.
3. Xong phần cuối và chấm xong, hộp "Lộ trình đề xuất từ kết quả kiểm tra" tự mở: điểm từng phần, nhận xét, rồi lộ trình chi tiết. Học sinh có thể tùy chỉnh (phần học, số câu, lịch) trước khi chọn.
4. Phần không chọn kiểm tra giữ trạng thái chưa biết; không tự biến thành "Tự đánh giá đã vững". Chỉ lựa chọn bỏ qua nền tảng có chủ đích mới được lưu là tự xác nhận; lựa chọn đó không trở thành bằng chứng năng lực.
5. Ôn xác nhận đạt: giữ lịch ôn giãn cách. Chưa đạt: phần đó thành phần học bình thường, thêm bộ rèn lại và bài tự kiểm tra 5 câu.

## Kiểm tra đầu vào

- `StartWithPlacement` tạo một tuyến kiểm tra riêng và giữ nguyên loại mục tiêu học: mỗi kỹ năng dùng 3–5 câu độc lập chưa làm/chưa lộ lời giải. Mẫu dưới 5 câu chỉ có độ tin cậy thấp và không xác nhận vững. Có thể lưu tiến độ theo từng phần.
- Kiểm tra đầu vào có thể đánh giá một kỹ năng khi nền tảng chưa được xác nhận. Với tuyến học thông thường, mỗi nền tảng cần có bằng chứng hợp lệ hoặc được học sinh tự xác nhận bỏ qua trong chính tuyến đó. Lựa chọn này không đổi trạng thái năng lực toàn môn và không mở khóa các nền tảng khác chưa được tự xác nhận.
- Phần đã có bằng chứng độc lập hợp lệ được dùng lại, không phát sinh điểm kiểm tra mới. Chỉ còn 0–2 câu hợp lệ thì ghi rõ thiếu câu, không tính là 0 điểm và không chặn tạo tuyến học.
- Không thêm vòng rèn lại hoặc lịch ôn trong giai đoạn thu thập đầu vào. Sau khi chấm từng phần, báo cáo tổng hợp điểm theo kỹ năng, chủ đề, toàn bài và ước lượng số bộ luyện. Điểm lấy từ điểm chấm thực tế, gồm điểm thành phần và điểm giáo viên sửa; không dùng điểm của các môn hoặc lượt luyện khác.
- Báo cáo đang làm chỉ tính phần đã hoàn thành. Học sinh có thể xem tuyến tạm với phần chưa kiểm tra được đánh dấu chưa đủ bằng chứng; không chuyển tuyến khi còn lượt đang làm hoặc chờ chấm.

## Tuyến luyện và chặng tiếp theo

- “Lập lộ trình từ kết quả” giữ phạm vi do học sinh chọn và lựa chọn bỏ qua nền tảng; bộ lập tuyến bổ sung các tiên quyết chưa bỏ qua và dùng bài làm đã có để điều chỉnh độ khó, số bộ luyện và câu từng sai. Số bộ là ước lượng, cập nhật sau từng bộ. Bản xem trước và trang lộ trình ghi rõ nền tảng tự xác nhận; cập nhật và tùy chỉnh không tự thêm lại các phần này.
- “Cập nhật lộ trình từ bài làm” điền lại phạm vi, loại mục tiêu, thời gian mỗi ngày, số ngày mỗi tuần và hạn mục tiêu. Luôn xem trước rồi mới xác nhận thay tuyến.
- Năng lực dựa trên 5 câu độc lập có bằng chứng hợp lệ gần nhất trong 30 ngày, mỗi câu chỉ tính một lần. Câu đã có hướng dẫn/lộ lời giải và bằng chứng cần giáo viên duyệt không tự xác nhận năng lực. Ngưỡng là 80%; nếu quá nửa câu là trắc nghiệm thì cần đúng toàn bộ, giống bộ đánh giá nhiệm vụ.
- Chỉ đề nghị sang chặng tiếp theo khi tất cả kỹ năng trong phạm vi tuyến có bằng chứng hợp lệ, không có lượt đang làm/chờ chấm hoặc phần rèn lại/ôn đến hạn chưa hoàn thành. Kỹ năng tiếp theo gợi ý từ giáo trình hiện tại và các tiên quyết đã có bằng chứng.
- Bài làm, kết quả và các tuyến cũ được giữ trong lịch sử. Các kỹ năng đã vững trong phạm vi tuyến mới được chuyển sang ôn, không luyện lại phần cơ bản.

## Dữ liệu và triển khai

Cần migration `20261025142829_LearningPathCalendarAndExams`: bổ sung ngày kế hoạch, điểm mục tiêu, cấu hình lịch, cờ đầu vào và metadata kỳ thi. Migration chuyển mục tiêu cũ sang nguồn chính thức và giữ dấu vết dữ liệu tương thích. Vẫn dùng hệ thống mission/activity/attempt hiện có. `LearningGoalScope` đọc được cả mảng kỹ năng cũ và object chứa phạm vi/tự xác nhận. API trả ngày dự kiến, ngân sách, kỳ thi, báo cáo đầu vào và giới hạn dự báo. Tuyến cũ vẫn đọc được; đường sinh legacy trả HTTP 410, callback chấm bài không tạo tuyến legacy mới.

Chất lượng phân tích phụ thuộc cấu trúc chủ đề/kỹ năng, quan hệ tiên quyết và ngân hàng câu hỏi do giáo viên biên soạn. Báo cáo chỉ nêu kết quả quan sát được, không tự suy đoán nguyên nhân sai cụ thể khi chưa có bằng chứng.
