# EDUTWIN — BÁO CÁO VÀ ĐẶC TẢ LỘ TRÌNH HỌC TẬP CÁ NHÂN V05

> **Phiên bản tài liệu:** V05 / V5.0 — ngày 16/10/2026.
>
> **Phạm vi:** Tổng hợp thiết kế đã điều chỉnh, hành vi đã triển khai và kết quả kiểm chứng sau quá trình rà soát bản Antigravity và các phản hồi sử dụng thực tế.
>
> **Trạng thái:** Luồng cốt lõi đã triển khai và chạy trên local. Chưa nghiệm thu toàn bộ tầm nhìn trong V04 và chưa có pilot chứng minh hiệu quả học tập.
>
> **Quan hệ với V04:** Kế thừa định hướng học theo năng lực; cập nhật trải nghiệm đề xuất, hoạt động học, phản hồi và tính liên tục. V05 là phiên bản tài liệu, không phải tên một engine hoặc migration mới. Code/API vẫn có thể dùng tên V4.

## 1. Mục tiêu của hệ thống

Lộ trình giúp học sinh biết nên cải thiện điều gì, học phần nào tiếp theo và vì sao hệ thống đề xuất như vậy. Tuyến học dựa trên mục tiêu, bài làm, bằng chứng năng lực và nội dung hiện có trong phạm vi được học.

Một tuyến có mục tiêu, các chặng theo kỹ năng, nhiệm vụ hôm nay, bài làm được lưu và quyết định điều chỉnh có lý do. Việc hoàn thành hoạt động được theo dõi riêng với việc chứng minh đã nắm vững kỹ năng.

Học sinh được xem một đề xuất đã điền đầy đủ, điều chỉnh rồi xác nhận. Hệ thống không yêu cầu học sinh tự thiết kế toàn bộ tuyến trước khi nhận được hỗ trợ.

## 2. Những thay đổi chính so với bản trước

| Nội dung | Điều chỉnh trong V05 |
|---|---|
| Điểm bắt đầu | Đọc bài làm và bằng chứng thật theo môn, lớp/giáo trình, học sinh và trung tâm |
| Khung đề xuất | Hiển thị số liệu gần đây, phần cần xem lại, bài liên quan và lý do đề xuất |
| Bấm xem đề xuất | Mở bản xem trước đã điền mục tiêu, kỹ năng, lịch và các chặng; cho chỉnh trước khi chọn |
| Nội dung tuyến | Lấy từ kỹ năng thực trong mục tiêu và nền tảng liên quan; bỏ tuyến gán cứng lặp một chủ đề |
| Loại buổi học | Tách tìm hiểu, luyện có hướng dẫn và tự kiểm tra; dùng trình thực hiện nhiệm vụ riêng |
| Bộ luyện | Danh sách câu khác nhau, hướng tới 5–8 câu khi ngân hàng/quỹ thời gian đáp ứng |
| Phản hồi | Có câu trả lời của học sinh, đáp án và lời giải ngân hàng; AI bổ sung nhận xét tiếng Việt riêng |
| Tìm hiểu | Lưu cách hiểu do học sinh diễn đạt, cho AI nhận xét và chỉ chỗ cần sửa |
| Tự kiểm tra | Dùng bộ câu mới, giữ lời giải đến khi nộp cả bộ, đánh giá qua pipeline bằng chứng |
| Học nâng cao | Đề xuất theo kỹ năng có đủ căn cứ; phát câu khó thật, báo thiếu khi chưa có |
| Xem lại | Một nút Xem lại; đọc bài và kết quả lưu sẵn, không chờ gọi đánh giá lại |
| Buổi đang dở | Có nút Tiếp tục; thông báo xung đột nêu đúng tên buổi và cho mở tiếp |
| Hộp thoại | Nền ngoài trong suốt; tiêu đề và nút đóng luôn nằm ngoài vùng cuộn |
| Dữ liệu dùng thử | Bổ sung 120 câu có nhãn; không tạo điểm hay bằng chứng giả |

