# EDUTWIN — ĐẶC TẢ LỘ TRÌNH HỌC TẬP CÁ NHÂN V4.0

> **Ngày:** 16/10/2026
>
> **Tên định hướng:** Bản đồ năng lực cá nhân và nhiệm vụ học tập có mục đích
>
> **Trạng thái:** Đề xuất thiết kế và triển khai theo yêu cầu người dùng; chưa phải chức năng đã được lập trình hoặc nghiệm thu.
>
> **Phạm vi:** Thiết kế lại trải nghiệm và cơ chế lộ trình; cho phép thay kiến trúc cũ khi cần. Không mặc định giữ giới hạn tính năng hoặc số bảng của đề xuất trước.
>
> **Đối tượng đầu tiên:** Học sinh THPT trong trung tâm, giáo viên phụ trách môn học.
>
> **Nguyên tắc:** Học sinh hiểu mình đang ở đâu, muốn đi đến đâu, vì sao học hoạt động này và đã làm được điều gì sau khi học.

## 1. Quyết định sản phẩm

EduTwin tổ chức việc học quanh **những khả năng học sinh cần đạt**, thay vì tạo một lịch nhiều tuần rồi dẫn mọi nút “Bắt đầu” đến cùng loại bài tập.

Một lộ trình cá nhân gồm năm thành phần:

1. **Đích đến:** học sinh muốn làm được gì, trong bối cảnh và thời gian nào.
2. **Bản đồ năng lực:** kiến thức hiện biết, chưa rõ, cần củng cố và đã sẵn sàng học.
3. **Tuyến học được đề xuất:** những bước phù hợp để đi từ vị trí hiện tại tới đích.
4. **Nhiệm vụ hôm nay:** hoạt động cụ thể phù hợp với thời gian, tiến độ và nhu cầu hiện tại.
5. **Bằng chứng và điều chỉnh:** kết quả thật quyết định bước tiếp theo; học sinh được biết và tham gia lựa chọn.

**Lời hứa với học sinh:** “Em có một tuyến học phù hợp với mình, được hướng dẫn khi mắc kẹt và được đi nhanh ở phần đã biết.” Không hứa chắc chắn tăng điểm hoặc đạt mục tiêu trong một thời hạn chưa kiểm chứng.

**Dấu hiệu phân biệt với luyện tập tự do:** học sinh có mục tiêu dài hơn một lượt làm bài; hoạt động đa dạng; có điểm bắt đầu riêng; có thử thách vận dụng; có lý do điều chỉnh và hồ sơ những khả năng đã chứng minh.

## 2. Căn cứ tham khảo và giới hạn suy luận

Các mô tả dưới đây dựa trên trang công khai và hướng dẫn chính thức đã tham khảo. Chúng là mẫu thiết kế sản phẩm, không phải bằng chứng rằng sao chép tính năng sẽ cải thiện kết quả học của EduTwin.

