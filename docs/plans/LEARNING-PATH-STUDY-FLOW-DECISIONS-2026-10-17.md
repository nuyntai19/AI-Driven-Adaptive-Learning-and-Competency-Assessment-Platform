# Lộ trình học: gợi ý, tạm dừng/hủy, thời gian, mức độ yếu, làm lại, đổi tuyến

Ngày: 17/10/2026 · Nhánh: shin/studytask

Đối chiếu: Khan Academy (hint từng bước, retake với câu khác, mastery lên/xuống theo bài mới), IXL (SmartScore, "Learn with an example", giải thích sau câu sai), Duolingo (mục tiêu theo phút, luyện lại lỗi sai, khuyến nghị ~15 phút/ngày), Coursera (giới hạn lượt làm, giữ lịch sử điểm).

## 1. Gợi ý

Lỗi cũ: server trả về lý do của nhiệm vụ (không liên quan câu hỏi), client hiển thị ở dòng thông báo và bị "Đã lưu bản nháp" ghi đè sau 1,2 giây.

Hiện tại: gợi ý mở từng bước, tối đa 3 mức, chỉ dựng từ dữ liệu đã biên soạn:

1. Định hướng: `ExpectedReasoning` hoặc mô tả kỹ năng.
2. Bước đầu tiên: khoảng 1/3 đầu lời giải.
3. Gần tới đáp án: toàn bộ lời giải trừ bước kết luận.

Đáp án đúng được che (`___`) ở mọi mức. Câu chưa có lời giải chỉ có gợi ý định hướng, kèm thông báo rõ ràng. Mở lại gợi ý đã xem không bị tính thêm. Tự kiểm tra vẫn chặn gợi ý ở phía server.

## 2. Tạm dừng / hủy

- Tạm dừng: dừng đồng hồ học thực tế, lưu nháp ngay, có "Tiếp tục" hoặc "Lưu và thoát". `timeSpentSeconds` không tính lúc tạm dừng.
- Hủy lượt (`POST missions/{id}/runs/{runId}/abandon`): lượt chuyển sang `Abandoned`. Câu đã nộp vẫn giữ làm bằng chứng, phần chưa nộp bị bỏ, nhiệm vụ bắt đầu lại được.
- Tự kiểm tra đã nộp ít nhất một câu thì không hủy được, để tránh "hủy khi làm kém". Vẫn có thể tạm dừng.

## 3. Thời gian hôm nay

- Thanh trượt 5 phút đến 5 giờ, bước 5 phút, đồng hồ hiển thị bên cạnh.
- Agenda trả về `TodayPlan`:
  - Xếp theo thứ tự ưu tiên: đang làm dở, ôn đến hạn, rồi theo tuyến.
  - Bộ luyện được co cho vừa thời gian (tối thiểu 2 câu).
  - Thời gian dư được cộng thêm câu cho kỹ năng yếu nhất trước, tối đa 20 câu/bộ và trong giới hạn ngân hàng.
- Không lặp câu để lấp thời gian; thời gian còn dư được báo trong `PacingNote`. Trên 50 phút có nhắc nghỉ.
- Tự kiểm tra luôn 5 câu để kết quả so sánh được.

## 4. Mức độ yếu quyết định khối lượng

`LearningPathPacing.Weakness` dựa trên câu trả lời gần nhất của từng câu khác nhau:

| Mức | Nội dung tuyến |
|---|---|
| Yếu (≥ 0,6) | Tìm hiểu 10 phút + 2 bộ luyện (tổng ~12–16 câu) |
| Cần củng cố | Tìm hiểu + 1 bộ (~8–11 câu) |
| Khá vững (< 0,3, có ≥ 3 câu) | Bỏ phần lý thuyết, 1 bộ ngắn (~6 câu) |

Bộ "Rèn lại" sau khi tự kiểm tra chưa đạt có 4 + 2 × số câu sai, trong khoảng 6–12 câu. Trong một bộ, câu từng sai được ra trước, sau đó đến câu chưa gặp, cuối cùng là câu đã đúng.

## 5. Làm lại

- Luyện / ôn / tìm hiểu: luôn có "Luyện thêm" hoặc "Học lại", mỗi lần tạo lượt mới.
- Tự kiểm tra chưa đạt: chỉ "Kiểm tra lại" sau khi xong một bộ rèn cùng kỹ năng (sửa trước, kiểm tra lại sau) và bằng ≥ 5 câu chưa gặp.
- Tự kiểm tra đã đạt: không cần làm lại; lịch ôn ngắt quãng sẽ kiểm tra khả năng nhớ.
- Lịch sử giữ mọi lượt, kể cả lượt đã hủy, gồm ngày, trạng thái, số câu đúng và đã làm. Có thể xem lại từng lượt. Năng lực tính theo bằng chứng mới nhất, như Khan Academy: tăng hoặc giảm theo bài mới.