## 3. Dữ liệu làm căn cứ cho đề xuất

### 3.1. Nguồn bài làm

Khung đề xuất tổng hợp lượt trả lời trong 30 ngày gần nhất thuộc phạm vi học sinh đang xem. Bài từ nhiệm vụ giáo viên giao và bài tự luyện có thể được dùng khi được lưu qua pipeline bài làm hợp lệ. Nguồn bài được phân biệt; số lượt trả lời không được gọi thành số bài kiểm tra đã hoàn thành.

| Chỉ số | Ý nghĩa |
|---|---|
| Lượt trả lời | Số lượt trả lời được ghi nhận trong phạm vi và khoảng thời gian thống kê |
| Bài tập đã tham gia | Số bài tập gắn với các lượt làm; tự luyện có thể không gắn bài tập giáo viên |
| Câu khác nhau đã chấm | Số câu có kết quả chấm, loại ảnh hưởng của việc làm lại cùng một câu |
| Điểm trên các câu gần nhất | Kết quả tổng hợp từ câu đã chấm; không thay cho độ thành thạo toàn môn |

Một câu làm nhiều lần dùng kết quả gần nhất của câu đó để thống kê đúng/sai phục vụ đề xuất. Lỗi đã sửa không tiếp tục bị cộng như một điểm yếu mới ở mỗi lượt làm.

### 3.2. Điểm số và năng lực

Điểm và câu sai giúp xác định phần nên luyện. Trạng thái năng lực còn phụ thuộc tính độc lập của bài làm, độ tin cậy của bằng chứng và trạng thái giáo viên duyệt.

- Chưa có dữ liệu: ghi chưa đủ bằng chứng; không gán học sinh yếu hoặc tự tạo phần trăm năng lực.
- Đúng 3/3 câu: ghi nhận kết quả quan sát được nhưng chưa suy ra giỏi cả kỹ năng hoặc cả môn.
- Có câu sai: đề xuất củng cố kỹ năng liên quan; không tự suy ra nguyên nhân sai chi tiết khi chưa đủ căn cứ.
- Nhận định misconception cụ thể chỉ dùng khi có phân tích phù hợp đã được xác nhận; không thay bằng một nhãn lỗi tùy ý.
- Phản hồi AI và tự nhận xét của học sinh không tự cấp trạng thái đã xác nhận năng lực.

### 3.3. Vai trò của các nguồn

| Nguồn | Cách sử dụng |
|---|---|
| Bài giáo viên giao | Bổ sung quan sát kết quả và bằng chứng hợp lệ theo pipeline hiện hữu |
| Luyện có hướng dẫn | Chọn phần cần củng cố, xem tiến bộ khi luyện; không tự coi là kiểm tra độc lập |
| Tự kiểm tra | Xác nhận kỹ năng khi đáp ứng đủ điều kiện bằng chứng |
| Digital Twin | Đọc hồ sơ đã được pipeline cập nhật; không ghi đè bằng số giả từ bộ lập tuyến |
| Knowledge Graph | Xác định kỹ năng, nội dung liên quan và điều kiện nền tảng |
| Lịch học và mục tiêu | Quyết định phạm vi, thứ tự ưu tiên và khối lượng đề xuất |

## 4. Trải nghiệm đề xuất lộ trình

### 4.1. Khung trên trang chính

Khung “Lộ trình đề xuất cho bạn” gồm thống kê 30 ngày, nhận xét theo kỹ năng, bài làm liên quan và nút “Xem tuyến đề xuất”. Học sinh có thể hiểu căn cứ trước khi quyết định mở tuyến.

### 4.2. Bản xem trước được điền sẵn

Khi chọn xem đề xuất, hệ thống tạo bản nháp và trình bày ngay:

