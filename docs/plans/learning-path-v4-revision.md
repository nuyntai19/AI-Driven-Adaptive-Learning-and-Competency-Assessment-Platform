# Lộ trình học tập V4 — Bản yêu cầu cập nhật

Ngày cập nhật: 25/10/2026.

Đây là bản nghiệp vụ thay cho danh sách thay đổi ban đầu, đã được triển khai trong mã nguồn V4. Trạng thái kiểm chứng và các giới hạn vận hành được ghi tại [báo cáo triển khai](learning-path-v4-implementation.md). Cần áp dụng migration trước khi chạy phiên bản mới trên cơ sở dữ liệu hiện hữu.

## 1. Phạm vi và nguyên tắc

- V4 là bộ lập và điều chỉnh lộ trình chính thức. Chỉ triển khai quy tắc mới trên V4.
- Giữ ngân hàng câu hỏi, player làm bài, chấm bài và cơ chế follow-up hiện có. Không xây module đề thi song song.
- Mỗi kế hoạch phải trả lời được: học gì, vào ngày nào, khoảng bao lâu, vì sao được ưu tiên và phần nào chưa đủ dữ liệu.
- Không thiếu câu hỏi mà chặn toàn bộ lộ trình. Không tự xác nhận năng lực từ bài luyện có gợi ý hoặc lời giải.
- Phân biệt dữ liệu quan sát, tự đánh giá và ước lượng. Chưa có bằng chứng không đồng nghĩa với yếu hoặc chưa học.
- Giữ lịch sử bài làm và phiên bản kế hoạch; lập lại kế hoạch không xóa kết quả cũ.

## 2. Một nguồn mục tiêu thống nhất

- Một bộ mục tiêu thống nhất cho mỗi học sinh, môn học và kỳ thi/phạm vi học. Học sinh có thể có mục tiêu đồng thời ở các môn khác nhau.
- Nguồn mục tiêu chính thức chứa loại mục tiêu, phạm vi kỹ năng, TargetScore khi phù hợp, DeadlineDate/ngày thi, ngân sách phút/ngày, các ngày có thể học và PreferredPace.
- Digital Twin và bộ lập lộ trình cùng đọc nguồn này. Chuyển các trường đang trùng lặp về nguồn chính thức; không duy trì hai bản độc lập có thể sửa riêng.
- Số ngày còn lại được tính từ ngày đích theo múi giờ học sinh; không lưu một bộ đếm có thể lệch ngày.
- TargetScore không ánh xạ trực tiếp thành cùng một tỷ lệ mastery cho mọi kỹ năng. Quy tắc ánh xạ phải xét phạm vi/trọng số đề và ghi rõ phiên bản, giả định; khi chưa có mô hình phù hợp, chỉ đưa dự báo sơ bộ.
- Cảnh báo dưới đà mục tiêu dựa trên dự báo cùng phạm vi đề và mức độ tin cậy. Thiếu dữ liệu thì thông báo thiếu dữ liệu, không đưa kết luận chắc chắn.
- PreferredPace điều chỉnh lượng học mới, độ dài bộ luyện và nhịp nghỉ trong ngân sách đã chọn; không tự tăng trần phút/ngày. Nếu chưa triển khai tác dụng thì ẩn khỏi giao diện.

## 3. Lập lịch theo ngày và kiểm tra ngân sách

- Thêm PlannedDate cho buổi học/nhiệm vụ có lịch. Số thứ tự chặng và tuần lịch là hai thông tin riêng; tuần lịch cần kèm khoảng ngày để tránh nhầm.
- Tách ngày dự kiến có thể thay đổi, hạn hoàn thành và thời điểm thi cố định. Ngày học dùng ngày địa phương; kỳ thi có giờ dùng thời điểm và múi giờ.
- Tính sức chứa từ các ngày học thực sự còn lại, ngân sách từng ngày, ngày nghỉ và các mốc thi đã biết. Nếu chỉ có số ngày/tuần, đề xuất lịch cụ thể để học sinh xem và đổi.
- Khi có ngày đích, dành thời gian ôn/thi thử gần ngày đó rồi xếp các nhiệm vụ theo quan hệ tiên quyết trong phần lịch còn lại. Không xếp ngược làm đảo thứ tự nền tảng trước, kỹ năng phụ thuộc sau.
- Mặc định dành 15–20% thời gian cho ôn và thi thử; tỷ lệ được cấu hình, điều chỉnh theo mục tiêu và thời gian còn lại. Không lấp toàn bộ lịch bằng bài mới.
- Ngân sách ôn tập, thi thử và rèn lại đều tính vào tổng ngân sách; không trừ phần dự phòng hai lần.
- So nhu cầu thời gian ước lượng với ngân sách thực tế. Khi vượt, báo số phút/buổi còn thiếu, các giả định và lựa chọn giảm phạm vi hoặc tăng thời gian; cho xem kế hoạch thu hẹp.
- Dùng thông báo “Vượt ngân sách học theo ước tính hiện tại”, tránh khẳng định học sinh chắc chắn không thể đạt mục tiêu.
- Mục tiêu không có ngày đích vẫn có kế hoạch cuốn chiếu và ngày hoàn thành ước lượng; không tạo kỳ thi hoặc hạn giả.

