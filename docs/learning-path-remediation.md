# Nhánh ôn phần yếu trong lộ trình

- Giáo viên gắn tối đa 5 nhãn nội dung nhỏ tại `gradingCriteria.learningFocuses` trong màn hình soạn câu. Nhãn chỉ là tên kiến thức, không chứa đáp án. Lưu trong JSON hiện có, không cần migration.
- Với câu tiếng Anh cũ chưa có nhãn, nhận diện tên thì chuẩn xuất hiện rõ trong ExpectedReasoning do giáo viên viết. Không suy đoán từ phương án sai hay lời giải. Thiếu metadata thì tiếp tục luyện ở mức chủ đề.
- Sau bộ luyện, kiểm tra hoặc ôn chưa đạt, chọn tối đa hai phần có lỗi và tỷ lệ đúng dưới 80%. Đây là tín hiệu cần củng cố, không khẳng định một lỗi đơn lẻ chứng minh học sinh yếu.
- Chèn nhánh ngay sau bộ nguồn: xem lại ví dụ đã biên soạn, rồi luyện theo phần yếu. Lưu bộ nguồn, gốc nhánh, số vòng và nội dung tập trung.
- Khoảng 80% câu thuộc phần yếu, chia đều các phần, kèm một câu đối chiếu nếu có. Ưu tiên câu khác chưa làm; xáo trộn trong cùng mức ưu tiên và độ khó. Giữ phạm vi lớp/giáo trình, thời gian chờ và phần câu dành cho tự kiểm tra. Nếu thiếu câu phần yếu thì rút ngắn bộ, không âm thầm thay bằng câu khác.
- Quay lại luyện tổng hợp khi hai bộ liên tiếp đạt từ 80%, từng phần đạt từ 80% trong lượt hiện tại và trên 5 câu khác nhau gần nhất của nhánh (tối thiểu 3 câu). Câu đúng có gợi ý tính nửa điểm; làm lại một câu không tăng số câu khác nhau.
- Hoàn thành nhánh không tự xác nhận vững cả chủ đề. Sau nhánh là luyện tổng hợp và tự kiểm tra.
- Tối đa 6 vòng nhánh, vẫn chịu trần 8 bộ/chủ đề và quỹ học hiện có. Nếu chưa ổn định hoặc ngân hàng thiếu 3 câu khác nhau mỗi phần, dừng thêm bộ tự động và đề nghị giáo viên hỗ trợ/bổ sung câu. Chạm trần không tự đánh dấu sẵn sàng.
- Không sửa bài làm đã có hay tự thêm nhánh khi đọc trang. Nhánh được tạo trong giao dịch đánh giá kết quả, với quyết định và bằng chứng đi kèm.

## Hiển thị và bộ ôn ngắn

Bộ luyện chính được đánh số riêng, không đếm các vòng nhánh. Nhánh ghi rõ Ôn kiến thức và Rèn lại bằng bài tập, có nhãn bộ nguồn/vòng trước. Cách hiển thị mới áp dụng cả các nhiệm vụ cũ qua dữ liệu trả về, không sửa bài làm đã lưu.

Bộ ngắn vẫn giữ ít nhất 80% câu thuộc phần yếu: 1–3 câu phần yếu thì chỉ luyện các câu đó; có 4 câu phần yếu mới thêm tối đa 1 câu đối chiếu. Không dùng câu dễ ở nội dung khác để bù đầy bộ.


## Kết thúc lượt ôn và mở tự kiểm tra

Hoàn thành lượt ôn không đồng nghĩa nhánh đã đạt. Trang lộ trình hiển thị trạng thái còn cần luyện, thiếu câu, cần giáo viên hỗ trợ hoặc đủ điều kiện quay lại luyện tổng hợp từ quyết định đánh giá đã lưu. Vòng sau được sinh khi chưa đạt và đủ câu, trong giới hạn số vòng; thiếu ít nhất 3 câu khác nhau thì thông báo chờ bổ sung thay vì lặp một câu vô hạn.

Bài tự kiểm tra cuối chặng của chủ đề có nhánh ôn chỉ mở sau khi nhánh và luyện tổng hợp đã đạt điều kiện ReadyForCheck. Kiểm tra điều kiện cả ở dữ liệu giao diện và API bắt đầu bài. Vẫn cho xem kết quả cũ hoặc tiếp tục bài đã mở; giữ chính sách riêng của kiểm tra đầu vào, chẩn đoán, nền tảng ngoài phạm vi và thi bấm giờ.