1. **Bài làm cho thấy:** số câu sai/đúng theo kỹ năng và mức độ đầy đủ của dữ liệu.
2. **Đề xuất cải thiện:** củng cố, xác định điểm xuất phát hay luyện nâng cao, kèm lý do.
3. **Phần nên học:** kỹ năng trọng tâm và nền tảng liên quan.
4. **Kế hoạch cụ thể:** tên mục tiêu, loại mục tiêu, lịch học, mốc dự kiến, các chặng, nhiệm vụ, số câu và thời lượng ước lượng.

Lịch gợi ý hiện dùng 15 phút/ngày, 5 ngày/tuần và mốc dự kiến hai tuần khi phù hợp với luồng tạo đề xuất. Đây là giá trị có thể chỉnh, không phải cam kết hoàn thành. Ước lượng và cảnh báo thiếu nội dung được trình bày trong bản xem trước.

Học sinh có thể chọn “Điều chỉnh mục tiêu” để sửa phạm vi, loại mục tiêu, lịch và thời hạn. Luồng “Thiết lập mục tiêu” thủ công vẫn được giữ.

Mở xem trước không thay tuyến đang học. Chỉ khi xác nhận chọn tuyến mới, hệ thống mới kích hoạt theo quy tắc chuyển tuyến, giữ lịch sử và xử lý các điều kiện đang chờ.

### 4.3. Quy tắc chọn hướng học

| Tình huống | Hướng đề xuất |
|---|---|
| Có lỗi trong các câu gần đây | Ưu tiên củng cố kỹ năng có lỗi, sau đó kiểm tra bằng câu mới |
| Chưa có hoặc chưa đủ bằng chứng | Mục tiêu xác định điểm xuất phát; không mặc định bắt học lại toàn bộ |
| Kỹ năng đã được xác nhận, có ít nhất 5 câu gần đây và tỷ lệ đúng từ 80% | Có thể tự đề xuất luyện nâng cao cho kỹ năng đó |
| Học sinh tự chọn nâng cao | Cho thiết lập mục tiêu phù hợp, nhưng không tự xác nhận năng lực hoặc bỏ điều kiện kiểm tra |
| Chưa có câu khó phù hợp | Nêu rõ thiếu nội dung, không dùng câu dễ thay thế rồi gọi là nâng cao |

Ngưỡng nâng cao là quy tắc sản phẩm hiện tại, chưa được hiệu chuẩn bằng pilot học sinh thật.

## 5. Cấu trúc tuyến và nhiệm vụ hôm nay

Các chặng gắn với kỹ năng thực trong phạm vi mục tiêu, có thể bổ sung kỹ năng nền tảng theo graph. Không chia nhiều tuần cùng học một chủ đề chỉ để làm tuyến có vẻ dài.

Nếu dữ liệu mới có các node rộng như “Thì”, hệ thống phải nói đúng độ chi tiết đó. Muốn cá nhân hóa đến từng thì hoặc từng lỗi nhỏ cần bổ sung graph và nội dung tương ứng; không chỉ đổi nhãn giao diện.

Trang chính có nhiệm vụ tiếp theo và lựa chọn thời gian 5/15/30 phút. Nhiệm vụ phải phù hợp quỹ thời gian và điều kiện nội dung; không thay nhãn phút của một hoạt động dài để giả phù hợp. Buổi đang dở được ưu tiên tiếp tục.

Nhiệm vụ có mã, lượt thực hiện và hoạt động được lưu thật. Các nút học mở đúng loại hoạt động trong tuyến, không tự chuyển mọi buổi sang trang luyện thích ứng chung.

## 6. Ba trải nghiệm học cốt lõi

### 6.1. Tìm hiểu và ví dụ