## 4. Thứ tự ưu tiên và thay đổi khi gần thi

- Đảm bảo quan hệ tiên quyết trước khi xếp hạng các kỹ năng đủ điều kiện.
- Với ExamPrep, dùng (1 − mastery) × ExamImportance làm điểm ưu tiên ban đầu. Chuyển phần tính phù hợp từ legacy sang V4 và kiểm tra miền giá trị mastery trước khi áp dụng.
- Khi thiếu thời gian, xét thêm thời lượng ước tính và giá trị mở khóa các kỹ năng phụ thuộc. Không cắt nền tảng cần thiết chỉ vì trọng số trực tiếp thấp.
- ExamImportance phải gắn với phạm vi kỳ thi/môn học. Nếu chỉ có trọng số chung, ghi rõ đây là trọng số mặc định; thiếu trọng số thì dùng giá trị trung tính.
- Học sinh chưa có dữ liệu được đánh dấu Unknown và nhận probe ngắn; không tự coi mastery bằng 0 như một kết luận năng lực.
- Mục tiêu khác giữ chiến lược ưu tiên phù hợp hiện tại; không ép mọi lộ trình sang tối ưu điểm thi.
- Trên 45 ngày: mặc định cân bằng học mới, luyện và ôn. Trong 45 ngày: tăng kiểm tra ngắn. Trong 14 ngày: ưu tiên thi thử bấm giờ và lỗ hổng trọng số cao, giảm bài mới ít giá trị.
- Các mốc 45/14 ngày là cấu hình mặc định, điều chỉnh theo độ dài lộ trình và ngân hàng đề. Không bỏ nền tảng thiết yếu hoặc tự chèn bài thi dài vượt quỹ học.

## 5. Kỳ thi, mốc chặng và kết quả

- Thêm phân loại Assignment: bài tập thường, thi giữa kỳ, thi cuối kỳ, thi thử. Loại bài không thay thế phạm vi nội dung hoặc chính sách chấm.
- Giáo viên tiếp tục tạo bài trong module Bài tập; chọn phân loại, thời gian thi, giới hạn thời gian và phạm vi nội dung.
- Bài thi đã công bố cho lớp tự xuất hiện trong lộ trình của học sinh đúng lớp/phạm vi, dùng tham chiếu đến cùng Assignment. Không tạo bản sao đề/bài làm; cập nhật lịch hoặc hủy thi phải phản ánh lại trên lộ trình.
- Phân biệt kỳ thi lớp có ngày cố định với mốc kiểm tra chặng kích hoạt theo tiến độ. Không dùng tên giữa kỳ/cuối kỳ cho mọi chặng cá nhân nếu gây nhầm với kỳ thi lớp.
- Mốc kiểm tra chặng được mở sau khi học đủ nội dung cần thiết. Bài thi thử cá nhân được lên lịch theo mục tiêu và ngân sách.
- Kết quả đã hoàn tất chấm đưa vào follow-up để cập nhật năng lực, nhiệm vụ rèn lại và dự báo. Xử lý lại cùng kết quả không tạo nhiệm vụ trùng.
- Chỉ khóa kỹ năng tiếp theo thực sự phụ thuộc vào nền tảng chưa đạt; phần độc lập vẫn học được. Không mặc định khóa cả lộ trình vì trượt một phần.
- Kỳ thi lớp ghi nhận kết quả và điều chỉnh kế hoạch; không mặc định là cửa khóa chặng cá nhân. Chính sách đạt/thi lại cần tách theo loại bài.
- Khi chưa đạt, luyện phần thiếu rồi kiểm tra lại bằng bằng chứng phù hợp; giới hạn vòng lặp và đề nghị hỗ trợ giáo viên nếu không tiến bộ.
- Không dùng ngưỡng 80% chung cho mọi bài thi, mục tiêu và cỡ mẫu. Ngưỡng/chính sách phải được cấu hình và giải thích theo loại đánh giá.

## 6. Điều chỉnh khi bỏ buổi, quá hạn hoặc nợ ôn

