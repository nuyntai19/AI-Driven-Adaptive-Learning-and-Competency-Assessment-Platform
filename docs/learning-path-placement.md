# Kiểm tra đầu vào và cập nhật lộ trình

Trang lộ trình hỗ trợ hai cách bắt đầu: kiểm tra đầu vào theo phạm vi được chọn hoặc học từ nền tảng. Checklist gom các kỹ năng lá theo chủ đề trong giáo trình; chọn một chủ đề chọn các kỹ năng con. Quan hệ tiên quyết lấy từ Knowledge Graph, bao gồm quan hệ giữa các chủ đề cha được mở rộng xuống kỹ năng lá. Với nền tảng chưa có bằng chứng trên web, học sinh chọn bổ sung vào tuyến hoặc tự xác nhận đã học ở trường và bỏ qua. Không tự mở rộng tuyến qua các nền tảng đã chọn bỏ qua.

## Kiểm tra đầu vào

- Mục tiêu `Placement` tạo một tuyến kiểm tra riêng: mỗi kỹ năng chưa được xác nhận có 5 câu độc lập chưa làm/chưa lộ lời giải. Câu hỏi có mức vừa/khó được đưa vào nếu ngân hàng có sẵn. Có thể lưu tiến độ theo từng phần.
- Kiểm tra đầu vào có thể đánh giá một kỹ năng khi nền tảng chưa được xác nhận. Với tuyến học thông thường, mỗi nền tảng cần có bằng chứng hợp lệ hoặc được học sinh tự xác nhận bỏ qua trong chính tuyến đó. Lựa chọn này không đổi trạng thái năng lực toàn môn và không mở khóa các nền tảng khác chưa được tự xác nhận.
- Phần đã có bằng chứng độc lập hợp lệ được dùng lại, không phát sinh điểm kiểm tra mới. Không đủ 5 câu mới thì ghi rõ thiếu câu, không tính là 0 điểm.
- Không thêm vòng rèn lại hoặc lịch ôn trong giai đoạn thu thập đầu vào. Sau khi chấm từng phần, báo cáo tổng hợp điểm theo kỹ năng, chủ đề, toàn bài và ước lượng số bộ luyện. Điểm lấy từ điểm chấm thực tế, gồm điểm thành phần và điểm giáo viên sửa; không dùng điểm của các môn hoặc lượt luyện khác.
- Báo cáo đang làm chỉ tính phần đã hoàn thành. Học sinh có thể xem tuyến tạm với phần chưa kiểm tra được đánh dấu chưa đủ bằng chứng; không chuyển tuyến khi còn lượt đang làm hoặc chờ chấm.

## Tuyến luyện và chặng tiếp theo

- “Lập lộ trình từ kết quả” giữ phạm vi do học sinh chọn và lựa chọn bỏ qua nền tảng; bộ lập tuyến bổ sung các tiên quyết chưa bỏ qua và dùng bài làm đã có để điều chỉnh độ khó, số bộ luyện và câu từng sai. Số bộ là ước lượng, cập nhật sau từng bộ. Bản xem trước và trang lộ trình ghi rõ nền tảng tự xác nhận; cập nhật và tùy chỉnh không tự thêm lại các phần này.
- “Cập nhật lộ trình từ bài làm” điền lại phạm vi, loại mục tiêu, thời gian mỗi ngày, số ngày mỗi tuần và hạn mục tiêu. Luôn xem trước rồi mới xác nhận thay tuyến.
- Năng lực dựa trên 5 câu độc lập có bằng chứng hợp lệ gần nhất trong 30 ngày, mỗi câu chỉ tính một lần. Câu đã có hướng dẫn/lộ lời giải và bằng chứng cần giáo viên duyệt không tự xác nhận năng lực. Ngưỡng là 80%; nếu quá nửa câu là trắc nghiệm thì cần đúng toàn bộ, giống bộ đánh giá nhiệm vụ.
- Chỉ đề nghị sang chặng tiếp theo khi tất cả kỹ năng trong phạm vi tuyến có bằng chứng hợp lệ, không có lượt đang làm/chờ chấm hoặc phần rèn lại/ôn đến hạn chưa hoàn thành. Kỹ năng tiếp theo gợi ý từ giáo trình hiện tại và các tiên quyết đã có bằng chứng.
- Bài làm, kết quả và các tuyến cũ được giữ trong lịch sử. Các kỹ năng đã vững trong phạm vi tuyến mới được chuyển sang ôn, không luyện lại phần cơ bản.

## Dữ liệu và triển khai

Không cần migration: dùng `LearningGoal.GoalType`, các trường JSON kế hoạch và hệ thống mission/activity/attempt hiện có. API mục tiêu, đề xuất và agenda có `selfReportedPrerequisiteSkillIds`. JSON `LearningGoal.TargetSkillIds` vẫn là mảng khi không bỏ qua nền tảng; với tự xác nhận, lưu object gồm `targetSkillIds` và `selfReportedPrerequisiteSkillIds`. `LearningGoalScope` đọc được cả hai định dạng, mọi API vẫn trả `targetSkillIds` dạng mảng. API đề xuất có `isPlacement`, `goalType`, `requestedSkillIds`; agenda có phạm vi/lịch mục tiêu, `placementReport`, `canAdvance`, `nextSkillIds`. Capability map có `topicId`, `topicName` là chủ đề cha gần nhất. Tuyến cũ vẫn đọc được; phạm vi/lịch lấy lại từ mục tiêu đã lưu.

Chất lượng phân tích phụ thuộc cấu trúc chủ đề/kỹ năng, quan hệ tiên quyết và ngân hàng câu hỏi do giáo viên biên soạn. Báo cáo chỉ nêu kết quả quan sát được, không tự suy đoán nguyên nhân sai cụ thể khi chưa có bằng chứng.