- Đọc nội dung đã xuất bản phù hợp kỹ năng; khi chưa có gói bài học, dùng mô tả chủ đề và ví dụ có lời giải từ ngân hàng với giới hạn rõ ràng.
- Học sinh diễn đạt cách hiểu bằng lời của mình; bản nháp và nội dung đã nộp được lưu theo tài khoản.
- Trợ giảng AI đọc cách hiểu, nêu phần phù hợp, điểm thiếu/sai, cách hiểu đúng và ví dụ bổ sung.
- Phân loại nhận xét: hiểu phù hợp, hiểu đúng một phần, có điểm cần sửa hoặc cần diễn giải thêm.
- Hoàn thành hoạt động đọc không tự tạo Attempt, điểm câu hỏi, bằng chứng độc lập hoặc cập nhật Twin.

### 6.2. Luyện có hướng dẫn

Bộ luyện hướng tới 5–8 câu khác nhau khi có đủ câu và lịch học cho phép. Câu được sắp để khởi động, gỡ vướng/áp dụng rồi luyện phản xạ; ưu tiên phần từng sai và tăng độ khó theo dữ liệu khả dụng.

Nếu ngân hàng chỉ đủ 1–4 câu, giao diện nêu rõ số câu và hạn chế. Không lặp cùng một câu trong một bộ để đủ số lượng. Bộ nâng cao chỉ phát câu thật có độ khó từ 4/5 trở lên theo cấu hình hiện tại.

Sau khi nộp mỗi câu, học sinh được xem:

1. Câu trả lời mình đã nộp.
2. Kết quả ban đầu nếu hệ thống chấm được, hoặc trạng thái chờ chấm.
3. Đáp án tham khảo và lời giải gốc của ngân hàng.
4. Hướng tiếp cận dạng bài và bước tiếp theo.
5. Nhận xét tiếng Việt, lời giải riêng và điểm cần sửa từ AI khi có kết quả.

Phản hồi từ ngân hàng và chấm ban đầu không chờ AI. Bài cần phân tích sâu hoặc giáo viên xử lý vẫn giữ nhãn chờ phù hợp, không được chấm giả để tạo cảm giác nhanh.

### 6.3. Tự kiểm tra

- Phát bộ 5 câu khác nhau, đúng kỹ năng và đủ điều kiện câu mới.
- Loại câu đã làm hoặc đã lộ lời giải khỏi bộ kiểm tra độc lập mới.
- Kiểm tra điều kiện nền tảng ở phía server.
- Chặn trợ giúp và lời giải trong lúc chưa nộp hết bộ; áp dụng cả với gọi API trực tiếp.
- Sau khi nộp cả bộ mới mở lời giải/nhận xét, nhưng kết luận xác nhận vẫn chờ đủ kết quả và bằng chứng hợp lệ.
- Thiếu câu phải báo số còn thiếu và nhu cầu bổ sung; không hạ chuẩn hoặc lấy lại câu đã lộ đáp án như câu mới.

Chính sách hiện tại yêu cầu ít nhất 5 câu độc lập qua kiểm duyệt bằng chứng, tỷ lệ đúng tối thiểu 80% và không còn điều kiện chờ chấm/giáo viên ngăn xác nhận. Đây là xác nhận cho kỹ năng được đo, không mặc nhiên xác nhận toàn môn.

## 7. Phản hồi AI và lời giải học thuật

### 7.1. Phân biệt nguồn

| Khu vực | Nguồn và ý nghĩa |
|---|---|
| Em đã trả lời | Nội dung học sinh thực sự nộp |
| Đáp án tham khảo | Đáp án hiện có của câu hỏi |
| Lời giải ngân hàng · nguyên văn | Trường lời giải được lưu trong ngân hàng; có thể là tiếng Anh nếu nội dung gốc là tiếng Anh |
| Trợ giảng AI · tiếng Việt | Phản hồi được tạo riêng từ bài làm và ngữ cảnh học tập |

Lời giải tiếng Anh xuất hiện ở bản trước là nội dung ngân hàng, không phải mặc định chọn tiếng Anh cho trợ giảng. V05 giữ nhãn nguồn và bổ sung khu vực AI tiếng Việt để tránh nhầm lẫn.

### 7.2. Nguyên tắc vận hành