- Lượt học thông thường quá hạn chưa làm có thể được đánh dấu Skipped với lý do bỏ lịch. Kỹ năng chưa học vẫn nằm trong nhu cầu còn lại, không được tính hoàn thành.
- Giữ trạng thái đang làm/đã nộp; không tự bỏ bài đang chấm, xóa bài làm hoặc thay đổi điểm.
- Lập lại phần chưa học từ hôm nay, giữ lịch sử phiên bản và luôn tôn trọng trần phút/ngày. Không dồn toàn bộ nợ vào một ngày.
- Ưu tiên ôn đến hạn trong một phần ngân sách có giới hạn; khi nợ nhiều, giảm bài mới. Không dừng mọi bài mới vô thời hạn chỉ vì hàng ôn chưa rỗng.
- Nếu không đủ thời gian, giảm nội dung ít ưu tiên, giữ nền tảng thiết yếu và nêu rõ phần bị hoãn/loại khỏi kế hoạch hiện tại.
- Báo sớm cho học sinh và giáo viên đúng phạm vi phụ trách: thiếu bao nhiêu thời gian, kỹ năng nào ảnh hưởng và các lựa chọn xử lý. Không gửi lại cùng cảnh báo khi tình trạng không thay đổi.
- Mốc thi cố định không tự chuyển ngày, tự hoàn thành hoặc Skipped khi quá hạn. Chuyển sang trạng thái quá hạn/vắng thi phù hợp và theo chính sách giáo viên; không lên lịch bù vào quá khứ.
- Nếu thời gian chuẩn bị không đủ, giữ giới hạn học và báo phần chưa được chuẩn bị; không “nén” bằng cách tăng tải ngầm.

## 7. Kiểm tra đầu vào có thể bỏ qua

- Kiểm tra đầu vào là bước mở đầu được đề xuất khi tạo lộ trình đầu tiên, không phải mục tiêu học thay thế. Việc tách khỏi GoalType cần thay đổi backend và chuyển dữ liệu cũ tương ứng.
- Học sinh có thể bỏ qua và nhận lộ trình từ dữ liệu hiện có, kết hợp probe trong quá trình học. Với ExamPrep, dùng trọng số đề thi sau quan hệ tiên quyết; với mục tiêu khác dùng chiến lược tương ứng.
- Mong muốn 5 câu hợp lệ/kỹ năng. Có 3–4 câu thì vẫn kiểm tra và ghi rõ mẫu ít/độ tin cậy thấp. Có 0–2 câu thì không kết luận kỹ năng, giữ trạng thái chưa đủ dữ liệu và bổ sung đánh giá sau.
- Phần thiếu câu không chặn các phần đủ điều kiện hoặc chặn tạo lộ trình toàn môn. Không tính phần chưa kiểm tra thành điểm 0.
- Chỉ dùng câu có thể tự chấm đầy đủ, đã có đáp án/cấu hình hợp lệ; loại Essay và câu cần chấm tay. Không chỉ lọc theo tên loại câu.
- Làm xong phải có kết quả tự động; không phát sinh hàng đợi giáo viên chấm đầu vào. Nếu chấm lỗi, hiển thị trạng thái lỗi có thể thử lại, không giả kết quả.
- Ba mức xử lý: Vững → rút gọn học cơ bản, duy trì ôn; Cần củng cố → ưu tiên hỗ trợ; Chưa kết luận → giữ nội dung phù hợp và hẹn probe sau vài buổi.
- Chính sách kết luận xét số câu hợp lệ, kết quả và nguồn bằng chứng; không chỉ đổi nhãn cho ngưỡng 4/5 cũ. Quy tắc phải có phiên bản và kiểm chứng bằng các trường hợp mẫu.
- Sai một câu có thể gợi ý kiểm tra 1–2 câu tiên quyết trong giới hạn thời gian. Đây là dấu hiệu cần xác minh, chưa đủ để kết luận hổng nền hoặc chưa học.
- Khi có bằng chứng bổ sung về nền tảng yếu, chèn hỗ trợ nền tảng trước phần phụ thuộc. Nếu chưa kết luận, giữ tuyến chính và hẹn kiểm tra thêm.
- Chưa triển khai CAT; chỉ xem xét sau khi ngân hàng câu hỏi và dữ liệu độ khó đủ chất lượng.

## 8. Ngân hàng câu hỏi và phương án dự phòng

- Ưu tiên phân bổ câu chưa lộ đáp án cho kiểm tra đầu vào và tự kiểm tra độc lập trước bài luyện; quy tắc cấp câu cần tránh phát cùng câu cho hai lượt đang mở.
- Câu đã lộ được tái sử dụng cho luyện/ôn sau khoảng thời gian cấu hình. Thời gian trôi qua không tự biến câu đã lộ thành bằng chứng độc lập mới.
- Khi thiếu câu kiểm tra độc lập, ghi rõ chưa đủ bằng chứng; không hạ điều kiện âm thầm để xác nhận vững.
- Khi một kỹ năng thiếu nội dung, cung cấp nhiệm vụ ôn nền/tổng quát cùng môn và phạm vi phù hợp nếu có nội dung hợp lệ. Kết quả chỉ cập nhật các kỹ năng thực sự được câu hỏi đánh giá.
- Nếu không có nội dung dự phòng hợp lệ, vẫn cho học các phần khả dụng và báo thiếu nội dung cụ thể; không dùng câu môn khác, bịa câu hoặc gán kết quả đại cương cho kỹ năng chưa được kiểm tra.