| Nền tảng | Điều tham khảo | Chuyển thành quyết định cho EduTwin |
|---|---|---|
| [Duolingo — thiết kế đường học](https://blog.duolingo.com/new-duolingo-home-screen-design/) | Dẫn từng bước, xen kẽ học mới và ôn, đưa nhiều loại hoạt động vào một đường học | Có bước tiếp theo rõ ràng; ôn tập là một phần của tiến trình |
| [Brilliant — Learning Paths](https://brilliant.org/help/features/what-are-learning-paths/) | Chuỗi chủ đề có trình tự, kết hợp giải thích và giải quyết vấn đề tương tác | Có hoạt động khám phá, thử và phản hồi ngay trong quá trình hiểu bài |
| [Khan Academy — Mastery](https://support.khanacademy.org/hc/en-us/articles/360007253831-Using-self-paced-practice-and-Mastery-in-the-classroom) | Theo dõi kỹ năng, chương và khóa học, có đánh giá ở nhiều phạm vi | Tách tiến độ hoạt động khỏi trạng thái năng lực; có kiểm tra tổng hợp |
| [ALEKS — hướng dẫn cho học sinh](https://www.mheducation.com/prek-12/support/knowledge/aleks-frequently-asked-questions-faq-for-students.html) | Ready to Learn, quyền chọn chủ đề, Learned và Mastered, kiểm tra duy trì kiến thức | Chỉ đề xuất nhánh đủ nền tảng, cho lựa chọn có giới hạn, phân biệt đã luyện được với đã kiểm chứng |
| [Codecademy — Career Paths](https://help.codecademy.com/hc/en-us/articles/360022552694-What-is-a-Career-Path) | Hướng tới đầu ra cụ thể, kết hợp bài học, quiz và dự án | Mỗi chặng kết thúc bằng nhiệm vụ vận dụng và mô tả “em đã làm được gì” |

Các thuật toán, ngưỡng, thời lượng và quy tắc dưới đây là **thiết kế đề xuất của EduTwin**, phải được giáo viên kiểm tra và pilot. Không trình bày chúng như thuật toán nội bộ của các nền tảng tham khảo.

## 3. Thế nào là “lộ trình của riêng mình”?

Thay tên học sinh, đổi lời giải thích hoặc đổi số tuần chưa đủ. Với cùng môn và cùng mục tiêu, lộ trình phải có thể khác ở các chiều sau:

| Chiều cá nhân hóa | Dữ liệu dùng | Khác biệt phải quan sát được |
|---|---|---|
| Điểm bắt đầu | Bằng chứng theo kỹ năng, kiểm tra ngắn nếu cần | Một em bắt đầu ở phân số, em khác vào thẳng phương trình |
| Nội dung phải học | Mục tiêu và các nền tảng cần thiết | Loại khỏi tuyến chính những kỹ năng không phục vụ mục tiêu |
| Mức hỗ trợ | Lỗi đã xác nhận, số lần cần trợ giúp, kết quả gần đây | Ví dụ có hướng dẫn, hoạt động tìm lỗi hoặc tự giải |
| Nhịp học | Lịch rảnh, tiến độ thực tế, lựa chọn hôm nay | Khối lượng và dự báo thay đổi, không ép cùng lịch |
| Đường đi | Prerequisite, điểm mạnh, điểm vướng | Bỏ qua có kiểm chứng, bổ trợ đúng lỗ hổng hoặc chọn nhánh khác |
| Cách hoạt động | Nội dung sẵn có, lựa chọn và khả năng tiếp cận | Chọn ví dụ từng bước, tương tác hoặc luyện ngắn phù hợp |
| Ôn lại | Thời điểm bằng chứng, kết quả kiểm tra nhớ lại | Hai học sinh có hàng đợi ôn khác nhau |

**Kiểm tra tính cá nhân hóa:** giữ nguyên tên, môn và mục tiêu, chỉ đổi hồ sơ bằng chứng; hệ thống phải đổi ít nhất điểm bắt đầu, nhiệm vụ hoặc quyết định hỗ trợ khi dữ liệu có ý nghĩa khác nhau. Nếu chỉ đổi văn bản, chưa đạt.

Không phân loại cố định học sinh thành “kiểu người học bằng hình ảnh/âm thanh”. Sở thích là lựa chọn giao diện và hoạt động, không phải kết luận khoa học về khả năng học.

## 4. Ba tình huống mẫu phải thiết kế được

| Học sinh | Bối cảnh | Lộ trình phù hợp |
|---|---|---|
| An | Muốn theo kịp phương trình; bằng chứng cho thấy mắc lỗi tính phân số; 15 phút/ngày | Bổ trợ phân số liên quan → phương trình cơ bản → vận dụng; vẫn cho xem bài trên lớp |
| Bình | Đã giải tốt cơ bản; chưa có bằng chứng về bài toán lời văn; 30 phút/ngày | Kiểm tra nhanh để bỏ qua cơ bản → học mô hình hóa → thử thách thực tế |
| Chi | Học sinh mới; tự nhận khá; sắp kiểm tra; chỉ có 10 phút/ngày | Tuyến tạm thời và kiểm tra ngắn đúng mục tiêu; điều chỉnh khi có dữ liệu; thương lượng phạm vi nếu thời gian thiếu |

Tự nhận “khá” không cấp trạng thái đã thành thạo. Thiếu dữ liệu không bị gán là yếu. Hạn kiểm tra gần không cho phép bỏ nền tảng bắt buộc hoặc hứa chắc đạt điểm.

## 5. Các khái niệm nghiệp vụ

| Khái niệm | Định nghĩa |
|---|---|
| Skill | Khả năng cụ thể có thể quan sát, ví dụ “bỏ ngoặc có hệ số âm” |
| Topic | Nhóm kiến thức rộng hơn, ví dụ “phương trình bậc nhất” |
| Goal | Đích đến do học sinh lựa chọn, gắn phạm vi và tiêu chí đầu ra |
| Milestone | Chặng có đầu ra ý nghĩa, gồm một số Skill và thử thách tổng hợp |
| Path | Tuyến cá nhân đi qua Skill/Milestone; là kế hoạch thay đổi có kiểm soát |
| Mission | Nhiệm vụ học cụ thể, có mục đích và thời lượng ước lượng |
| Activity | Một hoạt động bên trong Mission: khám phá, tìm lỗi, luyện, kiểm tra… |
| Run | Một lần thực hiện Mission, có vị trí, bản nháp và kết quả bền vững |
| Evidence | Bằng chứng bài làm được pipeline hiện hữu đánh giá độ tin cậy |
| Review | Hoạt động ôn/kiểm tra lại kiến thức theo nhu cầu, không đồng nghĩa teacher review |

Trong tài liệu, `Session` ở code hiện tại được định hướng chuyển thành `Mission`; `SessionRun` được tận dụng cho Run. “Chờ giáo viên” luôn ghi rõ `PendingTeacherReview`, tránh nhập nhằng với ôn tập.

Skill trong lần đầu nên tận dụng KnowledgeNode hiện hữu nếu node đủ cụ thể. Nếu graph chỉ có chương lớn, cần chuẩn hóa hoặc bổ sung node kỹ năng; không chỉ đổi nhãn chương thành “kỹ năng”.

## 6. Khởi tạo lộ trình: từ mong muốn tới đề xuất có thể xem lại

### 6.1. Bước chọn mục tiêu

Hỏi ngắn, bằng ngôn ngữ học sinh:

- Em muốn theo kịp bài trên lớp, học lại nền tảng, chuẩn bị kiểm tra hay học nâng cao?
- Môn và phạm vi nào? Gợi ý từ lớp/giáo trình đang học, cho chọn trong quyền hiện tại.
- Có mốc thời gian không? Ngày kiểm tra là dữ liệu do học sinh/giáo viên cung cấp, không phải hệ thống tự suy đoán.
- Thường có bao nhiêu phút và những ngày nào có thể học?
- Có phần nào em muốn được giúp? Có thể bỏ qua câu này.

Mục tiêu được chuẩn hóa thành `TargetSkillIds`, đầu ra vận dụng, deadline tùy chọn và ngân sách thời gian. Mục tiêu “đạt 8 điểm” cần phạm vi đề và tiêu chí giáo viên; nếu chưa có, chỉ ghi nguyện vọng, không suy ra dự báo điểm giả.

### 6.2. Bước xác định điểm xuất phát

Backend đọc bằng chứng trong phạm vi được phép: kết quả gần đây, nguồn/trust, kỹ năng liên quan, thời điểm, mức trợ giúp và teacher review. Không yêu cầu học sinh làm lại bài đánh giá nếu đã đủ dữ liệu phù hợp.

| Tình trạng | Hành vi |
|---|---|
| Đủ bằng chứng mới và có độ bao phủ phù hợp | Lập tuyến từ vị trí hiện có |
| Thiếu một số kỹ năng liên quan | Đề xuất kiểm tra ngắn các phần chưa rõ |
| Gần như chưa có dữ liệu | Cho lựa chọn kiểm tra định hướng hoặc bắt đầu tuyến tạm thời |
| Thiếu nội dung đánh giá hợp lệ | Nêu phần chưa xác định, dùng tuyến thận trọng; báo nội dung cần giáo viên bổ sung |

Kiểm tra định hướng ban đầu: khoảng 3–8 câu, chia thành các cụm ngắn nếu học sinh chỉ có ít thời gian. Có nút “Em chưa biết”; kết quả này thể hiện cần hỗ trợ, không tự gán toàn bộ chủ đề mastery 0. Số câu là giới hạn trải nghiệm đề xuất, không cam kết đủ đo toàn bộ chương.

Không dùng một câu trả lời để khẳng định chắc chắn học sinh đã nắm vững một kỹ năng. Khi giới hạn câu đã hết mà độ bao phủ còn thiếu, giữ trạng thái chưa rõ và tiếp tục thu thập khi học.

### 6.3. Bước xem và chọn tuyến

Trang preview phải có:

1. Đích đến và khả năng cuối chặng.
2. Điểm bắt đầu, dữ liệu đã dùng và phần còn chưa rõ.
3. Các chặng chính, nền tảng cần bổ sung, phần được đề xuất bỏ qua.
4. Khoảng thời gian ước lượng và giả định, ví dụ “khoảng 3–5 tuần với 15 phút, 5 ngày/tuần”.
5. Nhiệm vụ đầu tiên, lý do chọn và lựa chọn hợp lệ khác nếu có.
6. Cảnh báo khả thi: thiếu thời gian, nội dung hoặc bằng chứng.

Học sinh được đổi lịch rảnh, mục tiêu/phạm vi và cách hỗ trợ mong muốn; không được tự đánh dấu đã nắm vững để bỏ điều kiện học. Có nút kiểm tra nhanh để chứng minh phần đã biết.

Chỉ khi xác nhận mới kích hoạt tuyến mới. Preview không thay thế path đang dùng. Đây là thiết kế mới có chủ đích: có thể dùng tài nguyên `PathProposal` riêng để không ép sửa enum Path cũ thành Draft.

## 7. Bản đồ năng lực và tuyến học

### 7.1. Bản đồ phải đọc được

Mặc định hiển thị một chặng và khoảng 6–12 node liên quan, có thu gọn các nhóm. Trên điện thoại dùng danh sách phân cấp tương đương; không bắt học sinh thao tác với graph lớn.

Hiển thị hai lớp riêng:

- **Trạng thái hiểu biết:** `Unknown`, `Developing`, `Demonstrated`, `Confirmed`, `ReviewDue`.
- **Khả năng bắt đầu:** `Available`, `PrerequisiteBlocked`, `ContentUnavailable`, `OutOfScope`.

`Demonstrated`: đã làm được trong hoạt động luyện nhưng chưa đủ xác nhận độc lập. `Confirmed`: đạt chính sách bằng chứng của kỹ năng. `ReviewDue`: cần kiểm tra duy trì, không có nghĩa tự động mất năng lực đã xác nhận.

Đây là phép chiếu phục vụ lập kế hoạch từ Digital Twin + evidence và lịch sử hoạt động, không phải một mô hình mastery thứ hai tự ghi điểm. Mỗi trạng thái phải có policy version, evidence tham chiếu và độ chắc chắn/mức bao phủ phù hợp. Không tạo confidence % nếu hệ thống chưa có mô hình hiệu chuẩn.

### 7.2. Nội dung khi chọn node

Thẻ node hiển thị: “Em sẽ làm được gì”, nền tảng, trạng thái và căn cứ, hoạt động có thể bắt đầu, ước lượng thời gian, lý do ưu tiên, lựa chọn học/ôn/kiểm tra nhanh. Nếu bị chặn, nói rõ bởi kỹ năng nào hoặc thiếu nội dung gì.

Cho xem trước mục tiêu và nội dung giới thiệu của nhánh khó. Chỉ khóa hoạt động cần điều kiện thực sự, không khóa toàn bộ việc khám phá. Mọi quyền truy cập nội dung vẫn do backend kiểm tra.

### 7.3. Quyền lựa chọn

Hệ thống đưa một đề xuất chính và tối đa hai lựa chọn khác khi hợp lệ. Học sinh có thể chọn kỹ năng khác đã sẵn sàng, học lại phần cũ hoặc xin trợ giúp. Không tạo lựa chọn giả khi chỉ có một hoạt động đủ điều kiện.

Không dùng đường zíc zắc, huy hiệu hoặc hiệu ứng thay cho cấu trúc học. Đường nối phải thể hiện quan hệ hoặc trình tự có nghĩa; giao diện có thể dùng danh sách nếu rõ hơn graph.

## 8. Nhiệm vụ hôm nay và quản lý thời gian thực tế

### 8.1. Màn hình mặc định

```text
MỤC TIÊU: Tự giải được bài toán bằng phương trình
Chặng hiện tại: Phương trình có ngoặc

Hôm nay em có: [5 phút] [15 phút] [30 phút] [Tự chọn]

ĐỀ XUẤT: Tìm lỗi khi bỏ ngoặc — khoảng 8 phút
Vì sao: Em đang nhầm dấu âm trong các bài gần đây.
[Bắt đầu] [Xem lựa chọn khác]

ĐANG DỞ: Khám phá phép biến đổi tương đương — bước 2/4
[Tiếp tục]

SẮP TỚI: Bài toán gói cước — thử thách cuối chặng
[Xem mục tiêu]
```

Một người chỉ có một Run tương tác đang mở cho một môn; start mới phải chọn tiếp tục hoặc tạm dừng Run cũ rõ ràng. Có thể có nhiều Run chờ chấm, nhưng không tạo lần kiểm tra mới để né kết quả đang chờ duyệt của cùng mục tiêu.

### 8.2. Quy tắc 5/15/30 phút

| Ngân sách hôm nay | Nhiệm vụ phù hợp |
|---|---|
| Khoảng 5 phút | Một lỗi cụ thể, kiểm tra nhớ lại ngắn, tiếp tục một bước đang dở |
| Khoảng 15 phút | Một khái niệm hoặc một kỹ năng với hoạt động liên quan |
| Khoảng 30 phút | Một cụm kỹ năng hoặc thử thách tổng hợp |

Không ép một bài 20 phút vào thẻ 5 phút bằng cách đổi nhãn. Nếu chưa có hoạt động đủ ngắn, đề xuất tiếp tục một phần có thể lưu hoặc hẹn hoạt động dài hơn. Thời lượng là ước lượng, không phải đồng hồ thi hoặc giới hạn cứng.

### 8.3. Nghỉ học và kế hoạch quá sức

- Bỏ một ngày: dời công việc chưa làm, không cộng gấp đôi bài ngày sau.
- Nghỉ dài: đề xuất kiểm tra nhớ lại ngắn ở phần quan trọng; không bắt học lại tất cả.
- Còn ít thời gian trước deadline: trình bày các phương án giảm phạm vi, tăng thời gian nếu học sinh đồng ý hoặc ưu tiên kiến thức trọng tâm nhưng giữ nền tảng bắt buộc.
- Học nhanh hơn dự kiến: đề xuất thử thách hoặc kiểm tra vượt phần; không tự nhồi thêm khối lượng.
- Hoàn thành thời lượng chưa có nghĩa đạt năng lực. Dừng giữa buổi phải lưu và có thể tiếp tục.

Không dùng streak mất trắng, bảng xếp hạng hoặc thông báo gây áp lực làm cơ chế điều phối cốt lõi. Có thể ghi nhận thói quen học đều sau khi luồng học đã hữu ích.

### 8.4. Bài giáo viên

Bài được giao xuất hiện cùng kế hoạch cá nhân, có nguồn, hạn, trạng thái và thời lượng ước lượng. Bài bắt buộc trong phạm vi sắp đến hạn được ưu tiên theo chính sách giáo viên; quá hạn phải hiện trạng thái và quyền còn được nộp. Không gắn nhãn mọi bài chưa hoàn thành là khẩn cấp.

Học sinh thấy cả nghĩa vụ và tuyến cá nhân; hệ thống trừ phần thời gian dự kiến cho bài bắt buộc trước khi đề xuất khối lượng tự học. Assignment chỉ được đóng góp evidence qua Attempt thật; không đếm lần hai khi kết quả được dùng để điều chỉnh Path.

## 9. Thư viện hoạt động và trải nghiệm riêng

Không quy định mọi Mission đều đi qua cùng bốn bước. Mission chọn các hoạt động phù hợp với mục đích: ôn một lỗi không cần học lại cả lý thuyết; kiểm tra vượt phần không cần làm luyện tập trước.

| Loại | Mục đích | Tương tác tối thiểu | Hoàn thành hoạt động | Giá trị bằng chứng |
|---|---|---|---|---|
| Explore | Hiểu một quan hệ/khái niệm | Thay đổi giá trị, chọn dự đoán, quan sát, trả lời câu hỏi | Thực hiện các bước bắt buộc | Chủ yếu điều phối hỗ trợ; không tự xác nhận năng lực |
| WorkedExample | Hiểu cách giải | Mở từng bước và dự đoán bước tiếp | Trả lời tương tác bắt buộc, xem giải thích | Không thay thế kiểm tra độc lập |
| CompleteSolution | Hoàn thiện lập luận | Điền/sắp xếp bước thiếu | Nộp các bước cần thiết | Đánh dấu mức trợ giúp, chỉ dùng theo policy |
| FindError | Nhận diện hiểu sai | Chỉ bước sai, sửa và giải thích | Nộp vị trí lỗi và sửa lỗi | Có thể tạo evidence nếu chế độ đánh giá hợp lệ |
| Explain | Diễn đạt lý do | Viết/chọn giải thích cho một phép biến đổi | Nộp phản hồi | Rule/AI/teacher review theo rubric, không tự coi tự nhận là đúng |
| Practice | Làm chắc kỹ năng | Giải câu, có trợ giúp được ghi nhận | Hoàn thành tập/điểm dừng đã chốt | Guided evidence, không giả làm độc lập |
| IndependentCheck | Chứng minh tự làm | Câu mới, không gợi ý/đáp án trước nộp | Đủ câu và chấm theo policy | Có thể xác nhận kỹ năng qua Evidence Gate |
| TransferChallenge | Vận dụng nhiều kỹ năng | Tình huống thực tế, mô hình hóa và giải thích | Nộp các phần rubric | Bằng chứng nhiều kỹ năng nếu được ánh xạ và chấm riêng |
| RecallReview | Kiểm tra nhớ lại | Câu ngắn sau khoảng thời gian | Nộp và nhận kết quả | Chỉ evidence hợp lệ mới đổi nhận định năng lực |
| Reflection | Tự nhìn lại | “Em còn vướng gì?”, “Phần nào cần giúp?” | Lưu lựa chọn/ghi chú | Tín hiệu sở thích/nhu cầu, không cập nhật mastery |

Mỗi loại có renderer và phản hồi riêng. Dùng chung thành phần nhập toán, xem nội dung và nộp bài khi phù hợp; không dùng cùng một trang câu hỏi cho toàn bộ loại hoạt động rồi chỉ đổi tiêu đề.

### 9.1. Ví dụ Explore: cân bằng phương trình

Giáo viên chuẩn bị mô hình hai vế bằng nhau. Học sinh dự đoán điều gì xảy ra khi thêm 3 vào một vế, sau đó thêm vào cả hai vế. Hệ thống phản hồi bằng trạng thái mô hình và câu hỏi giải thích. Toàn bộ hoạt động có thể dùng template HTML/SVG xác định trước; không cần AI sinh một mô phỏng mới lúc học.

Có cách làm bằng bàn phím và danh sách lựa chọn thay cho kéo thả. Văn bản thay thế mô tả trạng thái cân; không chỉ dùng màu hoặc chuyển động truyền đạt ý nghĩa.

### 9.2. Ví dụ FindError

Lời giải mẫu có một lỗi có chủ đích: `-2(x-3) = -2x-6`. Học sinh chọn bước sai, sửa thành `-2x+6`, giải thích dấu. Nội dung phân biệt lỗi toán với cách diễn đạt; đáp án đúng và rubric phải được giáo viên phê duyệt.

### 9.3. Ví dụ TransferChallenge

So sánh hai gói cước: phí cố định và phí theo mức sử dụng được cho đầy đủ. Học sinh xác định ẩn, lập hai biểu thức, tìm điểm hòa vốn và giải thích lựa chọn cho một tình huống. Rubric phân tách mô hình hóa, biến đổi và kết luận; không gán điểm tổng thành mastery bằng nhau cho mọi Skill.

## 10. Biên soạn nội dung là một phần bắt buộc của sản phẩm

### 10.1. Gói nội dung một kỹ năng

Mỗi Skill phát hành trong tuyến đầu tiên phải có:

- Mô tả đầu ra quan sát được và prerequisite đã kiểm tra.
- Ít nhất một cách giải thích hoặc ví dụ có hướng dẫn đã được duyệt.
- Ít nhất hai loại hoạt động phù hợp; không ép mọi Skill phải có mô phỏng.
- Câu luyện, kiểm tra độc lập, rubric và phương án khi thiếu nội dung.
- Nhãn lỗi thường gặp có căn cứ chuyên môn; ánh xạ phản hồi và hoạt động bổ trợ.
- Thời lượng ước lượng, độ khó, mức trợ giúp, phạm vi môn/lớp/tenant.
- Nội dung thay thế khi không dùng AI, thao tác kéo thả hoặc thiết bị nhỏ.

Số lượng câu cần có được tính từ chính sách kiểm tra, số lượt retry muốn hỗ trợ và nhu cầu ôn; không đặt một chỉ tiêu chung thiếu căn cứ cho mọi Skill.

### 10.2. Quy trình giáo viên

`Draft → InReview → Published → Retired`. Teacher có quyền biên soạn và publisher theo quyền hiện hành phê duyệt; CenterManager không mặc nhiên được sửa nội dung học thuật. Nội dung AI tạo luôn ở Draft, có nguồn và người kiểm tra.

Một phiên bản Published là bất biến. Sửa nội dung tạo version mới. Run đang mở giữ version đã nhận; nếu nội dung bị thu hồi vì sai hoặc mất quyền, dừng hoạt động và yêu cầu thay thế có thông báo, không tiếp tục dùng bản sai vì đã snapshot.

Màn kiểm tra độ sẵn sàng nội dung phải báo Skill thiếu bài học, thiếu checkpoint, trùng câu luyện/kiểm tra, rubric thiếu hoặc prerequisite không hợp lệ. Không cho tuyên bố “sẵn sàng học đầy đủ” khi chỉ có danh sách tên kỹ năng.

Không sao chép bài học, hình ảnh hoặc ngân hàng câu của nền tảng tham khảo. Dùng nội dung nhóm tự biên soạn hoặc được phép sử dụng.

## 11. Mô hình người học và bằng chứng

### 11.1. Tín hiệu được dùng

| Nhóm | Dữ liệu | Quy tắc |
|---|---|---|
| Năng lực | Attempt và evidence hợp lệ, kết quả theo Skill, teacher review | Nguồn quyết định khả năng đã chứng minh |
| Sai lầm | Nhãn lỗi, bằng chứng lặp lại, phản hồi giáo viên | AI nghi ngờ lỗi chỉ là observation, chưa thành nhãn chắc chắn |
| Trợ giúp | Hint/example đã mở, lời giải đã lộ, mức hỗ trợ | Gắn với ActivityRun/Attempt, ảnh hưởng tư cách độc lập |
| Thời gian | Thời gian tương tác có giới hạn và lịch rảnh | Phục vụ ước lượng; tab nền không phải thời gian học đáng tin |
| Tự báo cáo | Quá khó/dễ, chưa học, cần nghỉ, sở thích | Điều chỉnh hỗ trợ và đề xuất kiểm tra, không tự xác nhận mastery |
| Bối cảnh | Lớp, giáo trình, deadline, assignment, nội dung khả dụng | Ràng buộc phạm vi và thứ tự ưu tiên |

Không suy ra lười, năng lực thấp hoặc cảm xúc từ một lần trả lời chậm. Behavior Twin không được dùng để phạt bằng cách khóa nội dung học hợp lệ.

### 11.2. Hồ sơ kỹ năng phục vụ quyết định

Backend tạo read model theo Skill: trạng thái, bằng chứng gần nhất, số nguồn/câu độc lập, mức bao phủ, thời điểm kiểm tra, điểm vướng có căn cứ, hoạt động phù hợp và lý do. Đây là projection có thể dựng lại; Digital Twin/evidence hiện hữu vẫn là nguồn năng lực chuẩn.

Thời gian trôi qua tạo `ReviewDue`, không tự ghi giảm mastery. Khi có kiểm tra mới không đạt, chính sách evidence quyết định cập nhật và giữ lịch sử kết quả trước.

### 11.3. Không gộp các kết quả khác nhau

`ActivityDone`, `MissionFinished`, `SkillDemonstrated`, `SkillConfirmed`, `GoalAchieved` là những kết quả riêng. Một học sinh có thể hoàn thành nhiệm vụ ôn nhưng chưa đủ bằng chứng xác nhận kỹ năng. Mục tiêu chỉ đạt khi tất cả tiêu chí đầu ra bắt buộc đã được chứng minh, gồm vận dụng nếu mục tiêu yêu cầu.

## 12. Bộ lập lộ trình và chọn nhiệm vụ

### 12.1. Kiến trúc quyết định

```text
Goal + Curriculum Scope + Knowledge Graph
                  +
Learner Evidence Projection + Published Content + Time Budget
                  ↓
Lọc quyền, nội dung và điều kiện nền tảng
                  ↓
Tạo tập Skill cần thiết và các Mission hợp lệ
                  ↓
Xếp ưu tiên có version + tính khả thi
                  ↓
Đề xuất chính + lựa chọn khác + lý do có căn cứ
                  ↓
AI diễn giải/hỗ trợ trong phạm vi hợp lệ, nếu khả dụng
                  ↓
Học sinh lựa chọn → Run → Evidence → Điều chỉnh có kiểm soát
```

### 12.2. Lập tuyến ban đầu

1. Chuẩn hóa Goal thành các Skill đầu ra và milestone.
2. Tính prerequisite closure trong scope được phép; graph có chu trình/thiếu node phải trả lỗi cấu hình có thể xử lý.
3. Đọc trạng thái từng Skill, giữ Unknown riêng với kết quả thấp đã đo.
4. Đánh dấu phần đã đủ xác nhận có thể bỏ qua; phần chưa rõ cần probe hoặc học tạm thời.
5. Xếp thứ tự nền tảng trước và chèn hoạt động bổ trợ đúng nhu cầu.
6. Kiểm tra nội dung thực sự có thể học và ngân sách thời gian.
7. Tạo milestone và tuyến gợi ý, kèm khoảng thời gian, giả định và thiếu hụt.
8. Sinh preview. Học sinh xác nhận mới kích hoạt.

Tuyến dài hạn chốt mục tiêu và quan hệ kỹ năng; chỉ lên chi tiết ngắn hạn cho khoảng một tuần hoặc một vài nhiệm vụ tiếp theo. Không tạo hàng chục buổi và bộ câu cố định cho nhiều tuần trong khi chưa biết kết quả học đầu tiên.

### 12.3. Chọn nhiệm vụ tiếp theo

Lọc điều kiện bắt buộc trước khi xếp hạng: tenant/owner, môn/lớp, trạng thái nội dung, prerequisite, run đang dở, review đang chặn và khả năng đủ câu của policy. Không dùng điểm ưu tiên để vượt điều kiện bắt buộc.

Thứ tự lựa chọn phiên bản đầu:

1. Tiếp tục Run đang dở nếu phù hợp và học sinh muốn tiếp tục.
2. Nghĩa vụ giáo viên đến hạn theo bối cảnh và quyền nộp.
3. Lỗ hổng nền tảng đang chặn mục tiêu chính, có bằng chứng phù hợp.
4. Kiểm tra nhớ lại của kỹ năng quan trọng đã đến hạn.
5. Skill tiếp theo thuộc mục tiêu và đã sẵn sàng.
6. Thử thách mở rộng khi mục tiêu hiện tại đã đủ điều kiện.

Đây là thứ tự mặc định, không phải buộc cắt ngang Run. Nếu nghĩa vụ đến hạn khẩn, thông báo và cho chuyển an toàn. Trong cùng nhóm, ưu tiên mức liên quan mục tiêu, tín hiệu vướng đáng tin và độ phù hợp thời lượng; dùng tie-break ổn định theo thứ tự giáo trình và ID.

Lần đầu ưu tiên luật minh bạch, có version; chưa cần hệ số trọng số giả chính xác. Lưu các candidate bị loại và reason code ở mức metadata cần giải thích, không lưu toàn bộ prompt/nội dung nhạy cảm vào nhật ký quyết định.

### 12.4. Ước lượng thời lượng và khả thi

Ước lượng ban đầu từ metadata do tác giả nhập. Khi có đủ dữ liệu, điều chỉnh bằng thời gian tương tác gần đây và loại hoạt động, có giới hạn để không bị tab mở lâu làm sai dự báo. Luôn hiển thị khoảng và trạng thái “ước lượng ban đầu” khi ít dữ liệu.

`Khối lượng còn lại` bao gồm học mới, phần bổ trợ đã biết và một dự phòng ôn/kiểm tra. `Ngân sách` từ lịch rảnh thực tế, trừ nghĩa vụ đã biết mà không đếm hai lần. Nếu thiếu ngân sách: hiển thị thiếu bao nhiêu và đưa phương án, không tự hạ tiêu chí đạt.

### 12.5. Tín hiệu điều chỉnh và phản ứng

| Tín hiệu | Điều chỉnh |
|---|---|
| Liên tiếp làm đúng độc lập, có đủ bao phủ | Đề xuất kiểm tra vượt phần hoặc hoạt động khó hơn |
| Sai cùng lỗi đã có bằng chứng lặp lại | Chèn FindError/WorkedExample ngắn đúng lỗi |
| Sai nhiều nhưng chưa xác định nguyên nhân | Probe nền tảng hoặc đề nghị giáo viên, không tự gán misconception |
| Dùng nhiều trợ giúp nhưng làm được | Giảm hỗ trợ từ từ rồi kiểm tra độc lập |
| Báo “quá dễ” | Đề xuất thử thách/kiểm tra nhanh; chưa tự xác nhận |
| Báo “quá khó/chưa học” | Đề xuất cách giải thích khác hoặc bước nền tảng |
| Nghỉ học hoặc đổi lịch | Điều chỉnh lượng việc tương lai và dự báo |
| Kết quả chờ giáo viên | Giữ đánh giá liên quan, có thể cho học nhánh không phụ thuộc |
| Cạn nội dung hợp lệ | Dừng kết luận, nêu thiếu nội dung và báo giáo viên |

Giới hạn chống dao động: không đổi Mission đang làm do một lỗi; thay đổi nhỏ gom tại ranh giới hoạt động hoặc sau evaluate. Điều chỉnh tuyến chính sau bằng chứng mới có ý nghĩa hoặc thay mục tiêu; không regenerate toàn Path sau mỗi Attempt.

Các ngưỡng “lặp lại”, “đủ bao phủ”, “mới” phải nằm trong policy version, có fixture và được giáo viên kiểm tra; không giấu số magic trong prompt.

### 12.6. Bộ luật pilot đủ cụ thể để triển khai

Các mặc định sau chỉ áp dụng chương pilot và phải được giáo viên duyệt trước khi bật. Có thể cấu hình lại theo Skill, nhưng phải tạo policy version và test fixture tương ứng.

| Quy tắc | Mặc định pilot | Giới hạn diễn giải |
|---|---|---|
| Bằng chứng để xem xét bỏ phần | Thỏa policy xác nhận của Skill; bằng chứng độc lập liên quan trong 30 ngày gần nhất | Cũ hơn thì đề xuất kiểm tra nhớ lại, không tự coi mất kiến thức |
| Lỗi lặp để đề xuất bổ trợ cụ thể | Cùng nhãn lỗi có căn cứ trên ít nhất 2 câu khác nhau trong 3 Attempt liên quan gần nhất, trong 14 ngày | Hai output AI chưa được phép dùng không tự tạo “lỗi đã xác nhận” |
| Chưa xác định lỗi nhưng đang mắc kẹt | Hai nhiệm vụ cùng Skill kết thúc NeedsSupport | Đề xuất probe prerequisite hoặc giáo viên, không đoán một nhãn lỗi |
| Giảm hỗ trợ | Hoàn thành hai câu luyện khác nhau liên tiếp mà không mở trợ giúp | Chuyển sang hoạt động ít trợ giúp hơn, chưa Confirmed |
| Đề xuất đi nhanh | Đã đủ policy độc lập của Skill, phần còn lại chỉ là luyện cùng đầu ra | Bỏ luyện thừa; prerequisite và rubric mục tiêu vẫn được giữ |
| Chống dao động | Tối đa một lần thay tuyến tương lai sau mỗi Mission được evaluate hoặc một thay đổi Goal/lịch được chấp nhận | Có thể cập nhật trạng thái chờ chấm; không đảo tuyến sau từng click |
| Nghỉ dài | Không có hoạt động học được xác nhận trong 7 ngày | Đề xuất recall ở Skill liên quan trước khi tiếp tục; cho từ chối, giữ phần chưa rõ |
| Lịch chi tiết | Tối đa 7 ngày hoặc 5 Mission tương lai, tùy giới hạn nào đến trước | Các milestone xa hơn giữ ở mức Skill/đầu ra, không chốt đề trước |

Các con số là tham số thử nghiệm để lập trình nhất quán, không phải bằng chứng về trí nhớ, khả năng hay hiệu quả học tập. Người học phản hồi và giáo viên có thể khiến nhóm điều chỉnh policy sau pilot.

Khi nhiều tín hiệu xung đột: quyết định giáo viên hợp lệ và quyền hiện tại được áp trước; giữ kết luận năng lực đã xác nhận tới khi có evidence mới đủ thẩm quyền thay đổi; dùng tín hiệu luyện để điều chỉnh trợ giúp. Không lấy một lỗi luyện vừa xảy ra để phủ định một bộ bằng chứng độc lập mà chưa qua policy.

## 13. Vai trò AI

### 13.1. Những việc AI có ích

- Diễn giải vì sao đề xuất dựa trên dữ kiện backend cung cấp.
- Giải thích khác cách ở một bước, đặt câu hỏi gợi mở và giúp hiểu phản hồi.
- Phân tích reasoning/misconception như observation qua pipeline kiểm tra hiện hữu.
- Đề xuất cách hỗ trợ trong tập candidate đã được backend xác thực.
- Hỗ trợ giáo viên soạn bản nháp bài học, lỗi mẫu và rubric để duyệt.
- Tổng hợp cuối tuần: phần đã làm được, phần còn vướng và thay đổi đề xuất có tham chiếu dữ liệu.

### 13.2. Ràng buộc

AI không tự sửa điểm, mastery, quyền, tiêu chí, deadline hoặc phát nội dung ngoài scope. AI không tự xuất bản câu/bài học và không nói kết quả “đã được xác nhận” khi đang chờ review.

Candidate selection của AI, nếu bật, phải trả schema gồm ID thuộc allowlist, reason codes và evidence IDs hợp lệ. Backend xác thực lại điều kiện ngay trước khi phát; output lỗi dùng lựa chọn deterministic.

Ở hoạt động học có hướng dẫn, AI có thể giải thích bước hoặc minh họa theo nội dung được phép. Ở IndependentCheck, server từ chối endpoint trợ giúp/lời giải liên quan trong lúc kiểm tra; frontend chỉ ẩn nút là chưa đủ. Những nội dung đã lộ được ghi nhận và làm mất tư cách evidence độc lập theo policy.

Không thể bảo đảm học sinh không dùng trợ giúp bên ngoài trình duyệt. Sản phẩm chỉ tuyên bố kiểm soát trợ giúp trong hệ thống; không quảng cáo giám sát thi hoặc chống gian lận tuyệt đối.

### 13.3. Fallback

Không có AI: vẫn xem/chọn lộ trình, học nội dung Published, dùng phản hồi mẫu, nộp và lưu bài, resume, nhận đề xuất bằng luật. Bài cần reasoning mà fallback không đủ thẩm quyền phải chờ giáo viên; AI tắt không đồng nghĩa được bỏ tiêu chí.

Response phải có `PlanningSource`, `AssistanceSource`, trạng thái AI và policy version phù hợp. UI chỉ hiện thông tin có ích, ví dụ “Đề xuất theo kết quả học và giáo trình”; không gắn nhãn “AI cá nhân hóa” cho fallback thuần luật.

## 14. Đánh giá thực tế: hoạt động, kỹ năng và mục tiêu

### 14.1. Chính sách theo Skill

Mỗi Skill có evaluation policy được giáo viên phê duyệt: loại bài, độ bao phủ, số câu/tác vụ tối thiểu, mức đúng, rubric reasoning, trust cho phép, mức trợ giúp, giới hạn lặp, độ mới và quy tắc kiểm tra lại.

**Cấu hình khởi đầu để pilot cho kỹ năng tính toán hẹp:** tối thiểu 5 câu độc lập khác nhau, đúng ít nhất 4/5, có ít nhất hai biến thể được tác giả gắn nhãn nếu Skill cần nhiều dạng. Đây là cấu hình thử nghiệm, không phải chứng minh khoa học rằng 80% tương đương mastery.

Đối với mô hình hóa/lập luận: dùng rubric và nhiệm vụ đại diện; không ép thành 5 câu trắc nghiệm. Muốn dùng cấu hình khác phải phát hành policy mới có lý do chuyên môn, không âm thầm hạ số câu khi ngân hàng thiếu.

### 14.2. Điều kiện tính bằng chứng độc lập

- Đúng Center, Student, Path, MissionRun, ActivityRun và Question/version đã được server phát.
- Đã nộp thành công, không Skipped, chấm xong và có evidence đủ điều kiện.
- Không nhận gợi ý/lời giải trước lúc nộp theo chính sách đã chốt.
- Không dùng lại câu vừa luyện hoặc câu đã lộ đáp án như bằng chứng độc lập mới.
- Không đếm nhiều Attempt cùng câu thành nhiều câu khác nhau; xử lý equivalence group nếu tác giả xác định là cùng bài biến thể gần như trùng.
- Không còn teacher review ảnh hưởng quyết định đạt; thiếu đánh giá không bị tự coi là sai.
- Evidence chỉ đóng góp một lần vào Twin, dù bài xuất hiện ở assignment, path hay báo cáo.

Nếu học sinh yêu cầu lời giải khi đang kiểm tra: hỏi chọn kết thúc phần kiểm tra và chuyển sang học có hướng dẫn; lưu quyết định. Không âm thầm vừa hiện lời giải vừa giữ nhãn độc lập.

### 14.3. Outcome

| Outcome | Ý nghĩa | Hành động tiếp theo |
|---|---|---|
| Passed | Đạt policy của nhiệm vụ kiểm chứng | Cập nhật projection sau pipeline, mở phần đủ điều kiện |
| NeedsSupport | Đủ dữ liệu cho thấy chưa đạt mục tiêu hiện tại | Đề xuất hoạt động theo lỗi hoặc nền tảng liên quan |
| PendingGrading | Bài đã lưu, đang chấm | Theo dõi trạng thái; có thể học nhánh khác hợp lệ |
| PendingTeacherReview | Bằng chứng cần giáo viên quyết định | Chặn xác nhận phần liên quan, cho tiếp tục phần không phụ thuộc |
| InsufficientEvidence | Chưa đủ dữ liệu hợp lệ | Tiếp tục kiểm tra nếu còn câu, hoặc báo thiếu nội dung |
| ActivityOnlyCompleted | Hoàn thành nhiệm vụ học/khám phá/tự nhìn lại | Không tuyên bố đã xác nhận Skill |

Giữ khả năng ánh xạ `NotPassed` cũ sang `NeedsSupport` ở DTO mới, không sửa lịch sử Attempt. Đổi tên UI không được che đi kết quả thực tế.

Chỉ một evaluator có quyền đóng outcome đánh giá. Submit lưu bài và tiến vị trí; không tự Passed theo tổng counter. Teacher review và async worker gọi cùng evaluator idempotent, tôn trọng thứ tự phiên bản quyết định.

### 14.4. Ôn và duy trì

Pilot có thể gợi ý kiểm tra nhớ lại sau khoảng 3 và 7 ngày cho kỹ năng mới xác nhận, tùy lịch học. Đây là nhịp thử nghiệm, không tuyên bố tối ưu cho mọi học sinh. Đến hạn chỉ đưa vào hàng đợi; không tự trừ mastery.

Nếu kiểm tra nhớ lại không đạt, đưa hoạt động ngắn về đúng phần vướng rồi kiểm tra lại bằng câu hợp lệ. Không bắt làm lại toàn chương vì một lỗi.

## 15. Trạng thái và tính liên tục

### 15.1. Đề xuất và Path

`PathProposal`: `Prepared → Accepted | Dismissed | Expired`. Thay preference làm proposal cũ stale; xác nhận kiểm tra lại quyền và phiên bản evidence/content.

`LearningPath`: giữ được Active/Completed/Superseded hiện có. Thiết kế mới quy định một tuyến Active chính cho `(CenterId, StudentId, SubjectId)`; mục tiêu phụ là nhánh trong tuyến. Class/curriculum vẫn là ràng buộc từng phần. Không gộp quyền của hai lớp để học nội dung chưa được phép.

Thay mục tiêu tạo revision mới cho tương lai sau preview/accept; Run đang mở gắn revision cũ và được giữ nếu còn quyền/nội dung. Nếu không thể tiếp tục, chuyển trạng thái có lý do và phương án phục hồi; không xóa.

### 15.2. Mission và Run

| Đối tượng | Trạng thái đề xuất |
|---|---|
| Mission kế hoạch | Planned, Available, InProgress, Completed, NeedsSupport, PendingTeacherReview, Superseded |
| Run thực thi | InProgress, Paused, AwaitingGrading, AwaitingTeacherReview, Finished, Cancelled |
| ActivityRun | NotStarted, InProgress, Submitted, AwaitingAssessment, Completed, Invalidated |

Outcome tách khỏi Run status. `Finished + NeedsSupport` hợp lệ. `Finished + ActivityOnlyCompleted` hợp lệ. Run thiếu câu chưa bị đóng giả; nếu thực sự không thể tiếp tục thì kết thúc với `InsufficientEvidence` và reason code, có đường tạo nhiệm vụ thay thế an toàn.

`Paused` là người học chủ động chuyển nhiệm vụ/nghỉ; rời trang đơn thuần không hủy Run. Khi bắt đầu lại, trả đúng Run và hoạt động hiện tại. Run chờ giáo viên vẫn chiếm chốt ngăn mở lần đánh giá mới cùng mục tiêu để né duyệt, nhưng không khóa toàn bộ môn.

### 15.3. Lưu và resume

Lưu server-side: content/policy version, activity order, câu đã phát, câu đã nộp, hint/solution exposure, bản nháp có version và vị trí hiện tại. Local storage chỉ hỗ trợ phản hồi nhanh, không là dữ liệu chuẩn.

Mất mạng: cho giữ bản nháp cục bộ và trạng thái chưa đồng bộ; chỉ báo “đã lưu trên server” khi có xác nhận. Retry giữ idempotency identity cho cùng payload. Hai thiết bị sửa draft dùng revision, trả xung đột và lựa chọn phục hồi, không im lặng ghi đè.

Một lần phát câu được ghi nhận trước khi trả response. Vị trí chỉ tiến sau persisted submission; không coi bấm “tiếp theo” là đã hoàn thành. API start/submit/evaluate phải chịu retry, hai tab và request đến không đúng thứ tự.

## 16. Dữ liệu và kiến trúc triển khai đề xuất

Không khóa trước số bảng. Mục tiêu là tránh hai nguồn trạng thái, nội dung không có version và không thể tái hiện quyết định học. Giữ modular monolith .NET/EF/MySQL, không yêu cầu thêm microservice hoặc broker.

### 16.1. Tận dụng và mở rộng

| Thành phần | Hướng xử lý |
|---|---|
| Auth/tenant/permission/curriculum scope | Giữ; áp ở mọi luồng mới |
| Knowledge Graph | Giữ, bổ sung mức Skill và quan hệ đúng chuyên môn khi cần |
| Question/Attempt/AI jobs/Evidence Gate/Teacher Review/Twin history | Giữ nguồn chuẩn; bổ sung ngữ cảnh ActivityRun và trợ giúp |
| LearningPath | Giữ định danh và lịch sử, bổ sung Goal/Revision/PlanningSource |
| LearningPathSession | Chuyển dần sang Mission, hỗ trợ nhiều Skill qua metadata/quan hệ đã chuẩn hóa |
| LearningPathSessionRun | Tận dụng Run; thay thiết kế một checkpoint đơn lẻ bằng ActivityRun/issued items |
| Preference hiện có | Di chuyển sang lịch rảnh, mục tiêu, sở thích; không dùng self-assessment làm mastery |
| PlanJson | Chỉ snapshot/projection hoặc đọc legacy, không cùng ghi trạng thái với bảng thực thi |

### 16.2. Các khái niệm lưu trữ cần có

Tên bảng là đề xuất để triển khai đối chiếu schema thật; có thể gộp hợp lý nhưng không bỏ các bất biến.

| Entity | Trường/quan hệ chính | Vai trò |
|---|---|---|
| LearningGoal | Owner, Subject, Scope, TargetSkills, GoalType, Deadline?, Availability, Status | Đích đến có cấu trúc |
| LearningContentVersion | Tenant, SkillIds, ActivityType, Payload, Rubric/QuestionRefs, Prerequisites, Duration, Status, Version | Nội dung được duyệt và bất biến sau publish |
| PathProposal | Owner, Goal, InputRevision, CandidatePlan, Content/PolicyVersions, ExpiresAt, Status | Preview chưa thay Path |
| LearningPathRevision | PathId, Revision, GoalSnapshot, SkillRoute, Milestones, Reasons, CreatedAt | Kế hoạch có lịch sử, không ghi lại cả Path mỗi câu |
| Mission/Session | PathRevision, TargetSkills, Purpose, ActivityPlan, Reason, Availability, Status | Nhiệm vụ cá nhân, ngắn hạn |
| MissionRun/SessionRun | Owner, Mission, Status, Outcome, CurrentActivity, PolicySnapshot, Version | Một lần học bền vững |
| ActivityRun | RunId, Order, ContentVersionId, Mode, Status, DraftRevision, AssistanceState, Result | Tương tác, bản nháp và resume |
| IssuedItem | ActivityRunId, Question/version, Order, ExposureMode, IssuedAt, SubmissionLink | Chốt đề đã phát và thứ tự, có thể dùng ActivityRun một-câu nếu cùng bất biến |
| PathDecision | PathRevision/Run, Trigger, EvidenceRefs, ReasonCodes, Before/AfterRefs, PolicyVersion | Giải thích thay đổi và chống áp lại quyết định |

Skill projection và review queue có thể là read model tính từ dữ liệu chuẩn trước khi cần bảng cache. Không lập bảng mastery mới độc lập. Bản nháp và interaction state được quản lý theo dữ liệu cần resume, không lưu mọi phím bấm.

`Attempt.ActivityRunId` hoặc liên kết tương đương được gắn tại lúc INSERT. Giữ `Attempt.SessionRunId` hiện có cho tương thích; nếu giữ cả hai phải bảo đảm ActivityRun thuộc đúng Run bằng validation và ràng buộc quan hệ phù hợp. Không nối Attempt lịch sử bằng suy đoán CreatedAt.

### 16.3. Bất biến database

- Tất cả quan hệ học thuật ràng buộc Center; owner đối chiếu xuyên Goal → Path → Mission → Run → Activity.
- Chốt một tuyến Active theo scope đã chọn bằng cơ chế DB/giao dịch, không chỉ Any rồi Insert.
- Chốt một Run tương tác đang mở mỗi học sinh/môn; chốt riêng lần đánh giá đang chờ cho cùng mục tiêu. Thiết kế khóa cần bao gồm Paused/Waiting theo invariant tương ứng, không một ActiveKey chung cho mọi ý nghĩa.
- Unique idempotency cho start/submit và mỗi lần tạo evidence; optimistic concurrency với version được tăng thực sự.
- Nội dung/policy đã bắt đầu có snapshot/version bất biến; thu hồi quyền và nội dung vẫn được revalidate.
- Thay checkpoint không sửa âm thầm mẫu số; tạo quyết định thay thế có version và thông báo trước khi tiếp tục.
- Không dùng SQL Server rowversion cho MySQL; theo mapping và transaction hiện có.
- Migration chỉ additive trong giai đoạn chuyển đổi; rollback ứng dụng không được làm mất dữ liệu mới.

## 17. API theo hành vi

Các route là thiết kế mục tiêu, chưa tuyên bố tồn tại. Khi triển khai phải đồng bộ API_CONTRACTS.md và không tạo hai bộ endpoint cùng có quyền quyết định outcome.

| Hành vi | API đề xuất | Trách nhiệm server |
|---|---|---|
| Đọc/lưu mục tiêu | GET/PUT `/students/me/learning-goals/{id}` | Scope, ownership, version, lịch rảnh hợp lệ |
| Tạo đề xuất | POST `/students/me/path-proposals` | Snapshot đầu vào, khả thi, candidate và lý do |
| Xác nhận | POST `/students/me/path-proposals/{id}/accept` | Revalidate stale/quyền, activate atomic, idempotent |
| Bản đồ/tuyến | GET `/students/me/learning-paths/{id}/map` | Projection có căn cứ, không backfill dữ liệu trong GET |
| Nhiệm vụ hôm nay | GET `/students/me/learning-agenda?availableMinutes=15` | Đề xuất đọc; không tự tạo Run hay đổi Path trong GET |
| Start/resume | POST `/students/me/missions/{id}/start` | Tạo/trả Run, chốt concurrency và đúng nội dung |
| Đọc Run | GET `/students/me/mission-runs/{id}` | Vị trí, draft, issued items, quyền hiện tại |
| Lưu draft | PUT `/students/me/activity-runs/{id}/draft` | ExpectedRevision, không đổi điểm |
| Yêu cầu trợ giúp | POST `/students/me/activity-runs/{id}/assistance` | Kiểm tra mode, exposure, nội dung an toàn |
| Nộp hoạt động | POST `/students/me/activity-runs/{id}/submissions` | Issued identity, idempotency, Attempt/evidence đúng một lần |
| Đánh giá | POST `/students/me/mission-runs/{id}/evaluate` | Một evaluator, không nhận Passed từ client |
| Phản hồi nhu cầu | POST `/students/me/mission-runs/{id}/feedback` | Tự báo cáo; không tự đổi mastery |
| Xem/chấp nhận thay đổi lớn | GET/POST `/students/me/learning-paths/{id}/change-proposals/{changeId}` | Preview ảnh hưởng, bảo toàn Run và lịch sử |
| Biên soạn/xuất bản | API học thuật Teacher theo permission hiện có | Validate schema, version, coverage và publisher |

Mọi response kết quả cần chỉ rõ `ActivityProgress`, `RunStatus`, `Outcome`, `SkillEvidenceStatus`, `NextActions`, `DecisionReasons`, `PolicyVersion`, `RowVersion`, và nguồn AI khi liên quan. Không trả `MasteryUpdated=true` nếu chưa có committed evidence/history tương ứng.

HTTP: 403/404 cho ngoài quyền theo quy ước chống lộ ID; 409 stale/xung đột/trạng thái/idempotency khác payload; 422 thiếu điều kiện nội dung/policy. Không ép mọi thiếu câu thành lỗi học sinh làm sai.

Async grading trả trạng thái bền vững và ID có thể theo dõi. Refresh/mất response vẫn đọc được kết quả đã lưu. Endpoint trợ giúp, hình ảnh, lời giải và draft cũng phải kiểm tra owner/scope như endpoint nộp bài.

## 18. Màn hình cần xây

| Màn hình | Nội dung và hành động chính | Điều kiện hoàn thành thiết kế |
|---|---|---|
| Thiết lập mục tiêu | Mục tiêu, phạm vi, thời gian, tự báo cáo tùy chọn | Học sinh hoàn thành mà không cần hiểu mastery/graph |
| Kiểm tra định hướng | Giải thích mục đích, câu ngắn, chưa biết, tạm dừng | Không biến thành bài thi dài trước mọi lần học |
| Preview tuyến | Điểm xuất phát, milestone, khoảng thời gian, phần chưa rõ | Xem và sửa trước khi kích hoạt |
| Bản đồ cá nhân | Node trạng thái, tuyến chính, nhánh hợp lệ, đầu ra | Có danh sách tương đương trên mobile/bàn phím |
| Hôm nay | Thời gian hiện có, một đề xuất chính, lựa chọn, bài bắt buộc | Một hành động chính rõ ràng, không dồn nhiều card cùng mức |
| Không gian Mission | Mục đích, tiến trình hoạt động, renderer, trợ giúp theo mode, lưu | Có trải nghiệm thật cho từng loại hoạt động |
| Kết quả | Đã làm gì, đã chứng minh gì, còn chưa rõ gì, bước tiếp | Không đánh đồng học xong với nắm vững |
| Sổ tay học tập | Lỗi đã sửa, ví dụ đánh dấu, ghi chú, khả năng đã chứng minh | Có nguồn và cho quay lại hoạt động liên quan |
| Nhìn lại tuần | Tiến bộ theo Skill, thời gian ước lượng, điều chỉnh đề xuất | Không dùng tổng số click làm thành tích học |
| Giáo viên: nội dung | Soạn/preview/publish, coverage, lỗi nội dung | Giáo viên tạo được gói học mà không sửa code |
| Giáo viên: hỗ trợ | Học sinh mắc kẹt, evidence, thiếu nội dung, review | Hành động đúng học sinh/lớp và có audit |

Nút bắt đầu tuyến dẫn tới nhiệm vụ phù hợp hoặc tiếp tục nhiệm vụ đang dở, không mặc định nhảy vào câu luyện chung. Trang Mission có ngữ cảnh mục tiêu và tuyến; luyện tập tự do vẫn là công cụ riêng để học sinh tự luyện ngoài tuyến.

Chỉ thông báo “đã làm được X” khi có tiêu chí và bằng chứng tương ứng. Nếu chỉ hoàn thành bài học: “Em đã học về X; hãy thử tự làm để kiểm tra.”

## 19. Chuyển đổi từ hệ thống hiện tại

### 19.1. Những phần nên giữ

Giữ authentication, permission, tenant isolation, lớp/giáo trình, Question Bank, nhập toán, scratchpad, Attempt append-only, AI grading jobs, Evidence Gate, Teacher Review và Twin history. Có thể tái sử dụng renderer làm bài ở Activity phù hợp.

### 19.2. Những phần cần thay đổi căn bản

| Hiện trạng đã quan sát | Quyết định V4 |
|---|---|
| Lịch nhiều tuần suy từ số ngày/nhịp và xoay câu hỏi | Tuyến theo Goal/Skill; chi tiết nhiệm vụ ngắn hạn theo bằng chứng |
| Mọi loại phiên chủ yếu dùng cùng bộ câu | Activity có loại, nội dung và trải nghiệm đúng mục đích |
| Một CheckpointQuestionId của Run | Issued activities/items hỗ trợ nhiều checkpoint và rubric |
| Submit tự đóng Passed bằng counter tổng | Submit chỉ lưu/tiến; evaluator duy nhất quyết định |
| Evaluate cũ tìm Attempt theo thời gian | FK Run/Activity tường minh; legacy chỉ đọc lịch sử theo nhãn cũ |
| PlanJson và Session cùng ghi trạng thái | Bảng Run/Activity/Session là nguồn thực thi; JSON là snapshot/projection |
| Không có Twin thì hiển thị 0% | Unknown và độ bao phủ riêng, không giả năng lực thấp |
| Tạo Path mới sau nhiều trigger Attempt | Trigger cập nhật projection/đề xuất; revision chỉ khi thực sự cần |
| Start resolve Session chưa kiểm tra đủ owner/scope | Đối chiếu toàn chuỗi sở hữu và quyền trước mọi phát nội dung |
| UI mặc định báo Twin cập nhật/mở khóa | Hiện đúng trạng thái đã commit và NextActions backend trả |

Các nhận xét hiện trạng dựa trên lần đọc mã ngày 16/10/2026, bao gồm thay đổi chưa commit lúc rà soát; cần kiểm tra lại branch thực tế khi triển khai. Không coi danh sách này là kết quả E2E hoặc kiểm kê database đang chạy.

### 19.3. Lộ trình chuyển đổi dữ liệu và route

1. Chụp baseline schema, routes, test và kiểm kê dữ liệu; không đưa secret vào báo cáo.
2. Thêm schema mới và adapter đọc path cũ, dùng `EngineVersion` phân biệt Legacy/V4.
3. Path legacy được xem và tiếp tục theo khả năng đã kiểm chứng; không tuyên bố đã là V4. Cho đề xuất chuyển tuyến tương lai sau preview.
4. Không tự gắn lại Attempt cũ vào ActivityRun hoặc suy diễn hoàn thành mới bằng thời gian. Chỉ dùng evidence lịch sử hợp lệ cho đánh giá điểm xuất phát.
5. Run đang mở giữ engine/version cũ tới khi kết thúc hoặc người học chấp nhận kết thúc an toàn; tuyến V4 bắt đầu ở ranh giới rõ ràng.
6. Bật V4 cho chương pilot/nhóm được chọn; route cũ không được sửa trạng thái Run V4.
7. Khi đã đối chiếu dữ liệu và pass các gate, ngừng đường ghi legacy cho tuyến mới. Chỉ cân nhắc dọn schema ở công việc riêng sau backup/rollback đã kiểm chứng.

Được phép thay UI và service điều phối cũ. “Đập đi sửa lại” không có nghĩa xóa bài làm, evidence, quyền, lớp hoặc lịch sử đã có.

## 20. Phạm vi phát hành theo chiều sâu

### 20.1. Bản đầu tiên phải có

- Một chương môn Toán có khoảng 6–8 Skill được giáo viên xác định, ít nhất hai nhánh xuất phát khác nhau và một thử thách vận dụng.
- Goal, điểm bắt đầu có bằng chứng/Unknown, preview và bản đồ cá nhân.
- Agenda theo thời gian, tiếp tục Run, lựa chọn hoạt động hợp lệ.
- Ít nhất ba trải nghiệm hoạt động khác nhau: WorkedExample/CompleteSolution, FindError và IndependentCheck; thêm một Explore template ở nơi có ích và một TransferChallenge cuối chặng.
- Điều chỉnh theo ít nhất ba tình huống thật: đã biết để đi nhanh, sai có căn cứ để bổ trợ, thiếu thời gian để đổi khối lượng.
- Teacher authoring/publish tối thiểu bằng form/schema có preview, không hardcode toàn nội dung trong component.
- Lưu/resume đa thiết bị, pending grading/review, scope và evaluator đúng.
- Sổ tay cơ bản từ hoạt động đã làm và lịch ôn thử nghiệm.
- AI giải thích/hỗ trợ có ngữ cảnh và fallback; không bắt buộc AI chọn tuyến.

### 20.2. Sau khi bản đầu hữu ích

Mở nhiều chương/môn, thêm renderer, tinh chỉnh ước lượng, tối ưu lịch ôn bằng dữ liệu, authoring thuận tiện hơn và phân tích mức hiệu quả của đề xuất. Không lấy số chương sinh tự động làm tiêu chí hoàn thành.

### 20.3. Chưa làm trong đợt đầu

Sinh tự do mọi bài học lúc học sinh mở trang; mô hình IRT/BKT/DKT tự huấn luyện khi chưa có dữ liệu; dự đoán chính xác điểm thi; giám sát thi; xếp hạng cạnh tranh; mô phỏng mọi chủ đề; chatbot mở vô hạn; tự động xuất bản nội dung AI; tái kiến trúc microservice.

Các mục này không cần để học sinh có một tuyến riêng thực tế. Phần khó và quan trọng của đợt đầu là chất lượng Skill, nội dung, policy và điều phối.

## 21. Thứ tự triển khai và gate

| Giai đoạn | Công việc | Gate đầu ra |
|---|---|---|
| 0. Chốt chương pilot | Giáo viên chọn Skill, prerequisite, lỗi thường gặp, đầu ra và policy; học sinh góp ý mục tiêu/ngôn ngữ | Gói nội dung và ba hồ sơ mẫu có thể walkthrough trên giấy |
| 1. Prototype trải nghiệm | Goal → preview → map → ba loại activity → kết quả; thử trên mobile | Học sinh hiểu bước tiếp theo và lý do; ghi nhận phản hồi thật |
| 2. Củng cố execution | Owner/scope, Run/Activity/FK, idempotency, evaluator duy nhất, legacy boundary | Không mất/trùng bài, không giả Passed, không truy cập chéo |
| 3. Nội dung và planner | Authoring/publish/version, skill projection, tuyến Goal, candidate rules, feasibility | Ba hồ sơ cùng Goal sinh tuyến/nhiệm vụ khác đúng kỳ vọng |
| 4. Mission player | Renderer, trợ giúp theo mode, draft/resume, result/next action | Một luồng học thật từ đầu đến cuối trên DB |
| 5. Adaptation | Bỏ qua có kiểm chứng, bổ trợ theo lỗi, thời gian, ôn và revision | Thay đổi có lý do, không phá Run đang làm |
| 6. AI và teacher integration | Context tutor, fallback, review, nội dung thiếu | AI lỗi vẫn học được; review không bị vượt qua |
| 7. Pilot và sửa | Quan sát học sinh/giáo viên, đo kết quả và sửa nội dung/luật | Báo cáo lợi ích/giới hạn có dữ liệu, không chỉ screenshot |

Một gate chưa đạt thì không đánh dấu phần sau hoàn thành bằng mock. Có thể phát triển kỹ thuật độc lập nhưng không mở rộng nội dung trước khi luồng một chương đã hoạt động và được dùng thử.

## 22. Acceptance Criteria

| ID | Tình huống | Kết quả bắt buộc |
|---|---|---|
| V4-01 | Hai học sinh cùng Goal nhưng khác bằng chứng | Khác điểm xuất phát/nhiệm vụ/hỗ trợ theo fixture, có lý do cụ thể |
| V4-02 | Học sinh mới chưa có evidence | Unknown, tuyến tạm thời hoặc probe; không hiển thị mastery 0 như kết quả đã đo |
| V4-03 | Tự nhận giỏi và yêu cầu bỏ phần | Đề xuất kiểm tra nhanh, không tự Confirmed |
| V4-04 | Đã đủ bằng chứng nền tảng | Bỏ phần lặp không cần thiết, giữ khả năng ôn |
| V4-05 | Deadline và thời gian không đủ | Hiện thiếu ngân sách và lựa chọn thay đổi; không hạ tiêu chí âm thầm |
| V4-06 | Đổi ngân sách 30 xuống 5 phút | Đổi nhiệm vụ/khối lượng hợp lệ, không chỉ thay nhãn phút |
| V4-07 | Bỏ ba ngày học | Điều chỉnh dự báo, không dồn gấp ba bài vào ngày sau |
| V4-08 | Khám phá/WorkedExample | Có tương tác và phản hồi đúng loại, không chỉ trang câu hỏi đổi tiêu đề |
| V4-09 | Tìm lỗi trong lời giải | Chọn/sửa lỗi, phản hồi đúng misconception/rubric |
| V4-10 | Tự nhận quá dễ/quá khó | Đề xuất hợp lệ, không ghi mastery từ tự báo cáo |
| V4-11 | Sai lặp có đủ căn cứ | Bổ trợ đúng lỗi, không bắt học lại cả chương |
| V4-12 | AI gán lỗi nhưng chưa đủ bằng chứng | Hiện observation/đề nghị probe, không khẳng định chắc chắn |
| V4-13 | Câu vừa luyện hoặc đã lộ lời giải | Không tính làm bằng chứng độc lập mới |
| V4-14 | Checkpoint chưa đủ số/coverage | InsufficientEvidence, không Passed |
| V4-15 | Luyện đúng nhiều nhưng kiểm tra độc lập không đạt | NeedsSupport, không Passed theo tổng câu đúng |
| V4-16 | Pending grading hoặc teacher review | Không Confirmed/mở phụ thuộc trước quyết định hợp lệ |
| V4-17 | Giáo viên duyệt/sửa sau đó | Evaluate lại cùng policy, không nhân đôi evidence/Twin |
| V4-18 | TransferChallenge đạt tổng điểm nhưng một Skill thiếu rubric | Chỉ xác nhận Skill đủ evidence, không chia điểm tổng đều |
| V4-19 | Đến hạn ôn nhưng chưa làm | ReviewDue, không tự giảm mastery |
| V4-20 | Đổi mục tiêu khi đang học | Preview ảnh hưởng và giữ Run/version đang mở nếu còn hợp lệ |
| V4-21 | Hai tab start cùng lúc | Cùng một Run hợp lệ; relational test dùng DbContext/connection độc lập |
| V4-22 | Submit timeout rồi retry | Một Attempt, một kết quả evidence; khác payload cùng key bị 409 |
| V4-23 | Đóng trình duyệt, mở thiết bị khác | Đúng activity, draft đã sync, câu đã nộp và vị trí |
| V4-24 | Hai thiết bị sửa draft | Xung đột có thể phục hồi, không im lặng mất nội dung |
| V4-25 | Sửa Student/Center/Path/Run/Activity ID | 403/404 và không lộ nội dung/draft/evidence |
| V4-26 | Scope lớp bị thu hồi khi Run mở | Kiểm tra lại quyền; không tiếp tục phát/nộp ngoài scope |
| V4-27 | Nội dung bị retire/thu hồi vì sai | Dừng/thay thế có giải thích, không dùng bản sai hay tự tính câu sai |
| V4-28 | Gọi trợ giúp/lời giải bằng API ở IndependentCheck | Server chặn hoặc chuyển mode rõ ràng, không chỉ ẩn UI |
| V4-29 | AI timeout/output sai ID | Fallback hợp lệ, thông báo nguồn đúng, không mất tiến độ |
| V4-30 | Thiếu checkpoint mới khi retry | Không tái dùng câu lộ đáp án như mới; báo thiếu nội dung và phương án |
| V4-31 | GET agenda/map/path | Không tự tạo Run, activate Path hoặc ghi PlanJson |
| V4-32 | Outcome Passed nhưng pipeline Twin chưa commit | UI không báo “Twin đã cập nhật” hoặc delta giả |
| V4-33 | Assignment và Path cùng dùng một Attempt | Một identity evidence và một lần cập nhật |
| V4-34 | Worker/legacy callback đến sau | Không regenerate Path hoặc ghi đè outcome V4 ngoài luật |
| V4-35 | Hoàn thành các hoạt động nhưng chưa đủ đầu ra Goal | Chưa GoalAchieved, chỉ rõ điều kiện còn thiếu |
| V4-36 | Teacher sửa nội dung Published | Tạo version mới, Run cũ có lịch sử và chính sách xử lý thu hồi |
| V4-37 | Dùng mobile, bàn phím, không kéo thả | Hoàn thành được hoạt động cốt lõi và hiểu trạng thái |
| V4-38 | Xem lịch sử Legacy sau bật V4 | Giữ bài làm/evidence, có nhãn engine, không giả lịch sử Activity mới |

### 22.1. Các lớp kiểm thử

- Unit: luật eligibility, Unknown, prerequisite, feasibility, policy, state transition, projection và reason code.
- Relational MySQL: unique/lock, concurrent start/submit/evaluate, transaction rollback, FK tenant/owner, migration và dữ liệu cũ. EF InMemory không đủ chứng minh concurrency.
- Integration: submit → async grading → gate → Twin → evaluator → next action; cả teacher override tới muộn.
- Frontend: renderer thật, accessibility, pending/error/fallback, draft conflict và không hiển thị khẳng định sai.
- E2E: ba hồ sơ, cùng mục tiêu, ít nhất hai thiết bị/browser context; goal → preview → start → activity → reload → check → adaptation → map.
- Content QA: kiểm tra công thức, rubric, phản hồi lỗi, bản quyền/nguồn, equivalence và coverage trước publish.

Không coi test đọc chuỗi source, screenshot hoặc dữ liệu seed tự hoàn thành là thay thế cho kiểm thử hành vi end-to-end.

### 22.2. Fixture đối chiếu cá nhân hóa từ đầu đến cuối

**Cùng mục tiêu:** giải bài toán thực tế bằng phương trình. **Cùng gói nội dung:** phân số liên quan, biến đổi biểu thức, giải phương trình, lập phương trình và so sánh phương án chi phí.

| Mốc | An | Bình | Chi |
|---|---|---|---|
| Hồ sơ đầu vào | Có bằng chứng mới về lỗi phân số; phương trình cơ bản Developing | Cơ bản Confirmed; mô hình hóa Unknown | Không có evidence; tự nhận khá |
| Preview | Nêu một phần bổ trợ phân số trước khi vào phương trình có phân số | Bỏ luyện cơ bản, đề xuất xác định khả năng mô hình hóa | Đánh dấu tuyến tạm thời và đề nghị probe mục tiêu |
| Nhiệm vụ đầu | FindError đúng lỗi phân số đã xác nhận | Probe mô hình hóa bằng nhiệm vụ chưa từng lộ | Probe ngắn hoặc hoạt động khởi đầu nếu từ chối probe |
| Sau kết quả | Nếu còn lỗi thì WorkedExample và luyện đúng kỹ năng | Nếu thiếu cách chọn ẩn thì hướng dẫn mô hình hóa, không học lại phép tính cơ bản | Chỉ cập nhật phần đã đo, giữ Unknown ở kỹ năng chưa bao phủ |
| Chọn 5 phút | Sửa một lỗi cụ thể hoặc tiếp tục bước dở | Phân tích một tình huống chọn ẩn ngắn | Một cụm probe ngắn, lưu phần còn lại |
| Thử thách cuối | Được hỗ trợ nền tảng trước rồi chứng minh khả năng vận dụng | Có thể đến sớm hơn, nhưng vẫn phải đạt rubric | Thời điểm phụ thuộc evidence thu được, không suy từ tự nhận |

Fixture phải khóa content/policy/evidence để so sánh được. Nếu xóa tên học sinh khỏi màn hình, giáo viên vẫn phải chỉ ra được vì sao ba tuyến khác nhau. Thứ tự chính xác của các hoạt động sau đó phụ thuộc bài làm thật, không hardcode một câu chuyện thành công cho từng tên.

## 23. Kiểm chứng “có giúp học sinh không?”

### 23.1. Pilot đề xuất

Mời khoảng 5–10 học sinh có điểm xuất phát khác nhau và 1–2 giáo viên dùng một chương trong khoảng hai tuần; đây là nghiên cứu sử dụng nhỏ, không đủ kết luận hiệu quả khoa học cho toàn sản phẩm. Lấy đồng ý và xử lý dữ liệu theo quy định nhà trường/trung tâm; không xuất thông tin định danh vào báo cáo công khai.

Trước pilot: giáo viên duyệt nội dung, có bài đánh giá đầu/cuối tương đương về phạm vi và độ khó nhưng tránh lặp nguyên câu, có nhiệm vụ vận dụng và kiểm tra nhớ lại. Ghi thời gian hỗ trợ từ giáo viên và các nội dung còn thiếu.

### 23.2. Chỉ số có ý nghĩa

| Nhóm | Câu hỏi đo | Cách đọc kết quả |
|---|---|---|
| Định hướng | Học sinh có biết học gì tiếp và vì sao? | Quan sát nhiệm vụ và phỏng vấn ngắn, không chỉ đo click |
| Cá nhân hóa | Tuyến có khác đúng theo nhu cầu? | Đối chiếu hồ sơ, quyết định và đánh giá của giáo viên |
| Học được | Tự giải được bài mới cùng kỹ năng không? | Đánh giá đầu/cuối với mức trợ giúp và coverage rõ |
| Vận dụng | Chuyển được kiến thức sang tình huống mới không? | Rubric TransferChallenge |
| Duy trì | Sau vài ngày còn làm được không? | Kiểm tra nhớ lại, phân biệt không làm với làm sai |
| Trải nghiệm | Có bị mắc kẹt, bị lặp quá nhiều hoặc quá tải không? | Điểm bỏ dở, yêu cầu giúp và phỏng vấn |
| Vận hành | Giáo viên mất bao lâu để chuẩn bị và hỗ trợ? | Thời gian authoring/review, câu thiếu/sai, lỗi kỹ thuật |

Trước thu thập dữ liệu cần chốt mục tiêu chấp nhận cụ thể với giáo viên; không chọn ngưỡng sau khi xem kết quả để làm đẹp báo cáo. Completion rate, streak và tổng thời gian không phải bằng chứng độc lập rằng học sinh học tốt hơn.

So sánh trước/sau nhỏ chỉ cho tín hiệu cải thiện; không khẳng định nguyên nhân do AI hoặc Path nếu không có thiết kế đối chứng phù hợp. Nếu học sinh chưa tiến bộ, cần xem lại nội dung, độ phù hợp nhiệm vụ và mức hỗ trợ trước khi thêm tính năng.

## 24. Kịch bản demo thể hiện đúng giá trị

1. Mở cùng mục tiêu “Giải bài toán bằng phương trình” ở hồ sơ An và Bình; hiển thị điểm xuất phát và tuyến khác nhau.
2. An chọn 15 phút, học FindError về dấu; Bình làm kiểm tra vượt phần rồi vào nhiệm vụ mô hình hóa.
3. Đóng trình duyệt của An giữa hoạt động; đăng nhập browser context khác và tiếp tục đúng chỗ.
4. An mắc lỗi phân số có căn cứ; hệ thống đề xuất bổ trợ ngắn, giải thích nguyên nhân và cho lựa chọn phù hợp.
5. Chuyển ngân sách của An xuống 5 phút; nhiệm vụ tương lai thay đổi thật, Run đã làm giữ nguyên.
6. Bình hoàn thành TransferChallenge; hồ sơ ghi khả năng được chứng minh theo rubric, không chỉ “đã làm 10 câu”.
7. Minh họa pending review/AI unavailable và việc học tiếp nhánh hợp lệ.
8. Giáo viên mở nội dung hoặc evidence đứng sau quyết định và kiểm tra được lý do.

Dùng tài khoản thật, quyền thật, nội dung đã duyệt và database thật. Các hồ sơ seed chỉ là dữ liệu thử, không giả làm kết quả nghiên cứu học sinh thực.

## 25. Những quyết định cần xác nhận cùng người dùng thực

Tài liệu đã chọn hướng triển khai; các mục sau là đầu việc thiết kế/pilot, không phải lý do dừng viết tài liệu hoặc tự thu hẹp sản phẩm:

- Chương đầu tiên, danh sách Skill và độ hạt prerequisite do giáo viên xác nhận.
- Đầu ra học sinh thật sự quan tâm và ngôn ngữ các lựa chọn Goal.
- Evaluation policy/rubric cho từng loại kỹ năng, tiêu chí bỏ qua và retry.
- Quyền học prerequisite ngoài phần đang dạy; hiển thị thông báo cho giáo viên khi cần.
- Khối lượng nội dung giáo viên có thể chuẩn bị và người chịu trách nhiệm publish.
- Thời lượng, lịch ôn và mục tiêu chấp nhận trải nghiệm để thử nghiệm.
- Dữ liệu nào được lưu, thời gian lưu draft/assistance và cách xuất báo cáo không định danh.

Khi bắt đầu lập trình: cập nhật requirement/schema/API/UI có liên quan bằng quyết định thay đổi rõ ràng; không để V4 và tài liệu authoritative mô tả hai hành vi mâu thuẫn. Yêu cầu hiện tại là xây đặc tả V4, chưa phải triển khai migration, thay dữ liệu hoặc công bố phần mềm đã hoàn thành.

**Tiêu chí cuối cùng:** một học sinh có thể giải thích “đây là mục tiêu của em; hệ thống chọn điểm bắt đầu này từ kết quả của em; khi em gặp khó, tuyến thay đổi ở đây; hiện em đã tự làm được những việc này”. Giáo viên có thể kiểm tra bằng chứng đứng sau các nhận định đó.