AI nhận xét cách hiểu, giải thích cách làm và gợi ý sửa lỗi. Phản hồi được lưu cùng hoạt động để xem lại, dùng lại kết quả đã tạo; không tự gọi hàng loạt cho mọi câu đang thu gọn trong lịch sử.

Khi provider lỗi, quá thời gian hoặc không trả kết quả hợp lệ, giao diện báo chưa có nhận xét và cho thử lại. Bài làm vẫn được giữ. Không tạo phản hồi AI giả hoặc dùng lỗi gọi AI để cấp đạt.

Phản hồi trợ giảng không thay điểm chuẩn, quyết định giáo viên hoặc Evidence Gate. Việc chọn tuyến chủ yếu do bộ luật và dữ liệu thực điều phối; không mô tả toàn bộ tuyến như một quyết định tùy ý do mô hình ngôn ngữ sinh ra.

## 8. Kết quả học và điều chỉnh tuyến

| Kết quả | Ý nghĩa và xử lý |
|---|---|
| Hoàn thành hoạt động học | Đã học/luyện xong; chưa tự chứng minh nắm vững |
| Đang chờ chấm | Giữ bài, chờ kết quả hợp lệ |
| Chờ giáo viên | Không xác nhận năng lực trước quyết định phù hợp |
| Chưa đủ bằng chứng | Nêu điều kiện thiếu; không đổi thành đạt |
| Cần củng cố | Đề xuất rèn lại kỹ năng liên quan |
| Đạt kiểm tra độc lập | Ghi nhận kỹ năng theo bằng chứng và policy |

Evaluator sử dụng bài làm đã phát và pipeline bằng chứng, ghi quyết định để tránh tạo lặp khi đánh giá lại. Worker cũ không được tự ghi đè tuyến đã xác nhận chỉ vì có bài làm mới.

Sau kết quả cần hỗ trợ, hệ thống có thể thêm buổi rèn lại. Sau khi đạt, lịch ôn ban đầu có mốc 2 ngày; hoàn thành lượt ôn hẹn tiếp sau 7 ngày. Các mốc này là cấu hình ban đầu, chưa phải mô hình ghi nhớ đã hiệu chuẩn. Hoạt động ôn không tự trở thành một kiểm tra độc lập mới.

## 9. Lưu tiến độ, xem lại và xử lý buổi đang dở

### 9.1. Một hành động chính trên mỗi thẻ

| Tình trạng | Nút chính |
|---|---|
| Có thể mở, chưa bắt đầu | Bắt đầu |
| Còn lượt đang thực hiện/chờ xử lý | Tiếp tục |
| Đã hoàn thành hoặc có lượt lưu chỉ để xem lại | Xem lại |
| Chưa đủ điều kiện và chưa có lượt xem lại | Chưa đủ điều kiện |

Đã bỏ nút phụ “Xem lượt đã làm”. Xem lại đọc câu trả lời và kết quả đã lưu qua GET; không tạo lượt mới, không gọi đánh giá lại và không chờ AI mới hiển thị bài. Có thể chọn số câu hoặc mở phần nội dung tương ứng để xem chi tiết.

Đã sửa trạng thái chờ vô hạn khi một truy vấn đánh giá không chạy nhưng giao diện vẫn báo “Đang kiểm tra kết quả đã lưu”. Thông báo tổng kết chỉ xuất hiện khi có yêu cầu tổng kết thực sự đang chạy trong lượt đang học.

### 9.2. Buổi đang dở trong cùng môn

Khi bắt đầu buổi mới bị chặn bởi buổi chưa hoàn tất, thông báo nêu đúng tên buổi đó và có nút “Tiếp tục: [tên buổi]”. Nút mở lại đúng lượt cũ và giữ bản nháp; học sinh không phải tự tìm nhiệm vụ đang chặn.