## 6. Đổi tuyến

- Không xóa bài làm. Mọi câu trả lời cũ được dùng để định mức độ yếu và bỏ qua phần đã vững trong tuyến mới.
- Tiến độ theo nhiệm vụ của tuyến cũ không chuyển sang tuyến mới, vì tuyến mới có nhiệm vụ khác. Tuyến cũ hiển thị ở "Lịch sử tuyến học".
- Lượt đang làm dở chặn việc đổi tuyến; học sinh làm tiếp hoặc hủy lượt rồi đổi.

## Kiểm thử

- `tests/EduTwin.BLL.Tests/Recommendations/LearningPathStudyFlowTests.cs`: 9 test mới.
- Backend: toàn bộ 4358 test đạt.
- Frontend: 760 test đạt (Node 22).
- `tsc` và `vite build` đạt.

## Bản sửa 2 (17/10/2026 tối): vòng luyện và đánh giá đúng

### Bộ luyện cố định, thời gian quyết định số bộ

- Mỗi bộ luyện cố định 6 câu; riêng "Thử nhanh" 4 câu.
- Thanh trượt thêm số bộ trong ngày, không kéo dài một bộ. Tối đa 3 bộ cho mỗi kỹ năng mỗi ngày; phần yếu được thêm bộ trước.
- Bộ đã mở giữ nguyên số câu. Giao diện ghi rõ thời gian mới áp dụng từ bộ tiếp theo.
- Đã sửa lỗi bộ nhớ đệm khiến số câu không đổi khi mở bài.

### Vòng luyện theo kết quả (`LearningPathFollowUp`)

| Kết quả bộ (câu có gợi ý tính nửa) | Bước tiếp |
|---|---|
| ≥ 80% | Bộ khó hơn; đạt 2 bộ liên tiếp thì đề xuất tự kiểm tra |
| 50–80% | Một bộ cùng mức, ưu tiên câu vừa sai |
| < 50% | Bộ dễ hơn, nhắc xem lại phần Tìm hiểu |

- Tối đa 8 bộ mỗi kỹ năng. Vượt mức thì đề nghị hỏi giáo viên thay vì luyện tiếp.
- Tự kiểm tra vẫn làm được bất cứ lúc nào, nhưng chỉ được đưa vào kế hoạch hôm nay khi đã sẵn sàng.
- Kỹ năng chưa có bài làm: làm "Thử nhanh" 4 câu trước; đạt từ 80% thì bỏ qua phần lý thuyết. Không áp dụng cho mục tiêu Nền tảng và Nâng cao.
- Xen kẽ: sau 3 bộ của cùng một kỹ năng trong ngày, ưu tiên kỹ năng khác.

### Ôn tập phát hiện việc quên

- Ôn từ 60%: hẹn ôn tiếp sau 7, rồi 14, rồi 28 ngày (tối đa 30).
- Ôn dưới 60%: kỹ năng quay lại luyện, thêm bộ "Rèn lại", kể cả khi kỹ năng đã từng đạt. Đạt lại 2 bộ liên tiếp thì hẹn ôn.

### Tự kiểm tra công bằng hơn

- Chọn 5 câu chưa gặp, sắp từ dễ đến khó. Nếu ngân hàng có, ít nhất 2 câu từ mức 3/5 trở lên.
- Không có câu mức vừa hoặc khó thì kết quả ghi rõ chỉ xác nhận mức cơ bản.
- Phần lớn là trắc nghiệm thì phải đúng hết.
- Câu do AI chấm còn chờ giáo viên duyệt: kết quả ghi "Tạm đạt · chờ giáo viên xác nhận", lộ trình vẫn đi tiếp. AI có được tạm xác nhận năng lực hay không vẫn cần nhóm thống nhất.

### Khác

- Phân tích điểm yếu tính câu đúng nhờ gợi ý là nửa câu đúng.
- Phần Tìm hiểu có tối đa 2 ví dụ mẫu, mở từng bước.
- Gợi ý: câu một từ hoặc trắc nghiệm chỉ có 2 mức; che cả vế phải của đáp án dạng "x = 2".
- Bài ôn được làm song song với một bộ luyện đang dở.
- Trang lộ trình:
  - Tiến độ mục tiêu: kỹ năng đã vững, thời gian còn lại, ngày dự kiến xong, chuỗi ngày học.
  - Thông báo cập nhật cho tuyến tạo trước khi có vòng luyện.
  - Lý do rút gọn, có nút "Vì sao?".
  - Sửa lỗi lặp chữ.
- Giữ nguyên phần sửa truy vấn lịch sử tuyến của commit 6761e1c.

### Kiểm thử

- Backend: 4364 test đạt, gồm 8 test mới cho vòng luyện, ôn tập, tự kiểm tra và gợi ý.
- Frontend: 762 test đạt (Node 22).
- `tsc` và `vite build` đạt.