## 9. Chuyển đổi hệ thống

- Kiểm kê endpoint, dependency injection, tác vụ nền và client đang dùng GenerateLearningPathUseCase/schema 2.0 trước khi ngắt đường sinh legacy.
- Ngừng tạo lộ trình legacy mới sau khi V4 đáp ứng các luồng thay thế. Giữ khả năng đọc lịch sử và đường dẫn cũ cần thiết; không xóa dữ liệu cũ để hợp nhất.
- Chuyển mục tiêu trùng về nguồn chính thức với quy tắc xử lý xung đột rõ ràng, giữ dấu vết chuyển đổi và khả năng phục hồi.
- Thứ tự triển khai: gỡ bế tắc đầu vào và hợp nhất mục tiêu → lập lịch/ngân sách/điều chỉnh → đồng bộ kỳ thi và dự báo nâng cao.

## 10. Giao diện và phần đã sửa trong lượt này

- Trang lộ trình dùng cùng giới hạn chiều rộng và lề với khung học sinh; bỏ khung nội dung hẹp lồng trong khung lớn.
- Lưới báo cáo đầu vào, nhiệm vụ và bản đồ kỹ năng tự co giãn theo số thẻ. Một thẻ dùng hết hàng; nhiều thẻ chia cột theo không gian thực tế, tự xuống hàng trên màn hình nhỏ.
- Chỉ dùng bộ chọn môn ở thanh chung. Môn trên URL, phạm vi lớp và nội dung lộ trình phải nhất quán. Trang lộ trình yêu cầu một môn cụ thể; các trang tổng quan vẫn hỗ trợ Toàn bộ.
- Khi vào trang chưa có môn, chọn môn khả dụng đầu tiên và ghi vào URL trước khi tải lộ trình. Đổi môn/lớp đóng giao diện thao tác cũ, không giữ nhiệm vụ của phạm vi trước.
- Khi chưa có lộ trình, chỉ có một nút “Bắt đầu lộ trình”. Trong hộp thoại có lựa chọn “Kiểm tra đầu vào · Đề xuất” hoặc “Bỏ qua kiểm tra, học ngay”.
- Khi đã có lộ trình, dùng “Điều chỉnh mục tiêu”; kiểm tra thêm chỉ dành cho phần ngoài phạm vi đang học. Khi đang kiểm tra đầu vào, báo cáo cung cấp thao tác tiếp tục hoặc xem tuyến từ dữ liệu hiện có.
- Khối “Hôm nay học gì?” chỉ xuất hiện khi có lộ trình học; tránh lặp lời mời bắt đầu và thao tác kiểm tra đầu vào.
- API tách StartWithPlacement khỏi GoalType; đầu vào dùng 3–5 câu hợp lệ, thiếu câu được báo riêng. Mẫu nhỏ không tự xác nhận vững. Lịch, điểm mục tiêu, kỳ thi và giới hạn dự báo đã nối vào giao diện.

## 11. Điều kiện nghiệm thu

- Không có hai bộ chọn môn trên nội dung trang; chọn môn/lớp khác không mở lại bài làm của phạm vi cũ.
- Một kỹ năng trong báo cáo không chừa cột trống; không tràn ngang ở màn hình điện thoại; lề nội dung và thanh chung thẳng hàng trên màn hình rộng.
- Luồng chưa có lộ trình có một điểm vào, vẫn chọn được kiểm tra hoặc học ngay, và vẫn xem trước/xác nhận tuyến học theo API hiện hành.
- Sau khi triển khai backend: kiểm tra được trường hợp 0/2/3/4/5 câu, câu chấm tay và chấm lỗi; mọi trường hợp thiếu câu không chặn cả môn.
- Kế hoạch không vượt trần phút/ngày, không xếp nền tảng sau kỹ năng phụ thuộc, không tự đổi lịch thi lớp, không đếm trùng dự phòng hoặc tạo follow-up trùng.
- Bỏ một buổi không làm kỹ năng được coi là đã học; nợ ôn không làm toàn bộ học mới bị khóa vô thời hạn.
- Khi giảm phạm vi, học sinh biết phần nào bị cắt và ảnh hưởng tới mục tiêu; khi thiếu dữ liệu, kết quả thể hiện rõ giới hạn kết luận.