API trả HTTP 409 với mã `LEARNING_MISSION_IN_PROGRESS` và thông tin buổi đang dở thuộc chính học sinh. Không tự bỏ buổi hoặc xóa tiến độ để né xung đột.

### 9.3. Tính liên tục

Bản nháp được đồng bộ về server; có revision để phát hiện xung đột giữa hai thiết bị. Start lặp hoặc start đồng thời phải trả về lượt hợp lệ hiện có. Submit thử lại cùng mã nộp không nhân đôi bài làm; cùng mã nhưng nội dung khác bị từ chối.

## 10. Các chỉnh sửa giao diện và lỗi đã xử lý

- Hộp đề xuất và buổi học dùng nền ngoài trong suốt; tiêu đề và nút đóng cố định ngoài vùng cuộn.
- Sửa lỗi 400 khi URL thiếu lớp làm form gửi mã lớp rỗng; dùng lớp được ngữ cảnh học tập xác nhận và bỏ trường optional rỗng.
- Hiển thị thông báo nghiệp vụ thay cho chuỗi chung “Request failed with status code 400”.
- Sau khi chấp nhận đề xuất, chờ làm mới dữ liệu tuyến để tránh mở nhiệm vụ từ cache của tuyến đã thay thế.
- Nút chọn tuyến nằm ở footer ngoài vùng cuộn. Nếu chưa thể chuyển vì có buổi đang làm/chờ xử lý, lỗi hiện ngay cạnh nút, nêu tên và trạng thái từng buổi, cho mở buổi hiện tại. Khi chuyển thành công có thông báo xác nhận trên trang; không để học sinh phải tự đoán thao tác đã được lưu hay chưa.
- Giữ xem lại cách hiểu đã nộp trong phần tìm hiểu, đáp án và phản hồi ở phần luyện.
- Sửa khóa tự kiểm tra tồn tại từ snapshot cũ khi ngân hàng đã được bổ sung: kiểm tra lại số câu mới thực tế, vẫn giữ nền tảng và policy.
- Giữ lịch sử tuyến cũ, không tạo lịch sử hoạt động mới giả để lấp dữ liệu chuyển đổi.

## 11. Dữ liệu dùng thử và an toàn dữ liệu

Theo yêu cầu kiểm tra chức năng, local đã bổ sung 120 câu Tiếng Anh: mỗi chủ đề Thì, Mệnh đề quan hệ và Từ vựng theo ngữ cảnh có thêm 20 câu tại mỗi trung tâm mẫu A/B. Câu có nhãn `[DỮ LIỆU DÙNG THỬ]`, lời giải tiếng Việt và một phần câu cấu hình khó.

Script bổ sung có thể chạy lại mà không nhân đôi và không ghi đè nội dung cũ. Chỉ thêm câu hỏi, không tạo Attempts, điểm, bằng chứng hoặc cập nhật năng lực giả. Độ khó của câu thử chưa được hiệu chuẩn về sư phạm.

Mọi thao tác phải kiểm tra tài khoản, trung tâm, quyền sở hữu và phạm vi lớp/giáo trình ở phía server. Lớp đã lưu trữ hoặc phạm vi bị thu hồi không được bỏ qua chỉ vì học sinh còn giữ URL cũ.

Đã sao lưu trước cập nhật local. Lần sửa xem lại/tiếp tục không có migration mới, không xóa MySQL volume và không thay điểm hay bài làm thật.

## 12. Kiến trúc triển khai hiện tại

| Thành phần | Trách nhiệm |
|---|---|
| LearningGoal / PathProposal | Lưu mục tiêu, bản nháp xem trước và xác nhận chọn tuyến |
| LearningPathV4Planner | Lập tuyến theo graph, mục tiêu, bằng chứng và nội dung khả dụng |
| LearningAgenda | Chọn nhiệm vụ theo thời gian, trạng thái và lịch ôn |
| LearningPathSession / SessionRun | Lưu nhiệm vụ và lượt thực hiện bền vững |
| ActivityRun / IssuedItem | Lưu hoạt động, câu đã phát, bản nháp, bài nộp và phản hồi |
| Canonical submission / Evidence Gate | Chấm và kiểm soát độ tin cậy của bằng chứng |
| LearningPathV4Evaluator / PathDecision | Kết luận buổi học và ghi lý do điều chỉnh |
| Trợ giảng AI | Nhận xét tiếng Việt bổ sung, có lưu kết quả và trạng thái chưa khả dụng |

