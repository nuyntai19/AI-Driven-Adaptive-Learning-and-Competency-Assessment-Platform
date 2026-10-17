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