Các thao tác đọc agenda, map, path và lượt đã lưu không tự kích hoạt tuyến hoặc tạo lượt làm. Việc chuyển tuyến, start, submit và đánh giá là hành động riêng có kiểm soát.

## 13. Kết quả kiểm chứng

Các con số dưới đây thuộc từng đợt kiểm tra, không cộng chúng thành một bộ chạy duy nhất và không quy đổi số test thành số tiêu chí sản phẩm hoàn tất.

| Đợt kiểm tra | Kết quả và giới hạn |
|---|---|
| Backend toàn bộ trước đợt sửa xem lại/tiếp tục cuối | 4.341 đạt, 0 lỗi, 103 bỏ qua; chưa phải chạy full lại sau đợt cuối |
| Backend sau sửa xem lại/tiếp tục | 10/10 kiểm thử thực thi đạt, gồm buổi đang dở, resume đúng lượt, đọc bài lưu và cách ly học sinh |
| Frontend sau đợt cuối | 759/759 đạt; build production thành công |
| Lint sau đợt cuối | 0 lỗi, còn 29 cảnh báo trong mã hiện có |
| HTTP/MySQL sau đợt cuối | 10 nhóm đạt trên database thử riêng, gồm start đồng thời, xung đột có tên, đọc lại không tạo lượt/quyết định mới |
| Trình duyệt sau đợt cuối | Đạt luồng desktop/mobile, tiếp tục đúng bản nháp, xem lại, luyện nhiều câu và phản hồi từng câu |
| Provider AI thật ở đợt trợ giảng | Đã thử Gemini / gemini-3.5-flash-lite trên tài khoản thử, trả nhận xét tiếng Việt và dùng lại kết quả lưu; không suy ra mọi cấu hình/provider đều đã kiểm chứng |
| Bổ sung dữ liệu thử | 120 câu/6 phạm vi, chạy lại thêm 0 câu, không tạo Attempt giả |
| Local sau cập nhật cuối | Web 200; API bảo vệ trả 401 khi chưa đăng nhập; API/web dùng image mới |
| Bảo toàn lịch sử ở lần cập nhật cuối | Users/Attempts/Evidence/Twin history giữ nguyên lần lượt 17/15/15/15 |

Các relational suite tùy chọn bị bỏ qua không được tính là đạt. Kiểm thử giao diện tự động có lần cần sửa thao tác mở nội dung thu gọn trong kịch bản; lượt chạy lại thành công dùng tài khoản thử mới. Không xóa hoặc sửa điểm thật để đạt kiểm thử.

## 14. Những phần còn thiếu

V05 không tuyên bố hoàn thành toàn bộ 38 tiêu chí V04. Những hạng mục sau cần tiếp tục xây dựng hoặc kiểm chứng:

1. **Độ chi tiết của kỹ năng:** bổ sung node nhỏ, điều kiện nền tảng và nội dung tương ứng để tuyến khác nhau sâu hơn giữa học sinh.
2. **Kho hoạt động học:** bài giảng, khám phá tương tác, tìm lỗi và vận dụng được biên soạn đầy đủ cho nhiều môn; hỗ trợ một số loại dữ liệu chưa đồng nghĩa có đủ gói nội dung để học.
3. **Quy trình giáo viên biên soạn/xuất bản:** hoàn thiện quản lý phiên bản, thu hồi nội dung sai và xử lý ảnh hưởng tới buổi đang học.
4. **Vận dụng đa kỹ năng:** nhiệm vụ tổng hợp với rubric từng kỹ năng, không chia điểm tổng để tự xác nhận tất cả.
5. **Lịch ôn và dự báo:** hiệu chuẩn bằng thời gian thực, kết quả ghi nhớ và tình huống bỏ buổi; chưa có mô hình cá nhân hóa đầy đủ cho mọi trường hợp.
6. **Khả năng tiếp cận và quy mô:** kiểm chứng sâu hơn thao tác bàn phím, các dạng hoạt động, nhiều thiết bị và tải đồng thời; mobile không tràn ngang chưa chứng minh toàn bộ khả năng tiếp cận.
7. **Hiệu quả học tập:** pilot học sinh thật, kiểm tra chuyển giao và nhớ lại, giáo viên đánh giá chất lượng phản hồi AI và câu hỏi.

Các tiêu chí V4-01…V4-38 trong đặc tả gốc giữ nguyên ý nghĩa. Không đổi nội dung ID hoặc gắn toàn bộ nhãn đạt chỉ vì đã có endpoint/component.

## 15. Kịch bản kiểm tra và demo đề nghị

| Tình huống | Điều cần quan sát |
|---|---|
| Học sinh chưa có bài | Đề xuất xác định điểm xuất phát, không kết luận yếu |
| Học sinh có câu sai | Nêu số câu và kỹ năng liên quan; bản đề xuất điền sẵn phần cần củng cố |
| Đúng ít câu có hướng dẫn | Không tự xác nhận giỏi hoặc đẩy lên nâng cao |
| Đủ căn cứ cho một kỹ năng | Đề xuất câu khó thật khi có nội dung, không kết luận toàn môn |
| Hoạt động tìm hiểu | Có nội dung, cách hiểu đã nộp, AI góp ý riêng và khả năng xem lại |
| Bộ luyện | Nhiều câu khác nhau, phản hồi tham khảo có trước khi AI hoàn tất |
| Tự kiểm tra | Bộ 5 câu mới; chưa lộ lời giải trước khi nộp hết; kết luận chờ bằng chứng |
| Đóng rồi mở lại | Giữ bản nháp và đúng lượt; bài đã hoàn thành có nút Xem lại |
| Mở buổi khác khi còn dở | Thông báo tên buổi đang chặn và nút tiếp tục đúng buổi |
| AI không khả dụng | Có thông báo rõ; bài làm và tiến độ không mất |
| Cuộn hộp thoại | Vẫn nhìn thấy nút đóng; nền ngoài trong suốt |

## 16. Tài liệu đối chiếu

- [Đặc tả V04 và 38 tiêu chí gốc](EDUTWIN-PERSONALIZED-LEARNING-PATH-V4.0.md).
- [Rà soát bản Antigravity và sửa nền tảng](../verification/EDUTWIN-V4-CODEX-REVIEW-2026-10-16.md).
- [Kết nối bài làm, luyện tập và điều chỉnh](../verification/EDUTWIN-CONNECTED-LEARNING-2026-10-16.md).
- [Đề xuất được điền đầy đủ và hướng nâng cao](../verification/EDUTWIN-AUTO-PROPOSAL-2026-10-16.md).
- [Xem lại, trợ giảng AI và dữ liệu dùng thử](../verification/EDUTWIN-REVIEW-TUTOR-DEMO-2026-10-16.md).
- [Sửa xem lại và tiếp tục buổi đang dở](../verification/EDUTWIN-REVIEW-RESUME-2026-10-16.md).
- [Phản hồi khi chọn tuyến học](../verification/EDUTWIN-PROPOSAL-ACCEPT-FEEDBACK-2026-10-16.md).

Các báo cáo theo đợt là bằng chứng tại thời điểm tương ứng. Khi mô tả giao diện hiện tại, áp dụng điều chỉnh mới nhất trong V05; chẳng hạn nút phụ “Xem lượt đã làm” trong báo cáo đợt trước đã được bỏ.
