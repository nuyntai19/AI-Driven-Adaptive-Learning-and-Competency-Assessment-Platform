# EDUTWIN

## ĐẶC TẢ ĐỀ TÀI, CHỨC NĂNG VÀ THIẾT KẾ HỆ THỐNG

### Nền tảng hỗ trợ học tập cá nhân hóa, phân tích năng lực và đề xuất lộ trình học tập dựa trên bằng chứng

**Thông điệp cốt lõi:** EduTwin không chỉ lưu điểm. Hệ thống cố gắng trả lời ba câu hỏi quan trọng cho mỗi học sinh: đang yếu ở đâu, vì sao lại yếu và nên học gì tiếp theo. Trí tuệ nhân tạo chỉ hỗ trợ phân tích; các quy tắc của hệ thống và giáo viên mới là nơi đưa ra quyết định có ảnh hưởng đến hồ sơ học tập.

### Thông tin chung

| Thông tin | Nội dung |
| --- | --- |
| **Nhóm thực hiện** | Tuấn Tài, Thịnh, Khoa, Thành Tài, Sơn |
| **Công nghệ chính** | Visual Studio 2026; .NET 10; ASP.NET Core Web API; Entity Framework Core 10; MySQL 8; React/rngVite |
| **Cơ sở dữ liệu** | 40 bảng |
| **Trạng thái tài liệu** | Bản diễn giải chi tiết, ưu tiên tiếng Việt dễ hiểu |
| **Ngày cập nhật** | 13/09/2026 |

*Bản dùng để đọc, trao đổi, báo cáo và giải thích kiến trúc. Các tài liệu Markdown trong repository vẫn giữ vai trò đặc tả kỹ thuật chi tiết.*

## HƯỚNG DẪN ĐỌC TÀI LIỆU

Tài liệu này được viết lại theo cách gần với tài liệu đặc tả nghiệp vụ: đi từ bài toán, mục tiêu, người dùng và chức năng trước; sau đó mới giải thích quy trình, kiến trúc, cơ sở dữ liệu và các chi tiết kỹ thuật. Vì vậy người đọc không cần hiểu sâu về lập trình vẫn có thể theo dõi.

### Quy ước ngôn ngữ

Thuật ngữ tiếng Anh chỉ được giữ lại khi đó là tên công nghệ, tên bảng/cột hoặc thuật ngữ chuyên môn khó dịch hoàn toàn. Mỗi thuật ngữ quan trọng đều được giải thích bằng tiếng Việt ngay lần đầ u xuất hiện.

### Một số thuật ngữ cần biết trước

| Thuật ngữ | Giải thích dễ hiểu |
| --- | --- |
| **Trung tâm độc lập (Center/tenant)** | Một đơn vị giáo dục sử dụng hệ thống. Dữ liệu của trung tâm này không được lẫn với trung tâm khác. |
| **Loại tài khoản** | Phân loại nghiệp vụ cố định gồm Học sinh, Giáo viên và Quản lý trung tâm. |
| **Vai trò** | Nhóm các quyền chức năng có thể cấu hình trong từng trung tâm, ví dụ Người duyệt nội dung hoặc Điều phối học thuật. |
| **Quyền chức năng** | Một quyền cụ thể như xem học sinh, tạo bài tập, xuất bản bài tập hoặc xem nhật ký phân quyền. |
| **Hồ sơ học tập số** | Hồ sơ mô tả mức độ thành thạo, hành vi học tập, mục tiêu và lịch sử thay đổi của học sinh. |
| **Bằng chứng học tập** | Dữ liệu dùng để đánh giá học sinh: đáp án, thời gian làm bài, mức tự tin, số lần đổi đáp án, phần giải thích cách làm và xác nhận của giáo viên. |
| **Bộ kiểm soát bằng chứng** | Cơ chế xác định bằng chứng nào đủ tin cậy để được dùng khi cập nhật hồ sơ học tập số. |
| **Cơ chế dự phòng** | Cách hệ thống tiếp tục hoạt động khi AI hoặc Internet lỗi; hệ thống không giả vờ rằng AI đã phân tích thành công. |
| **Tính toán lại lịch sử** | Sau khi giáo viên sửa/xác nhận một kết quả, hệ thống tính lại các trạng thái liên quan nhưng vẫn giữ lịch sử cũ để truy vết. |

## MỤC LỤC NỘI DUNG

1. Tổng quan đề tài
2. Bài toán thực tế cần giải quyết
3. Mục tiêu của hệ thống
4. Đối tượng sử dụng
5. Đặc tả chức năng cốt lõi
6. Chức năng nâng cao và điểm sáng tạo
7. Quy trình nghiệp vụ chính
8. Vai trò của AI và Hồ sơ học tập số
9. Phân quyền động trong từng trung tâm
10. Kiến trúc và công nghệ
11. Cơ sở dữ liệu
12. Bảo mật và các yêu cầu phi chức năng
13. Trạng thái hiện tại và phần còn phải hoàn thiện
14. Lộ trình hoàn thiện hệ thống
15. Phân chia công việc nhóm 5 người
16. Kiểm thử và tiêu chí chấp nhận
17. Đối chiếu yêu cầu môn học
18. Phụ lục A. Danh sách 40 bảng dữ liệu
19. Phụ lục B. Các tình huống minh họa dễ hiểu
20. Phụ lục C. Nguồn tài liệu kỹ thuật

## 1. TỔNG QUAN ĐỀ TÀI

### 1.1. Tên đề tài

**EduTwin** - Nền tảng học tập thích ứng, đánh giá năng lực và xây dựng Hồ sơ học tập số cho học sinh.

### 1.2. Ý tưởng chính

EduTwin được xây dựng cho các trung tâm giáo dục THPT. Điểm khác biệt của hệ thống là không dừng ở việc lưu điểm hoặc cho giáo viên biết học sinh làm đúng bao nhiêu câu. Hệ thống cố gắng theo dõi quá trình học theo từng chủ đề kiến thức, kết hợp kết quả làm bài với hành vi học tập và phần trình bày cách suy luận để tạo ra một bức tranh rõ hơn về năng lực của từng học sinh.

Từ các dữ liệu đó, EduTwin hỗ trợ học sinh hiểu mình đang yếu ở đâu và nên học gì tiếp theo; hỗ trợ giáo viên phát hiện sớm học sinh cần can thiệp; đồng thời giúp quản lý trung tâm kiểm soát tài khoản, quyền truy cập và tình trạng học tập ở mức tổng hợp.

**Điểm khác biệt so với hệ thống quản lý điểm thông thường:** Một học sinh được 6/10 chưa nói lên nguyên nhân. EduTwin cố gắng phân biệt: học sinh thiếu kiến thức nền, hiểu sai một bước, làm quá nhanh, thiếu tự tin hay thay đổi đáp án nhiều lần. Hệ thống vì vậy hướng tới 'hiểu quá trình học' chứ không chỉ 'ghi kết quả'.

### 1.3. Phạm vi áp dụng

* Trung tâm giáo dục hoặc trung tâm luyện thi có nhiều lớp, giáo viên và học sinh.
* Học sinh THPT học theo môn, chủ đề, chương trình và bài tập do trung tâm tổ chức.
* Giáo viên cần theo dõi tiến độ, phát hiện điểm yếu và xem lại các trường hợp AI không chắc chắn.
* Quản lý trung tâm cần phân quyền linh hoạt theo nhiệm vụ thực tế, thay vì sửa mã nguồn mỗi khi tổ chức thay đổi.

## 2. BÀI TOÁN THỰC TẾ CẦN GIẢI QUYẾT

Nhiều hệ thống học tập hiện nay trả lời tốt câu hỏi “học sinh được bao nhiêu điểm”, nhưng lại trả lời chưa tốt các câu hỏi “vì sao học sinh sai”, “chủ đề nào đang là nguyên nhân gốc” và “học sinh nên làm gì tiếp theo”. Đây là khoảng trống chính mà EduTwin muốn xử lý.

| Vấn đề | Ảnh hưởng thực tế |
| --- | --- |
| **Điểm số thiếu chiều sâu** | Hai học sinh cùng 6/10 có thể có nguyên nhân khác nhau. Một em mất kiến thức nền, một em sai kỹ năng biến đổi, một em biết làm nhưng quá vội. |
| **Giáo viên khó phát hiện sớm** | Nếu lớp đông, giáo viên khó đọc từng bài giải để biết ai đang gặp vấn đề nghiêm trọng hoặc chủ đề nào nhiều học sinh cùng sai. |
| **Học sinh thiếu định hướng tiếp theo** | Sau khi biết điểm, học sinh thường không biết nên ôn chủ đề nào trước, vì sao phải ôn và mục tiêu đó liên quan thế nào tới phần kiến thức kế tiếp. |
| **Quyền truy cập cứng nhắc** | Nếu chỉ dùng ba nhãn Học sinh/Giáo viên/Quản lý rồi viết quyền cố định trong mã nguồn, trung tâm khó tạo thêm các vai trò như Người duyệt nội dung hoặc Điều phối học thuật. |
| **Phụ thuộc AI là rủi ro** | AI có thể chậm, hết hạn mức, mất mạng hoặc trả lời sai. Một hệ thống giáo dục không được mất khả năng vận hành chỉ vì dịch vụ AI gặp sự cố. |

### 2.1. Tình huống minh họa

Ví dụ, Minh và An cùng làm một bài kiểm tra 10 câu và đều được 6 điểm. Nếu chỉ nhìn điểm, hai em giống nhau. Nhưng dữ liệu chi tiết có thể cho thấy Minh sai liên tục ở các câu cần kiến thức phương trình bậc hai, còn An chủ yếu sai vì thay đổi đáp án ở giây cuối dù trước đó chọn đúng. EduTwin cần ghi nhận sự khác biệt này để giáo viên không áp dụng cùng một cách hỗ trợ cho cả hai em.

## 3. MỤC TIÊU CỦA HỆ THỐNG

### 3.1. Mục tiêu tổng quát

Mục tiêu quan trọng nhất của EduTwin là xây dựng một nền tảng học tập có khả năng theo dõi và giải thích sự tiến bộ của từng học sinh theo thời gian. Hệ thống giúp học sinh hiểu điểm mạnh, điểm yếu và hành động học tập tiếp theo; giúp giáo viên phát hiện sớm những học sinh cần hỗ trợ và có đủ thông tin để can thiệp; giúp quản lý trung tâm vận hành người dùng, lớp học và quyền truy cập một cách linh hoạt, an toàn.

EduTwin hướng tới việc biến dữ liệu học tập rời rạc thành một hồ sơ năng lực có lịch sử, có lý do và có thể kiểm tra lại. AI được sử dụng để bổ sung khả năng đọc hiểu phần trình bày suy luận của học sinh, nhưng AI không được phép trở thành nơi quyết định duy nhất về điểm số, mức độ thành thạo hoặc nội dung học tiếp theo.

### 3.2. Các mục tiêu cụ thể

| Mục tiêu | Hệ thống cần đạt được |
| --- | --- |
| **Cá nhân hóa việc học** | Mỗi học sinh có một hồ sơ học tập riêng theo từng môn và từng chủ đề; hệ thống không áp dụng một lộ trình giống nhau cho tất cả. |
| **Phát hiện đúng điểm yếu** | Theo dõi mức độ thành thạo theo chủ đề kiến thức, không chỉ theo điểm tổng của một bài kiểm tra. |
| **Giải thích nguyên nhân** | Kết hợp đáp án, thời gian làm bài, mức tự tin, số lần đổi đáp án và phần trình bày cách làm để cung cấp thêm ngữ cảnh cho giáo viên. |
| **Gợi ý hành động tiếp theo** | Ưu tiên chủ đề và bài tập nên học tiếp dựa trên mức độ thành thạo, mục tiêu, kiến thức tiên quyết và mức độ cấp thiết. |
| **Cảnh báo sớm** | Hỗ trợ nhận diện học sinh có nguy cơ không đạt mục tiêu để giáo viên can thiệp sớm thay vì chờ đến kỳ thi. |
| **Giữ giáo viên trong vòng kiểm soát** | Các trường hợp AI không chắc chắn hoặc bài tự luận chưa chấm phải được đưa vào danh sách cần giáo viên xem lại. |
| **Hoạt động được khi AI lỗi** | Đăng nhập, quản lý lớp, giao/nộp bài, chấm sơ bộ, lưu hành vi và các chức năng cốt lõi vẫn hoạt động khi AI không khả dụng. |
| **Phân quyền linh hoạt** | Quản lý trung tâm có thể tạo vai trò và gán quyền phù hợp với cơ cấu tổ chức mà không cần sửa mã nguồn. |
| **Minh bạch và có thể truy vết** | Mọi thay đổi quan trọng về quyền, bằng chứng học tập và Hồ sơ học tập số đều có lịch sử để biết ai hoặc cơ chế nào đã tạo ra thay đổi. |

### 3.3. Giá trị sáng tạo của đề tài

* **Hồ sơ học tập số theo thời gian:** mô hình hóa mức độ thành thạo và hành vi của từng học sinh thay vì chỉ lưu điểm.
* **Bộ kiểm soát bằng chứng:** kết quả AI không được đi thẳng vào hồ sơ học tập; hệ thống đánh giá mức tin cậy trước khi sử dụng.
* **Thiết kế không phụ thuộc AI:** AI làm tăng chất lượng phân tích nhưng không phải điều kiện để toàn hệ thống hoạt động.
* **Khuyến nghị có thể giải thích:** thứ tự chủ đề nên học được tính bằng quy tắc xác định; AI chỉ hỗ trợ viết lời giải thích dễ đọc.
* **Phân quyền động theo từng trung tâm:** cùng là giáo viên nhưng mỗi người có thể được giao nhiệm vụ và quyền khác nhau.

## 4. ĐỐI TƯỢNG SỬ DỤNG

Trong phạm vi hệ thống hiện tại có các nhóm người dùng chính. Giảng viên môn học là người đánh giá đồ án, không phải một tài khoản vận hành trong EduTwin.

| Đối tượng | Nhiệm vụ chính | Lợi ích nhận được |
| --- | --- | --- |
| **Học sinh** | Làm bài, gửi đáp án và cách trình bày suy luận; xem kết quả, phản hồi, hồ sơ học tập, mục tiêu và gợi ý học tiếp. | Biết mình yếu ở đâu, vì sao và nên làm gì tiếp theo. |
| **Giáo viên** | Quản lý nội dung học tập, câu hỏi, bài tập; theo dõi lớp; xem các trường hợp cần kiểm tra; xác nhận hoặc sửa kết quả phân tích. | Biết học sinh nào cần hỗ trợ, lớp đang yếu chủ đề nào và bằng chứng nào cần xem lại. |
| **Quản lý trung tâm** | Quản lý tài khoản, lớp, vai trò, quyền, nhật ký phân quyền và báo cáo tổng hợp của trung tâm. | Biết ai được làm gì, dữ liệu có an toàn không và lớp nào cần chú ý. |
| **Quản trị viên nền tảng (PlatformAdmin)** | Quản lý vòng đời các trung tâm đối tác (tạo mới, kích hoạt, đình chỉ), khởi tạo quản lý trung tâm, theo dõi số liệu thống kê tổng hợp toàn hệ thống. | Quản trị tập trung toàn bộ nền tảng, đảm bảo an toàn, cách ly dữ liệu giữa các trung tâm mà không can thiệp vào hoạt động học thuật. |

### 4.1. Học sinh

* **Đăng nhập và Quản lý Không gian Học tập:**
  + Đăng nhập bảo mật theo phân quyền Học sinh; tự động nhận diện trung tâm và các môn học được phân bổ.
  + Xem tổng quan tiến độ môn học, điểm mục tiêu kỳ thi và cảnh báo mức độ rủi ro (An toàn, Trung bình, Nguy cơ cao).
* **Quản lý và Theo dõi Bài tập Được Giao:**
  + Xem danh sách bài tập được giáo viên giao theo từng trạng thái: *Chưa bắt đầu*, *Đang làm*, *Đã hoàn thành*, *Quá hạn*.
  + Theo dõi tiến độ hoàn thành bài tập trực quan (số câu đã làm / tổng số câu, tỷ lệ phần trăm %).
  + Xem chi tiết từng câu hỏi trong bài tập: mức độ khó, thời gian dự kiến, loại câu hỏi và trạng thái đã làm hay chưa trước khi bắt đầu.
* **Không gian Làm bài & Luyện tập Thích ứng (Adaptive Learning & Assessment Workspace):**
  + Làm bài theo đề thích ứng dựa trên năng lực hiện tại hoặc theo bài tập chỉ định từ giáo viên.
  + Nhập đáp án đa dạng: Trắc nghiệm lựa chọn, câu hỏi điền đáp án ngắn, và trình bày các bước suy luận / giải thích chi tiết.
  + **Trình gõ toán học trực quan (Visual Math Field):** Soạn thảo công thức toán học chuẩn xác thời gian thực (WYSIWYG LaTeX) với các ô điền [?] trực quan, tích hợp thanh công cụ biểu thức nhanh.
  + **Khung trợ lý học tập song song (Side-by-Side Assistant Workspace) tích hợp ngay kế bên bài làm:**
    - *Máy tính khoa học Casio fx-580VN X:* Thực hiện tính toán nâng cao với tính năng 1-click chèn trực tiếp kết quả vào vị trí con trỏ bài làm.
    - *Bảng nháp vẽ vector trực tuyến (Scratchpad):* Công cụ bút vẽ, tẩy, thước đo, compa, êke tam giác, hệ trục Oxy, giấy kẻ ô ly; tự động xuất ảnh PNG làm bằng chứng bài làm gửi kèm cho AI và giáo viên.
    - *Khảo sát đồ thị hàm số 2D (Function Grapher):* Phác thảo và quan sát đồ thị hàm số trực quan ngay khi làm bài.
  + **Bảo toàn phiên làm bài & Chống nộp lặp:** Tự động lưu bản nháp vào bộ nhớ cục bộ IndexedDB, tự khôi phục dữ liệu khi tải lại trang, và sử dụng mã định danh duy nhất (clientSubmissionId) để chống lỗi gửi trùng lặp.
* **Thu thập Dữ liệu Hành vi Học tập (Behavioral Telemetry):**
  + Tự động ghi nhận thời gian làm bài chi tiết của từng câu hỏi.
  + Tự đánh giá mức độ tự tin (Confidence level: 0 - 100%) cho câu trả lời.
  + Tự động đếm tần suất thay đổi đáp án và phát hiện hành vi bỏ qua câu hỏi để nhận diện thói quen làm bài (đoán mò, vội vã hay cân nhắc kỹ lưỡng).
* **Phản hồi Đa chiều & Phân tích Tư duy Chuyên sâu từ AI:**
  + Theo dõi trạng thái phân tích bất đồng bộ theo thời gian thực (Job Polling).
  + Xem điểm số sơ bộ và độ chính xác (Đúng, Sai, Đúng một phần) ngay sau khi hoàn tất.
  + Nhận báo cáo phân tích tư duy chi tiết từ AI: Phương pháp giải đã nhận diện, điểm chất lượng tư duy (/100), các bước suy luận còn thiếu và các lỗ hổng / quan niệm sai lầm cốt lõi (Misconceptions).
  + Xem lời giải mẫu chuẩn xác và các bài làm cần giáo viên xác nhận / nhận xét điều chỉnh thủ công.
* **Hồ sơ Năng lực Số (Digital Twin - Bản sao số của Học sinh):**
  + **Bản sao Tri thức (Knowledge Twin):** Biểu đồ Radar năng lực và bảng chi tiết mức độ thành thạo (%) theo từng node chuyên đề trong Cây tri thức (Knowledge Graph), kèm số lượng bằng chứng tích lũy.
  + **Bản sao Hành vi (Behavioral Twin):** Báo cáo thời gian làm bài trung bình, tỷ lệ bỏ qua, tỷ lệ đổi phương án, và **Chỉ số hiệu chuẩn độ tự tin (Confidence Calibration Index)** giúp rèn luyện thói quen tự lượng sức mình.
  + **Tăng trưởng Nhận thức (Cognitive Growth):** Biểu đồ đường tiến trình năng lực qua các mốc thời gian, đo lường tốc độ bứt phá điểm số ($\Delta%$) và khoảng cách tới điểm mục tiêu kỳ thi.
  + **Nhật ký Cập nhật Năng lực (Twin History):** Dòng thời gian ghi nhận chi tiết mọi sự kiện tác động vào hồ sơ (do AI phân tích bài làm hay giáo viên điều chỉnh) có hiển thị biến động điểm số và lý do cụ thể.
* **Đề xuất Hành động Học tập Thông minh (Intelligent Recommendations):**
  + Nhận đề xuất chuyên đề nên học tiếp theo dựa trên dữ liệu bản sao số, đi kèm chiến lược can thiệp cụ thể và giải thích lý do rõ ràng (không phải gợi ý ngẫu nhiên).
  + Hiển thị chỉ số cơ hội tăng điểm dự kiến (+X% điểm) nếu hoàn thành chuyên đề được gợi ý.
  + Chủ động tương tác với gợi ý: Lựa chọn *Bắt đầu học ngay*, *Chấp nhận* vào kế hoạch học hoặc *Bỏ qua* để hệ thống tự tối ưu lại lộ trình

### 4.2. Giáo viên

* Quản lý nội dung kiến thức theo môn và mối quan hệ kiến thức tiên quyết.
* Xây dựng chương trình học, ngân hàng câu hỏi và bài tập.
* Giao bài cho lớp hoặc học sinh cụ thể và theo dõi tiến độ.
* Xem các bài mà AI hoặc cơ chế dự phòng đánh dấu cần kiểm tra.
* Xác nhận/sửa kết quả phân tích có lý do và làm hệ thống tính lại trạng thái liên quan.
* Xem báo cáo theo lớp, học sinh và chủ đề để ưu tiên hỗ trợ.

### 4.3. Quản lý trung tâm

* Quản lý hồ sơ trung tâm, tài khoản, giáo viên, học sinh, lớp và môn học.
* Tạo vai trò tùy chỉnh trong trung tâm và chọn các quyền phù hợp.
* Gán hoặc thu hồi vai trò của tài khoản cùng trung tâm.
* Xem nhật ký thay đổi quyền để biết ai đã cấp/thu hồi quyền và vào thời điểm nào.
* Xem báo cáo tổng hợp theo lớp, môn hoặc mục tiêu mà không vượt ra ngoài dữ liệu của trung tâm.

### 4.4. Quản trị viên nền tảng (PlatformAdmin)

* **Vị trí và Phạm vi hoạt động:** Đóng vai trò là **Mặt phẳng điều khiển (Control Plane)** của toàn hệ thống, hoạt động độc quyền trong không gian tenant hệ thống dành riêng mang tên PLATFORM (Root Tenant).
* **Ràng buộc hai chiều nghiêm ngặt (Tenant Invariant):**
  + Định danh người dùng của PlatformAdmin bắt buộc phải gắn liền với trung tâm PLATFORM.
  + Người dùng thuộc các trung tâm đào tạo đối tác thông thường tuyệt đối không thể thuộc PLATFORM và không thể được nâng cấp thành PlatformAdmin. Ngược lại, tài khoản PlatformAdmin không thể bị gán quyền hoặc chuyển sang bất kỳ trung tâm nào khác.
* **Quyền hạn và Trách nhiệm chính:**
  + Quản trị toàn diện danh sách và vòng đời của các trung tâm đối tác (khởi tạo, cập nhật metadata, đình chỉ và kích hoạt lại).
  + Quản trị nhân sự quản lý trung tâm (CenterManager): tạo mới, khóa/mở khóa, đặt lại mật khẩu và chỉ định/chuyển giao Quản lý chính (Primary CenterManager).
  + Theo dõi các chỉ số quy mô vận hành tổng hợp (số lượng học sinh, giáo viên, lớp học, quản lý đang hoạt động).
  + Tra cứu nhật ký kiểm toán độc lập trên toàn nền tảng.
  + Tự quản lý bảo mật tài khoản cá nhân (xem số phiên hoạt động, đổi mật khẩu và thu hồi toàn bộ phiên đăng nhập khẩn cấp).
* **Ranh giới dữ liệu giáo dục tuyệt đối (Educational Data Denial Matrix):**
  + PlatformAdmin tuyệt đối **bị từ chối truy cập (403 Forbidden fail-closed)** đối với toàn bộ dữ liệu học thuật chi tiết: không được xem bài làm (Attempt), câu trả lời (Answer), ảnh bản vẽ nháp (Attachment), kết quả phân tích suy luận AI (AI reasoning), hàng đợi chấm bài của giáo viên (Teacher Review Queue) hay Hồ sơ năng lực số của học sinh (Digital Twin).
  + Chỉ được phép xem các chỉ số thống kê số lượng tổng hợp ở cấp độ hạ tầng, không thể truy ngược về danh tính hay kết quả của một cá nhân cụ thể.

## 5. ĐẶC TẢ CHỨC NĂNG CỐT LÕI

### 5.1. Quản lý tài khoản và tổ chức

**Mục đích:** Quản lý cấu trúc trung tâm, người dùng và lớp học.

**Chức năng chính:**

* Đăng nhập, làm mới phiên đăng nhập, đăng xuất và xem thông tin tài khoản hiện tại.
* Quản lý hồ sơ giáo viên, học sinh, lớp và môn học.
* Gán học sinh vào lớp và gán giáo viên phụ trách lớp.
* Khóa/vô hiệu hóa tài khoản khi cần mà không làm mất lịch sử dữ liệu.

**Quy tắc quan trọng:**

* Không cho phép người dùng của trung tâm A tham chiếu dữ liệu trung tâm B.
* Dữ liệu đã có lịch sử học tập không được xóa tùy tiện; ưu tiên vô hiệu hóa/xóa mềm.

### 5.2. Phân quyền động trong trung tâm

**Mục đích:** Cho phép Quản lý trung tâm cấu hình ai được làm gì mà không sửa mã nguồn.

**Chức năng chính:**

* Xem danh mục quyền mà hệ thống hỗ trợ.
* Tạo/sửa/ngừng sử dụng một vai trò, ví dụ Người duyệt nội dung.
* Gán nhiều quyền vào một vai trò.
* Gán nhiều vai trò phù hợp cho một tài khoản.
* Xem quyền hiệu lực của một tài khoản.
* Xem nhật ký các lần cấp/thu hồi quyền.

**Quy tắc quan trọng:**

* Không cho gán quyền vượt ra ngoài trung tâm.
* Không cho tự nâng quyền hoặc làm mất người quản trị cuối cùng còn đủ quyền.
* Mọi thay đổi quyền phải ghi lý do và nhật ký.

### 5.3. Mô hình kiến thức và chương trình học

**Mục đích:** Mô tả các chủ đề kiến thức, mối quan hệ tiên quyết và chương trình học theo lớp.

**Chức năng chính:**

* Tạo chủ đề/chương/bài/khái niệm theo môn.
* Tạo quan hệ 'cần học trước', 'liên quan', 'thuộc về'.
* Tạo chương trình học và sắp xếp các chủ đề theo thứ tự.
* Gán chương trình cho lớp.

**Quy tắc quan trọng:**

* Không cho quan hệ một chủ đề trỏ tới chính nó.
* Không cho chuỗi kiến thức tiên quyết tạo thành vòng lặp.
* Các chủ đề liên kết phải thuộc đúng môn và đúng trung tâm.

### 5.4. Ngân hàng câu hỏi

**Mục đích:** Quản lý câu hỏi dùng để đánh giá kiến thức và tạo bài tập.

**Chức năng chính:**

* Tạo/sửa/xem câu hỏi.
* Hỗ trợ câu hỏi lựa chọn và câu hỏi tự luận theo phạm vi hiện tại.
* Gắn câu hỏi với một hoặc nhiều chủ đề kiến thức.
* Quản lý đáp án lựa chọn và đáp án đúng đối với câu có thể chấm tự động.

**Quy tắc quan trọng:**

* Câu hỏi phải thuộc môn phù hợp.
* Dữ liệu đáp án đúng không được lộ cho học sinh trước khi hoàn tất bài.

### 5.5. Bài tập và giao bài

**Mục đích:** Cho giáo viên tạo bài, chọn câu hỏi và giao cho đúng đối tượng.

**Chức năng chính:**

* Tạo bài tập, chọn câu hỏi và thứ tự câu.
* Giao cho lớp hoặc học sinh cụ thể.
* Xuất bản, đóng bài, theo dõi người đã nhận và tiến độ.
* Lưu ảnh chụp danh sách người nhận tại thời điểm xuất bản để lịch sử không bị thay đổi khi lớp thay đổi sau đó.

**Quy tắc quan trọng:**

* Không cho học sinh ngoài danh sách người nhận truy cập bài.
* Không tự thay đổi bài đã xuất bản mà làm sai lịch sử nộp bài.

### 5.6. Làm bài và nộp bài

**Mục đích:** Ghi nhận kết quả và hành vi học tập của học sinh một cách an toàn.

**Chức năng chính:**

* Hiển thị bài được giao và trạng thái thời hạn.
* Nhận đáp án, phần trình bày cách làm, mức tự tin và dữ liệu hành vi.
* Chấm sơ bộ các loại câu hỏi có thể chấm xác định.
* Lưu bài nộp trước khi gửi sang xử lý AI.
* Cho giao diện kiểm tra trạng thái phân tích sau khi nộp.

**Quy tắc quan trọng:**

* Gửi lặp do mạng không được tạo hai bài nộp giống nhau.
* Bài đã lưu không được mất chỉ vì AI lỗi hoặc timeout.

### 5.7. Phân tích cách suy luận bằng AI

**Mục đích:** Dùng AI để đọc phần trình bày cách làm và tạo quan sát hỗ trợ giáo viên.

**Chức năng chính:**

* Nhận đầu vào tối thiểu cần thiết của bài làm.
* Trả kết quả có cấu trúc: phương pháp nhận diện, chất lượng suy luận, loại lỗi, hiểu sai, bước thiếu và phản hồi gợi ý.
* Kiểm tra cấu trúc và tính hợp lý trước khi lưu.
* Tự chuyển sang cơ chế dự phòng khi AI lỗi.

**Quy tắc quan trọng:**

* AI không tự sửa đáp án đúng, không tự quyết định điểm cuối và không tự cập nhật trực tiếp hồ sơ học tập.
* Kết quả AI chỉ là một nguồn bằng chứng.

### 5.8. Bộ kiểm soát bằng chứng

**Mục đích:** Đánh giá mức độ tin cậy của dữ liệu trước khi cho phép ảnh hưởng đến hồ sơ học tập.

**Chức năng chính:**

* Kiểm tra kết quả AI có đúng cấu trúc và không mâu thuẫn rõ với dữ liệu xác định.
* Xác định mức 'Tin cậy', 'Tin cậy giảm' hoặc 'Chờ giáo viên'.
* Quyết định phần phân tích suy luận được dùng với trọng số bao nhiêu.
* Lưu quyết định thành lịch sử, không sửa đè record cũ.

**Quy tắc quan trọng:**

* Điểm tự tin do AI tự báo không được coi là xác suất đúng tuyệt đối.
* Bài tự luận chưa được giáo viên chấm phải ở trạng thái chờ giáo viên và không làm thay đổi mức độ thành thạo.

### 5.9. Hồ sơ học tập số

**Mục đích:** Theo dõi trạng thái học tập của từng học sinh theo thời gian.

**Chức năng chính:**

* Lưu mức độ thành thạo theo chủ đề.
* Lưu hành vi theo môn: thời gian, mức tự tin, tỷ lệ bỏ qua, số lần đổi đáp án.
* Lưu mục tiêu và rủi ro theo môn.
* Lưu lịch sử mỗi lần trạng thái thay đổi và nguồn bằng chứng.

**Quy tắc quan trọng:**

* Mọi thay đổi quan trọng phải giải thích được vì sao xảy ra.
* Dữ liệu hành vi có thể cập nhật ngay cả khi AI lỗi; dữ liệu suy luận không được giả lập khi AI không có.

### 5.10. Gợi ý học tập và báo cáo

**Mục đích:** Giúp học sinh biết nên làm gì tiếp theo và giúp giáo viên/quản lý biết nơi cần chú ý.

**Chức năng chính:**

* Tính độ ưu tiên của chủ đề dựa trên mức thành thạo, mục tiêu, kiến thức tiên quyết và độ cấp thiết.
* Tạo lộ trình học và gợi ý bài/chủ đề tiếp theo.
* Hiển thị lý do gợi ý cho học sinh.
* Tạo bảng tổng hợp cho giáo viên và quản lý trung tâm.

**Quy tắc quan trọng:**

* Thứ tự gợi ý do thuật toán xác định để có thể tái lập và kiểm thử.
* AI chỉ được hỗ trợ viết lời giải thích bằng ngôn ngữ tự nhiên, không tự xếp hạng nội dung.

### 5.11. Quản trị nền tảng (Platform Administration)

**Mục đích:** Đóng vai trò là Mặt phẳng điều khiển (Control Plane) chịu trách nhiệm khởi tạo, giám sát và quản trị vòng đời toàn bộ hệ sinh thái các trung tâm đối tác (tenants) trên nền tảng EduTwin. Vai trò này tách biệt hoàn toàn với công việc quản trị vận hành học thuật nội bộ của Quản lý trung tâm (CenterManager) và hoạt động độc quyền trong Root Tenant PLATFORM.

| **Vai trò** | **Phạm vi và Ranh giới dữ liệu** |
| --- | --- |
| **PlatformAdmin** | Quản lý danh sách trung tâm, tài khoản quản lý trung tâm và chỉ số hạ tầng tổng hợp toàn hệ thống. Bị từ chối truy cập (403 Forbidden) đối với mọi dữ liệu học thuật chi tiết (bài làm, đáp án, suy luận AI, Hồ sơ học tập số). |
| **CenterManager** | Chỉ quản lý người dùng, lớp học, phân quyền động và chương trình đào tạo nội bộ bên trong phạm vi trung tâm của mình. |

**6 Nhóm Chức năng Cốt lõi:**

**1. Quản trị Vòng đời Trung tâm (Tenant Lifecycle Management):** Xem danh sách, tra cứu và lọc tất cả các trung tâm đối tác; khởi tạo trung tâm mới; cập nhật thông tin metadata (tên, địa chỉ, số điện thoại, email liên hệ); kích hoạt hoặc đình chỉ hoạt động của trung tâm. Khi đình chỉ, hệ thống tự động vô hiệu hóa tức thì toàn bộ phiên đăng nhập của người dùng thuộc trung tâm đó (tăng AuthVersion và thu hồi refresh token).

**2. Quản trị Vòng đời Nhân sự Quản lý (CenterManager Lifecycle):** Khởi tạo tài khoản Quản lý đầu tiên (Primary CenterManager) khi tạo trung tâm mới; quản lý danh sách cán bộ quản lý của từng trung tâm; khóa/mở khóa tài khoản; đặt lại mật khẩu khẩn cấp (có kiểm tra concurrency version); chỉ định hoặc chuyển giao vai trò Quản lý chính.

**3. Theo dõi Quy mô Vận hành An toàn (Safe Operational Analytics):** Báo cáo các chỉ số tổng hợp cấp hạ tầng như tổng số lượng học sinh, giáo viên, lớp học, tài khoản quản lý đang hoạt động theo từng trung tâm hoặc trên toàn hệ thống mà không truy cập vào bất kỳ nội dung hay kết quả học tập cá nhân nào.

**4. Nhật ký Kiểm toán Nền tảng Độc lập (Platform Audit Log):** Ghi nhận và cho phép tra cứu toàn bộ lịch sử các thao tác tác động cấp nền tảng (tạo trung tâm, đình chỉ/kích hoạt, tạo/khóa tài khoản quản lý, reset mật khẩu) phục vụ công tác kiểm tra an ninh và truy vết độc lập.

**5. Tự Quản lý Bảo mật Cá nhân (Self Security Management):** Cho phép PlatformAdmin tự quản lý thông tin tài khoản cá nhân, xem danh sách các phiên đăng nhập đang hoạt động, đổi mật khẩu cá nhân và thực hiện lệnh thu hồi toàn bộ phiên đăng nhập trên các thiết bị khác trong trường hợp nghi ngờ rò rỉ thông tin.

**6. Trải nghiệm Giao diện Người dùng (User Interface & Experience):** Bố cục không gian làm việc quản trị chuyên biệt dành riêng cho Control Plane, cung cấp các bộ lọc trạng thái (Hoạt động/Đình chỉ), tìm kiếm theo từ khóa, bảng thống kê trực quan và phản hồi trạng thái rõ ràng cho mọi thao tác tác động hệ thống.

**Quy tắc Quan trọng và Ranh giới Kiến trúc:**

* **Tính Bất khả Ủy quyền (IsDelegable = false):** Mọi quyền chức năng thuộc nhóm quản trị nền tảng (platform.\*) là duy nhất cho PlatformAdmin, không được phép gán, kế thừa hoặc ủy quyền cho bất kỳ vai trò nào thuộc các trung tâm đào tạo đối tác.
* **Bảo vệ Root Tenant PLATFORM:** Trung tâm hệ thống PLATFORM là tenant gốc duy nhất chứa tài khoản PlatformAdmin. Root Tenant này bất khả xâm phạm: không thể bị xóa, đổi tên hay chuyển sang trạng thái đình chỉ.
* **Ranh giới Thiết kế Hoãn lại (Deferred Design Boundaries):** Các tính năng phức tạp như tự cấu hình gói cước thương mại (Billing/Subscription), giám sát tài nguyên hạ tầng server sâu (Infrastructure Telemetry) hoặc công cụ tự động chuyển đổi dữ liệu giữa các trung tâm chưa triển khai ở giai đoạn này để giữ thiết kế tối giản và tập trung vào an toàn dữ liệu.

### 5.12. Bộ công cụ Toán học trực quan trong Learning Player

**Mục đích:** Nâng cấp môi trường làm bài (Learning Player) thành không gian làm bài Toán trực quan, hỗ trợ học sinh trình bày suy luận phong phú, có AI phân tích hỗ trợ nhưng không biến AI thành lõi phụ thuộc.

Bao gồm 4 phân hệ chính (A -> D):

**A. Bàn phím nhập công thức Toán (Visual Math Toolbar):**

* Giúp học sinh không phải tự nhớ và gõ mã LaTeX bằng tay.
* Cung cấp thanh công cụ với 5 nhóm chuyên biệt: **Cơ bản** (±, ×, ÷, √x, x², xⁿ, a/b); **Đại số** (≤, ≥, ≠, ≈, ∞, π, α, β, θ); **Giải tích** (∫, d/dx, Σ, lim); **Tập hợp và Logic** (∈, ∉, ⊂, ∪, ∩, ∅, ∀, ∃, ⟹); **Hình học và Lượng giác** (sin, cos, tan, cot, ∠, △, ⊥, ∥, °).
* Công thức được chèn trực tiếp tại vị trí con trỏ và hiển thị bản xem trước 2D bằng KaTeX an toàn.
* *Nguyên tắc dữ liệu:* Tách riêng nội dung có ý nghĩa logic để chấm điểm và công thức LaTeX (answer\_display\_latex) dùng để trình bày.
* *Chế độ chấm câu trả lời ngắn (ShortAnswer):* Hỗ trợ 3 chế độ: TextExact, NumericRational (chuẩn hóa phân số tối giản với BigInteger), và Manual.

**B. Máy tính khoa học (Scientific Calculator Drawer):**

* Ngăn máy tính phụ trợ ngay trong màn hình làm bài, hỗ trợ: phép tính số học, căn và lũy thừa, logarit, lượng giác, Ans, bộ nhớ tạm và lịch sử.
* *Nguyên tắc trung thực học thuật:* Máy tính chỉ là công cụ tính toán số học trung gian; cấm tuyệt đối tính năng tự động giải toàn bộ phương trình.

**C. Bảng vẽ nháp Toán học (Vector Scratchpad Canvas):**

* Bảng vẽ toàn màn hình phục vụ vẽ hình chứng minh, đồ thị và phép biến đổi.
* *Công cụ hỗ trợ:* Bút vẽ tự do, tẩy, thước kẻ, compa, vẽ tam giác, hệ trục Oxy, lưới ô ly, undo/redo 30 bước, pan và zoom.
* *Tối ưu lưu trữ:* Bản nháp được lưu dưới dạng các nét vector trong IndexedDB; chỉ khi nộp bài hệ thống mới kết xuất ra một ảnh PNG duy nhất.

**D. Nộp bản vẽ và phân tích đa phương thức:**

* Học sinh có thể đính kèm ảnh PNG từ bảng vẽ nháp vào bài nộp; dữ liệu được lưu vào bảng attempt\_attachments; Worker gửi dữ liệu cho AI phân tích.
* *Cơ chế bảo vệ & bảo mật:* Ảnh PNG <= 5 MB, <= 4096x4096px. Signed token gắn chặt với ID trung tâm và học sinh, chống replay attack.
* *Phân tích AI đa phương thức:* AI đọc hình vẽ nháp để hỗ trợ phát hiện sai bước biến đổi, vẽ sai đồ thị hoặc thiếu giả thiết.
* *Evidence Gate:* Kết quả AI chỉ là quan sát, không phải kết luận tự động ghi đè điểm số.

**Mục tiêu chung:** Môi trường làm bài Toán hoạt động độc lập, có cơ chế dự phòng đầy đủ ngay cả khi AI gián đoạn hoặc mất mạng.

## 6. CHỨC NĂNG NÂNG CAO VÀ ĐIỂM SÁNG TẠO

| Hướng nâng cao | Mục tiêu |
| --- | --- |
| **Dự báo nguy cơ bằng ML.NET** | Đã được đánh giá chính thức tại R07 và nhận quyết định **NO-GO**: hiện có 0 sự kiện đủ điều kiện (cần tối thiểu 10.000), chưa có quy trình xin phép người dùng cho ML profiling. Hệ thống giữ thuật toán heuristic opportunity-v1 làm baseline duy nhất. Có sẵn evaluation protocol đã phê duyệt để tái đánh giá khi đủ dữ liệu thật. |
| **Giải thích khuyến nghị bằng AI** | Sau khi hệ thống tự tính chủ đề ưu tiên, AI có thể viết lại lý do theo ngôn ngữ tự nhiên dễ hiểu. AI không được thay đổi thứ tự đã tính. |
| **Đánh giá chất lượng mô hình** | So sánh AI/ML với cách tính cơ bản, theo dõi sai số, độ ổn định và những trường hợp cần giáo viên can thiệp. |
| **Triển khai và kiểm thử tải** | Sau khi bản chính ổn định có thể triển khai môi trường cloud, đo thời gian phản hồi, hàng đợi AI và tải dashboard. |
| **Bộ công cụ toán học và minh chứng đa phương thức** | Bổ sung thanh công cụ ký tự toán học KaTeX, máy tính khoa học an toàn, bảng vẽ nháp vector (IndexedDB) và nộp ảnh minh chứng kèm bài nộp (bảng attempt\_attachments). AI hỗ trợ đọc hiểu hình vẽ nháp nhưng không quyết định điểm số. Chi tiết xem mục 5.12 và 7.9. |

## 7. QUY TRÌNH NGHIỆP VỤ CHÍNH

### 7.1. Quản lý vai trò và quyền

1. Quản lý trung tâm mở màn hình quản lý vai trò.
2. Quản lý tạo một vai trò mới hoặc chọn vai trò có sẵn để chỉnh sửa.
3. Hệ thống hiển thị các quyền phù hợp với loại tài khoản của vai trò.
4. Quản lý chọn quyền, nhập lý do thay đổi và gửi yêu cầu.
5. Máy chủ kiểm tra trung tâm, quyền của người đang thao tác, giới hạn ủy quyền và nguy cơ làm mất quản trị viên cuối cùng.
6. Nếu hợp lệ, hệ thống lưu thay đổi và ghi nhật ký đầy đủ.
7. Các phiên đăng nhập cũ của người bị thay đổi quyền được làm mới hoặc vô hiệu hóa theo chính sách.

*Điểm cần nhớ: Ba lớp kiểm tra luôn phải cùng tồn tại: có quyền chức năng, đúng trung tâm và đúng phạm vi tài nguyên phụ trách.*

### 7.2. Giáo viên tạo và giao bài

1. Giáo viên tạo bài tập, nhập tiêu đề, thời gian mở/đóng và các thông tin cần thiết.
2. Giáo viên chọn câu hỏi từ ngân hàng câu hỏi.
3. Hệ thống kiểm tra câu hỏi thuộc đúng môn và giáo viên có quyền sử dụng.
4. Giáo viên chọn lớp hoặc học sinh cần nhận bài.
5. Khi xuất bản, hệ thống chụp lại danh sách câu hỏi và người nhận để bảo toàn lịch sử.
6. Học sinh thuộc danh sách người nhận thấy bài trên trang cá nhân.

### 7.3. Học sinh làm và nộp bài

1. Học sinh mở bài được giao và hệ thống kiểm tra bài còn hiệu lực.
2. Học sinh trả lời câu hỏi; có thể nhập phần giải thích cách làm và mức tự tin.
3. Hệ thống ghi nhận thêm thời gian làm, số lần đổi đáp án và trạng thái bỏ qua.
4. Khi nộp, hệ thống kiểm tra yêu cầu gửi có bị trùng do mạng gửi lại hay không.
5. Các câu có đáp án xác định được chấm sơ bộ ngay.
6. Bài nộp và công việc phân tích AI được lưu trước; giao diện nhận thông báo đã nộp thành công.
7. Phân tích AI chạy ở phía sau. Học sinh không phải chờ AI mới được công nhận đã nộp bài.

*Điểm cần nhớ: Nếu AI mất kết nối, bài nộp vẫn còn nguyên. Đây là nguyên tắc quan trọng nhất để AI không kéo toàn hệ thống xuống.*

### 7.4. Khi AI hoạt động bình thường

1. Hệ thống lấy một bài nộp đang chờ phân tích.
2. AI phân tích phần trình bày cách suy luận và trả kết quả có cấu trúc.
3. Hệ thống kiểm tra định dạng, dữ liệu liên quan và các mâu thuẫn rõ ràng.
4. Bộ kiểm soát bằng chứng đánh giá kết quả ở mức tin cậy phù hợp.
5. Nếu đủ điều kiện, hệ thống dùng bằng chứng đó cùng với kết quả làm bài và hành vi để cập nhật Hồ sơ học tập số.
6. Hệ thống ghi lịch sử thay đổi rồi tính chủ đề nên ưu tiên học tiếp.
7. Học sinh và giáo viên nhận thông tin đã được xử lý cùng lý do cần thiết.

### 7.5. Khi AI hoặc Internet bị lỗi

1. Hệ thống phát hiện AI bị lỗi, quá thời gian hoặc không thể kết nối.
2. Hệ thống thử lại tối đa theo số lần đã cấu hình.
3. Nếu vẫn thất bại, hệ thống tạo kết quả dự phòng dựa trên dữ liệu xác định hiện có.
4. Phần phân tích suy luận được đánh dấu 'Chờ giáo viên' và có trọng số bằng 0.
5. Dữ liệu hành vi như thời gian, mức tự tin và số lần đổi đáp án vẫn được cập nhật.
6. Mức độ thành thạo không tăng/giảm chỉ vì AI lỗi.
7. Giáo viên nhìn thấy bài trong danh sách cần kiểm tra và có thể xác nhận sau.

*Điểm cần nhớ: Khi AI lỗi, EduTwin phải kém thông minh hơn chứ không được ngừng hoạt động.*

### 7.6. Bài tự luận chưa được chấm

1. Học sinh nộp bài tự luận; hệ thống chưa có kết luận đúng/sai.
2. AI có thể đọc phần trình bày để tạo nhận xét tham khảo.
3. Bộ kiểm soát bằng chứng bắt buộc giữ trạng thái 'Chờ giáo viên'.
4. Hồ sơ mức độ thành thạo không được thay đổi ở bước này.
5. Giáo viên chấm bài, xác nhận đúng/sai và xác nhận chất lượng suy luận cần thiết.
6. Hệ thống tạo bằng chứng mới do giáo viên xác nhận và tính lại các trạng thái liên quan.

*Điểm cần nhớ: Không được ngầm biến 'chưa có kết luận' thành 'sai'. Đây là một nguyên tắc dữ liệu quan trọng.*

### 7.7. Giáo viên sửa kết quả và hệ thống tính lại

1. Giáo viên mở bài cần xem lại và đọc dữ liệu bài nộp, kết quả AI cùng quyết định kiểm soát bằng chứng.
2. Giáo viên nhập nội dung cần sửa/xác nhận và bắt buộc ghi lý do.
3. Hệ thống kiểm tra giáo viên có quyền và có phụ trách học sinh/lớp liên quan hay không.
4. Hệ thống tạo một phiên bản bằng chứng mới, không xóa bằng chứng cũ.
5. Các thay đổi Hồ sơ học tập số liên quan được tính lại theo thứ tự thời gian.
6. Lịch sử mới được ghi thêm và các gợi ý cũ được thay thế bằng phiên bản mới nếu cần.

### 7.8. Quy trình Quản trị nền tảng và Vòng đời trung tâm

**7.8.1. Quy trình Khởi tạo và Quản trị Vòng đời Trung tâm:**

* PlatformAdmin đăng nhập vào hệ thống dưới tenant gốc PLATFORM và truy cập trang Quản trị trung tâm.
* Bấm "Tạo trung tâm mới", điền Mã trung tâm (CenterCode), Tên trung tâm, Múi giờ vận hành và thông tin Quản lý khởi tạo đầu tiên.
* Hệ thống thực hiện một Transaction nguyên tử: Tạo bản ghi trung tâm, khởi tạo tài khoản Quản lý đầu tiên, gán vai trò CenterManager, thiết lập liên kết Quản lý chính (primary\_manager\_user\_id) và ghi nhận nhật ký kiểm toán khử khuẩn.
* Khi cần sửa thông tin, PlatformAdmin mở modal cập nhật, chỉnh sửa tên/múi giờ kèm lý do; hệ thống kiểm tra RowVersion (OCC) để ngăn chặn ghi đè dữ liệu.
* Khi cần tạm dừng trung tâm, PlatformAdmin kích hoạt lệnh Đình chỉ (Suspend) kèm lý do bắt buộc. Hệ thống cập nhật trạng thái Suspended, tăng AuthVersion của toàn bộ tài khoản trong trung tâm và thu hồi toàn bộ refresh token, trục xuất toàn bộ người dùng trung tâm đó ra khỏi phiên làm việc.
* Khi cần mở lại, PlatformAdmin kích hoạt lệnh Tái kích hoạt (Reactivate); hệ thống kiểm tra trung tâm có Quản lý chính đang hoạt động trước khi cho phép mở lại.

**7.8.2. Quy trình Quản trị Vòng đời Nhân sự Quản lý (CenterManager):**

* PlatformAdmin chọn một trung tâm và mở cửa sổ "Quản lý nhân sự".
* Tạo thêm quản lý: Điền thông tin và mật khẩu mới; hệ thống khởi tạo tài khoản và ghi nhận audit log.
* Chuyển giao Quản lý chính: Chọn một quản lý phụ và bấm "Chuyển làm Quản lý chính". Có thể tick chọn ô "Đồng thời vô hiệu hóa Quản lý cũ". Hệ thống kiểm tra RowVersion của trung tâm và quản lý cũ, cập nhật con trỏ primary\_manager\_user\_id sang người mới.
* Khóa / Mở khóa tài khoản: Nhập lý do và xác nhận thay đổi trạng thái sang Locked hoặc Active. Hệ thống áp dụng bất biến: Từ chối nếu tài khoản là Quản lý chính hoặc là Quản lý hoạt động duy nhất còn lại của trung tâm.
* Đặt lại mật khẩu: Nhập mật khẩu mới và lý do; hệ thống băm mật khẩu mới, cập nhật vào CSDL, tăng AuthVersion của quản lý để hủy phiên cũ và ghi nhật ký kiểm toán (tuyệt đối không lưu mật khẩu mới vào nhật ký).

**7.8.3. Quy trình Giám sát Vận hành và Kiểm toán Khử khuẩn:**

* PlatformAdmin truy cập trang "Nhật ký kiểm toán nền tảng" (/quan-tri-nen-tang/nhat-ky).
* Hệ thống tải danh sách nhật ký kèm bộ lọc theo loại hành động, trung tâm và phân trang.
* Khi bấm xem chi tiết bản ghi, hệ thống hiển thị thời gian, người thực hiện, tài nguyên mục tiêu, lý do, mã Trace ID, trạng thái dữ liệu BeforeData và AfterData dưới dạng JSON chuẩn.
* Hệ thống đảm bảo 100% dữ liệu nhạy cảm (mật khẩu, token) đã được khử khuẩn trước khi hiển thị.

**7.8.4. Quy trình Tự Quản trị Bảo mật PlatformAdmin:**

* PlatformAdmin bấm vào biểu tượng tài khoản bảo mật cá nhân trên thanh điều hướng.
* Xem hồ sơ bảo mật: số phiên đang đăng nhập, thời điểm đăng nhập gần nhất, phiên bản AuthVersion.
* Nếu cần đổi mật khẩu: Nhập mật khẩu hiện tại và mật khẩu mới; hệ thống xác thực và cập nhật.

Nếu nghi ngờ lộ lọt phiên: Bấm "Thu hồi tất cả phiên đăng nhập". Hệ thống tăng AuthVersion của PlatformAdmin và thu hồi toàn bộ refresh token; phiên hiện tại và mọi phiên trên các thiết bị khác đều bị đăng xuất ngay lập tức, bắt buộc đăng nhập lại bằng thông tin mới.

### 7.9. Quy trình Nộp bài với Minh chứng Đa phương thức và Phân tích ảnh nháp

1. Học sinh làm bài, nhập công thức qua KaTeX, tính toán qua máy tính và vẽ nháp vector.
2. Bản vẽ nháp được lưu dạng vector cục bộ trong IndexedDB.
3. Học sinh đính kèm bản vẽ: hệ thống xuất ảnh PNG và nhận signed token qua API upload tạm.
4. Học sinh nộp bài đính kèm token ký số.
5. Máy chủ xác thực token, lưu bài nộp và metadata ảnh vào bảng attempt\_attachments.
6. Background worker đẩy dữ liệu câu trả lời kèm ảnh nháp sang AI để phân tích.
7. AI trả về AI Observation; Evidence Gate đánh giá mức độ tin cậy.
8. Sự cố lưu trữ ảnh: thử lại 3 lần. Nếu vẫn lỗi, chuyển vào danh sách giáo viên xem xét (bài chính khóa) hoặc cho phép nộp lại (bài tự do).

## 8. VAI TRÒ CỦA AI VÀ HỒ SƠ HỌC TẬP SỐ

### 8.1. AI được dùng để làm gì?

AI được dùng ở một nhiệm vụ hẹp nhưng có giá trị: đọc phần học sinh trình bày cách suy luận để tìm tín hiệu về phương pháp làm, loại lỗi, hiểu sai và bước còn thiếu. AI có thể soạn một bản phản hồi gợi ý cho học sinh hoặc giáo viên.

### 8.2. AI không được phép làm gì?

* Không tự quyết định điểm cuối của học sinh.
* Không tự sửa đáp án đúng hoặc nội dung do giáo viên xây dựng.
* Không tự thay đổi mức độ thành thạo mà bỏ qua Bộ kiểm soát bằng chứng.
* Không tự xếp hạng chủ đề học tiếp.
* Không quyết định quyền truy cập.
* Không đánh giá/xếp hạng giáo viên hoặc học sinh bằng một điểm AI duy nhất.

### 8.3. Bộ kiểm soát bằng chứng hoạt động ra sao?

| Mức | Khi nào xảy ra | Cách hệ thống sử dụng |
| --- | --- | --- |
| **Tin cậy** | Kết quả hợp lệ, không có mâu thuẫn đáng kể và vượt ngưỡng chính sách. | Phần suy luận có thể được dùng với trọng số đầy đủ. |
| **Tin cậy giảm** | Kết quả vẫn dùng được nhưng mức chắc chắn thấp hơn. | Phần suy luận chỉ ảnh hưởng một phần. |
| **Chờ giáo viên** | AI quá thiếu chắc chắn, có dấu hiệu mâu thuẫn, AI lỗi hoặc bài tự luận chưa có kết luận. | Phần suy luận không được làm thay đổi mức độ thành thạo. |

### 8.4. Hồ sơ học tập số gồm những gì?

| Thành phần | Ý nghĩa |
| --- | --- |
| **Mức độ thành thạo theo chủ đề** | Học sinh hiện hiểu từng chủ đề ở mức nào; được cập nhật theo bằng chứng hợp lệ. |
| **Hành vi học tập** | Thời gian làm, mức tự tin, tỷ lệ bỏ qua, số lần đổi đáp án và các xu hướng tương tự. |
| **Mục tiêu** | Mục tiêu điểm hoặc mức hoàn thành theo môn. |
| **Rủi ro** | Tín hiệu cho biết học sinh có nguy cơ không đạt mục tiêu hay không. |
| **Lịch sử thay đổi** | Mỗi lần trạng thái thay đổi đều có nguồn và lý do để truy vết. |

### 8.5. Hệ thống gợi ý học tiếp như thế nào?

Hệ thống không hỏi AI 'học sinh nên học gì tiếp'. Thay vào đó, hệ thống tự tính độ ưu tiên dựa trên các dữ liệu có cấu trúc: mức độ thành thạo hiện tại, mục tiêu, kiến thức tiên quyết, độ quan trọng của chủ đề và thời gian còn lại. Kết quả vì vậy có thể tính lại, so sánh và kiểm thử.

*Vai trò hợp lý của AI: AI có thể giúp viết câu giải thích thân thiện. Nhưng thứ tự ưu tiên phải do thuật toán của hệ thống quyết định.*

### 8.6. Công thức tính toán đã được khóa

Các công thức dưới đây đã được chốt chính thức (Chi tiết kỹ thuật xem CONSTITUTION.md §13–16):

**a) Bộ kiểm soát bằng chứng — ba mức tin cậy:**

| Mức | Khi nào | Trọng số suy luận | Hành động |
| --- | --- | --- | --- |
| **Tin cậy** | AI hợp lệ, không mâu thuẫn, mức tự tin AI ≥ 80; hoặc giáo viên xác nhận | 1,00 | Cho phép cập nhật đầy đủ |
| **Tin cậy giảm** | AI hợp lệ, mức tự tin AI 50–79 | 0,50 | Cập nhật giảm trọng số, gắn cờ theo dõi |
| **Chờ giáo viên** | Tự tin AI < 50, AI lỗi, bài tự luận chưa chấm | 0,00 | Không đổi mức thành thạo kiến thức |

**b) Tính mức độ thành thạo (Mastery heuristic v2):**

* MụcTiêuBằngChứng = 100 × ChấtLượngSuyLuận × (0,65 + 0,20 × ĐúngSai + 0,10 × ChấtLượngThờiGian + 0,05 × ĐộChuẩnTựTin)
* ĐộThayĐổi = 0,25 × HệSốĐộKhó × (MụcTiêuBằngChứng − MứcThànhThạoHiệnTại)
* MứcThànhThạoMới = GiớiHạn(MứcThànhThạo + TrọngSốSuyLuận × ĐộThayĐổi, 0, 100)

*Ý nghĩa: Chất lượng suy luận có ảnh hưởng lớn nhất. Đúng đáp án nhưng suy luận kém chỉ tăng ít. Sai đáp án nhưng suy luận tốt vẫn có thể ghi nhận thành thạo một phần.*

**c) Điểm rủi ro (Risk Score):**

* ĐiểmDựĐoán = 10 × TrungBìnhThànhThạoTheoChủĐề / 100
* KhoảngCáchĐiểm = GiớiHạn((ĐiểmMụcTiêu − ĐiểmDựĐoán) / 10, 0, 1)
* ÁpLựcThờiGian = 1 − GiớiHạn(SốNgàyCòn / 180, 0, 1)
* ĐiểmRủiRo = LàmTròn(100 × KhoảngCáchĐiểm × (0,70 + 0,30 × ÁpLựcThờiGian), 2)

**d) Cơ hội cải thiện (Opportunity Gap):**

Chỉ kích hoạt khi học sinh có ít nhất 3 bài nộp trong môn:

* KỳVọngCảiThiện = (1 − MứcThànhThạo/100) × ĐộQuanTrọngThi
* KhảNăngThànhThạo = GiớiHạn(0,20 + 0,60 × TBSuyLuậnGầnĐây + 0,20 × ĐộSẵnSàngTiênQuyết, 0, 1)
* CơHộiThô = KỳVọngCảiThiện × KhảNăngThànhThạo / Max(SốGiờHọcDựKiến, 0,5)

## 9. PHÂN QUYỀN ĐỘNG TRONG TỪNG TRUNG TÂM

**Công thức dễ nhớ:** Quyền truy cập cuối cùng = Quyền chức năng phù hợp + Đúng trung tâm + Đúng phạm vi dữ liệu phụ trách.

### 9.1. Dữ liệu phân quyền dự kiến

| Bảng | Ý nghĩa dễ hiểu |
| --- | --- |
| permissions | Danh mục quyền chức năng mà mã nguồn thực sự hỗ trợ. |
| permission\_account\_types | Quy định quyền nào được phép dùng cho Học sinh, Giáo viên hoặc Quản lý trung tâm. |
| roles | Vai trò tùy chỉnh được tạo trong từng trung tâm. |
| role\_permissions | Một vai trò gồm những quyền nào. |
| user\_roles | Một tài khoản đang được gán những vai trò nào. |
| authorization\_audit\_logs | Nhật ký các lần tạo/sửa vai trò và cấp/thu hồi quyền. |

### 9.2. Chống tự khóa quyền quản trị

Sau mỗi thay đổi quyền, hệ thống phải bảo đảm còn ít nhất một Quản lý trung tâm đang hoạt động và vẫn giữ đủ tập quyền quản trị cốt lõi. Nếu một thao tác làm trung tâm không còn ai có khả năng quản lý vai trò/quyền, giao dịch phải bị từ chối.

## 10. KIẾN TRÚC VÀ CÔNG NGHỆ

### 10.1. Luồng xử lý chính của một yêu cầu

| Bước | Thành phần | Công nghệ | Vai trò |
| --- | --- | --- | --- |
| **1** | Giao diện web | React/Vite | Hiển thị màn hình, lấy dữ liệu người dùng nhập và gửi yêu cầu đến máy chủ. |
| **2** | Cổng dịch vụ web | ASP.NET Core Web API | Nhận yêu cầu HTTP, xác thực người dùng, ánh xạ dữ liệu đầu vào/đầu ra. |
| **3** | Tầng xử lý nghiệp vụ | .NET 10 | Kiểm tra quyền, quy tắc trung tâm, quy tắc nghiệp vụ, giao dịch dữ liệu và thuật toán học tập. |
| **4** | Tầng truy cập dữ liệu | Entity Framework Core 10 | Làm việc với entity, truy vấn, migration và ánh xạ dữ liệu. |
| **5** | Cơ sở dữ liệu | MySQL 8 / InnoDB | Lưu dữ liệu và bảo vệ quan hệ bằng khóa, ràng buộc, chỉ mục và giao dịch. |

### 10.2. Dịch vụ AI nằm ở đâu?

AI không nằm trên đường bắt buộc của mọi yêu cầu. Khi học sinh nộp bài, bài nộp được lưu trước. Sau đó một công việc nền mới gọi dịch vụ AI để phân tích. Vì vậy lỗi AI không làm mất bài nộp và cũng không làm các chức năng quản lý khác ngừng hoạt động.

### 10.3. Nguyên tắc phụ thuộc

* Giao diện không tự quyết định quyền cuối cùng; máy chủ luôn phải kiểm tra lại.
* Bộ điều khiển API không gọi AI trực tiếp.
* Tầng dữ liệu không chứa logic AI.
* Quy tắc học tập và phân quyền nằm ở tầng xử lý nghiệp vụ để có thể kiểm thử riêng.

## 11. CƠ SỞ DỮ LIỆU

Cơ sở dữ liệu hiện có **39 bảng** trong migration (bao gồm 7 bảng phân quyền động và kiểm soát bằng chứng đã được triển khai). Thiết kế đích có **40 bảng** sau khi bổ sung bảng attempt\_attachments cho minh chứng đa phương thức (bảng vẽ nháp vector).

### 11.1. Nhìn cơ sở dữ liệu theo 5 cụm nghiệp vụ

| Cụm | Bảng chính | Ý nghĩa |
| --- | --- | --- |
| **Tổ chức và tài khoản** | centers, users, refresh\_tokens, teachers, students, subjects, classes, class\_students | Quản lý trung tâm, tài khoản, hồ sơ, lớp và môn. |
| **Kiến thức, chương trình và câu hỏi** | knowledge\_nodes, knowledge\_edges, curriculums, curriculum\_classes, curriculum\_nodes, questions, question\_options, question\_knowledge\_nodes | Mô tả cấu trúc kiến thức, chương trình học và ngân hàng câu hỏi. |
| **Bài tập và đánh giá** | assignments, assignment\_questions, assignment\_targets, student\_assignment\_progress, attempts, reasoning\_analyses, ai\_analysis\_jobs | Giao bài, nộp bài, chấm sơ bộ, xử lý AI và lưu kết quả phân tích. |
| **Hồ sơ học tập và gợi ý** | student\_subject\_goals, student\_twins, knowledge\_twins, behavior\_twins, twin\_update\_history, learning\_paths, learning\_path\_items, recommendations | Theo dõi năng lực, hành vi, mục tiêu, lịch sử và hành động học tiếp theo. |
| **Phân quyền động và bằng chứng** | permissions, permission\_account\_types, roles, role\_permissions, user\_roles, authorization\_audit\_logs, evidence\_assessments | Cho phép phân quyền linh hoạt và lưu quyết định của Bộ kiểm soát bằng chứng. |
| **Minh chứng đa phương thức — mục tiêu** | attempt\_attachments | Lưu ảnh bảng vẽ nháp vector của học sinh kèm bài nộp (bảng thứ 40, chưa migration). |

### 11.2. Các ràng buộc dữ liệu quan trọng

* Mọi bảng thuộc một trung tâm phải có center\_id và ràng buộc ngăn tham chiếu chéo trung tâm.
* Quan hệ duy nhất, khóa ngoại và các miền giá trị quan trọng phải được bảo vệ tại cơ sở dữ liệu.
* Knowledge Graph không được có cạnh trỏ về chính nó và không được tạo vòng lặp ở quan hệ kiến thức tiên quyết.
* Bài nộp, công việc AI, bằng chứng và cập nhật hồ sơ học tập phải dùng giao dịch dữ liệu và cơ chế chống gửi/ghi lặp phù hợp.
* Các bảng lịch sử quan trọng được lưu theo kiểu chỉ thêm mới để bảo đảm khả năng truy vết.

## 12. BẢO MẬT VÀ CÁC YÊU CẦU PHI CHỨC NĂNG

| Nhóm yêu cầu | Diễn giải |
| --- | --- |
| **Bảo mật** | Mặc định từ chối nếu thiếu quyền; kiểm tra đúng trung tâm và phạm vi tài nguyên; mật khẩu được băm; phiên đăng nhập có cơ chế thu hồi. |
| **Toàn vẹn dữ liệu** | Khóa ngoại, khóa duy nhất, kiểm tra miền giá trị, giao dịch dữ liệu, chống ghi đè và chống gửi lặp. |
| **Khả dụng** | Luồng cốt lõi không phụ thuộc AI; số lần thử lại có giới hạn; có cơ chế dự phòng rõ ràng. |
| **Hiệu năng** | Phân trang, truy vấn chỉ lấy dữ liệu cần thiết, tránh truy vấn lặp N+1, thiết kế chỉ mục phù hợp với màn hình chính. |
| **Riêng tư** | Không đưa mật khẩu, khóa API hoặc dữ liệu cá nhân không cần thiết vào log và prompt AI. |
| **Khả năng giải thích** | Có thể chỉ ra bằng chứng nào đã làm thay đổi Hồ sơ học tập số và vì sao hệ thống khuyến nghị một chủ đề. |
| **Dễ sử dụng** | Giao diện có trạng thái đang tải, rỗng, lỗi, không có quyền; thông báo rõ ràng khi AI đang chờ hoặc không khả dụng. |
| **Dễ bảo trì** | Tách giao diện, nghiệp vụ, dữ liệu và AI; mỗi module có trách nhiệm rõ; migration đi theo hướng nâng cấp dần. |

## 13. TRẠNG THÁI HIỆN TẠI VÀ PHẦN CÒN PHẢI HOÀN THIỆN

| Lĩnh vực | Hiện trạng | Cần làm |
| --- | --- | --- |
| **Xác thực và trung tâm** | Đã có nền mạnh | Bổ sung kiểm tra phiên quyền cũ và payload quyền động. |
| **Quản lý giáo viên/học sinh/lớp/môn** | Đã có đáng kể | Hoàn thiện giao diện và chuyển sang kiểm tra capability. |
| **Knowledge Graph và Curriculum** | Đã có đáng kể | Hoàn thiện tích hợp và kiểm tra phân quyền mới. |
| **Question Bank và Assignment** | Đang phát triển | Hoàn thiện toàn bộ luồng tạo/giao bài và giao diện. |
| **Nộp bài và AI job** | Đã có nền | Hoàn thiện luồng cuối, Evidence Gate và các trạng thái giao diện. 5 trạng thái Attempt chính thức: PendingAnalysis, Processing, Completed, NeedsTeacherReview, AnalysisFailed. |
| **Phân quyền động** | Đã triển khai migration + API + giao diện | Hoàn thiện kiểm thử bảo mật và cutover legacy. |
| **Quản trị nền tảng (PlatformAdmin)** | Đã phê duyệt, đang triển khai | Root Tenant PLATFORM, API quản lý vòng đời trung tâm. |
| **Bộ công cụ toán học và minh chứng đa phương thức** | Đã phê duyệt, đang triển khai | Math Toolbar, Calculator, Scratchpad, bảng attempt\_attachments. |
| **Hồ sơ học tập số** | Có entity/thuật toán rời rạc | Nối thành quy trình cập nhật nguyên tử có lịch sử. |
| **Khuyến nghị** | Có nền; ML.NET NO-GO chính thức | Giữ heuristic opportunity-v1; hoàn thiện giải thích và orchestration. |
| **Dashboard** | Một phần | Hoàn thiện theo Học sinh/Giáo viên/Quản lý trung tâm. |

**Ba thay đổi kiến trúc quan trọng:**

1. Bỏ phụ thuộc vào role cứng để chuyển sang quyền chức năng động.

2. Không cho kết quả AI đi thẳng vào mức độ thành thạo; phải qua Bộ kiểm soát bằng chứng.

3. Bổ sung PlatformAdmin cô lập trong Root Tenant PLATFORM để quản lý vòng đời trung tâm mà không truy cập dữ liệu học thuật.

## 14. LỘ TRÌNH HOÀN THIỆN HỆ THỐNG

| Giai đoạn | Mục tiêu | Kết quả mong muốn |
| --- | --- | --- |
| **R00** | Chốt đặc tả và baseline | Đồng bộ tài liệu, phạm vi và trạng thái hiện tại. |
| **R01** | Kiểm tra cơ sở dữ liệu thực | Đối chiếu schema MySQL, ràng buộc, chỉ mục và test tích hợp. |
| **R02** | Củng cố nền bảo mật | Kiểm tra phiên quyền, lỗi chung, cách ly trung tâm và quan sát hệ thống. |
| **R03** | Phân quyền động | Migration + nghiệp vụ + API + giao diện + kiểm thử bảo mật. |
| **R04** | Hoàn thiện nội dung | Knowledge Graph, Curriculum và Question Bank. |
| **R05** | Hoàn thiện giao/nộp bài | Lifecycle, người nhận, chống gửi lặp, chấm sơ bộ và AI job. |
| **R06** | Quản trị bằng chứng | AI adapter, dự phòng, Evidence Gate, review và giáo viên xác nhận. |
| **R07** | Hoàn thiện Hồ sơ học tập số | Cập nhật Knowledge/Behavior, lịch sử, cạnh tranh ghi và replay. |
| **R08** | Khuyến nghị và dashboard | Opportunity Gap, lời giải thích và ba giao diện tổng hợp. |
| **R09** | Kiểm thử cuối và demo | Bảo mật, hiệu năng, E2E, triển khai, báo cáo và kịch bản trình bày. |

## 15. PHÂN CHIA CÔNG VIỆC CHO NHÓM 5 NGƯỜI

| Thành viên | Phần chính | Phối hợp/review |
| --- | --- | --- |
| **Tuấn Tài** | Quản trị dự án, xác thực/phân quyền backend, tích hợp và phát hành | Review giao diện quyền, migration và pipeline Evidence. |
| **Thịnh** | UX, giao diện Organization và màn hình quản trị role/quyền | Phối hợp API với Tuấn Tài; hỗ trợ E2E. |
| **Khoa** | Database, migration, audit MySQL, Knowledge Graph và Curriculum | Review ràng buộc dữ liệu và thuật toán. |
| **Thành Tài** | Question Bank, Assignment, Submission và integration test | Phối hợp schema với Khoa và trạng thái Evidence với Sơn. |
| **Sơn** | AI safety, Bộ kiểm soát bằng chứng, Hồ sơ học tập số, Recommendation và đánh giá ML | Bắt buộc review chéo với Khoa/Tuấn Tài; tách package nếu bị block. |

## 16. KIỂM THỬ VÀ TIÊU CHÍ CHẤP NHẬN

| Lớp kiểm thử | Phạm vi | Điều kiện đạt |
| --- | --- | --- |
| **Kiểm thử đơn vị** | Quy tắc validation, phân quyền, Evidence Gate, thuật toán Mastery và Opportunity Gap. | Có test đúng/sai/biên; đặc biệt ngưỡng Evidence và bài tự luận chưa chấm. |
| **Kiểm thử tích hợp MySQL** | Khóa ngoại, unique, check, giao dịch, cạnh tranh ghi và composite FK. | Chạy trên MySQL thật, không chỉ provider InMemory. |
| **Kiểm thử API** | Quyền, trung tâm, ownership, lỗi, hủy request và chống gửi lặp. | Không rò dữ liệu hoặc trả thông tin của trung tâm khác. |
| **Kiểm thử giao diện** | Ẩn/hiện thao tác theo quyền, form, loading/error/empty state. | Giao diện không dùng role label làm security. |
| **Kiểm thử đầu-cuối** | Luồng Học sinh/Giáo viên/Quản lý trung tâm và tình huống AI outage. | Các chức năng cốt lõi vẫn hoàn thành khi AI tắt. |
| **Kiểm thử bảo mật** | Cross-tenant, phiên quyền cũ, last-admin, tự nâng quyền và secret scan. | Mặc định từ chối khi không đủ điều kiện. |

## 17. ĐỐI CHIẾU YÊU CẦU MÔN HỌC

| Yêu cầu | Hiện trạng | Lưu ý |
| --- | --- | --- |
| **C# / .NET / EF Core / LINQ / Web API** | Đã có trong prototype. | Tiếp tục giữ làm stack chính. |
| **Database hơn 20 bảng** | 39 bảng hiện có, thiết kế đích 40. | Đã audit MySQL thật và có integration test. |
| **Có nhiều actor và chức năng rõ** | Có Học sinh, Giáo viên, Quản lý trung tâm và các module nghiệp vụ lớn. | Nên dùng phần đặc tả chức năng trong tài liệu này khi trình bày. |
| **Phân quyền linh hoạt** | Đã triển khai dynamic RBAC và PlatformAdmin. | Hoàn thiện cutover legacy và kiểm thử bảo mật. |
| **GitHub và đóng góp nhóm** | Có prototype/baseline và kế hoạch phân chia. | Khi tạo repo môn học cần import baseline minh bạch rồi mới tính đóng góp mới. |
| **Khảo sát/Figma** | Chưa hoàn tất ở tài liệu hiện tại. | Có thể làm ở repo/course project khi nhóm chính thức bắt đầu nếu giảng viên yêu cầu bằng chứng này. |
| **AI/ML** | AI pipeline và Evidence Gate đã có; ML.NET đã đánh giá chính thức và nhận NO-GO. | Giữ heuristic opportunity-v1 làm baseline; có evaluation protocol để tái đánh giá khi đủ dữ liệu. |

## PHỤ LỤC A. DANH SÁCH 40 BẢNG DỮ LIỆU

| **#** | **Tên bảng** | **Trạng thái** | **Chức năng** |
| --- | --- | --- | --- |
| 1 | **centers** | Hiện có | Trung tâm đào tạo — gốc phân vùng dữ liệu đa tenant (Tenant Root) |
| 2 | **users** | Hiện có | Tài khoản người dùng, loại tài khoản (PlatformAdmin/CenterUser) và phiên bản quyền (auth\_version) |
| 3 | **refresh\_tokens** | Hiện có | Lưu token làm mới phiên đăng nhập (đã băm), xoay vòng và thu hồi phiên |
| 4 | **teachers** | Hiện có | Hồ sơ nghiệp vụ giáo viên |
| 5 | **students** | Hiện có | Hồ sơ nghiệp vụ học sinh |
| 6 | **subjects** | Hiện có | Danh mục môn học theo từng trung tâm |
| 7 | **classes** | Hiện có | Lớp học theo giáo viên, môn học và niên khóa |
| 8 | **class\_students** | Hiện có | Quan hệ thành viên giữa học sinh và lớp học |
| 9 | **knowledge\_nodes** | Hiện có | Cây tri thức: Chương, bài, chủ đề, khái niệm và kỹ năng |
| 10 | **knowledge\_edges** | Hiện có | Quan hệ tiên quyết (prerequisite) và phân cấp giữa các chủ đề tri thức |
| 11 | **curriculums** | Hiện có | Khung chương trình học do giáo viên quản lý |
| 12 | **curriculum\_classes** | Hiện có | Phân công chương trình học áp dụng cho lớp |
| 13 | **curriculum\_nodes** | Hiện có | Thứ tự và trọng số các chủ đề trong chương trình học |
| 14 | **questions** | Hiện có | Ngân hàng câu hỏi, độ khó, tiêu chí chấm và chế độ đánh giá (answer\_evaluation\_mode) |
| 15 | **question\_options** | Hiện có | Các lựa chọn đáp án cho câu hỏi trắc nghiệm |
| 16 | **question\_knowledge\_nodes** | Hiện có | Ánh xạ câu hỏi đánh giá những chủ đề tri thức nào |
| 17 | **assignments** | Hiện có | Bài tập/bài kiểm tra giao cho lớp và vòng đời phát hành |
| 18 | **assignment\_questions** | Hiện có | Danh sách và thứ tự câu hỏi trong từng bài tập |
| 19 | **assignment\_targets** | Hiện có | Danh sách học sinh hoặc nhóm được giao bài tập |
| 20 | **student\_assignment\_progress** | Hiện có | Tiến độ làm bài và tổng điểm bài tập của từng học sinh |
| 21 | **student\_subject\_goals** | Hiện có | Mục tiêu điểm số và thời lượng học tập theo từng môn |
| 22 | **student\_twins** | Hiện có | Gốc tổng hợp Hồ sơ năng lực số (Learning Digital Twin) của học sinh |
| 23 | **knowledge\_twins** | Hiện có | Mức độ thành thạo (mastery) và rủi ro hổng kiến thức theo chủ đề |
| 24 | **behavior\_twins** | Hiện có | Dữ liệu vi mô về hành vi học tập (tốc độ, độ bền, tần suất ôn tập) |
| 25 | **twin\_update\_history** | Hiện có | Nhật ký lịch sử cập nhật hồ sơ Digital Twin (append-only) |
| 26 | **learning\_paths** | Hiện có | Phiên bản lộ trình học tập cá nhân hóa được sinh tự động |
| 27 | **learning\_path\_items** | Hiện có | Các bước học tập chi tiết (chủ đề, câu hỏi gợi ý) trong lộ trình |
| 28 | **recommendations** | Hiện có | Gợi ý hành động học tập tiếp theo kèm giải trình chi tiết |
| 29 | **recommendation\_generation\_states** | Hiện có | Watermark bền vững chống trùng lặp trigger sinh lộ trình/gợi ý |
| 30 | **attempts** | Hiện có | Lượt làm bài nộp, đáp án, thời gian làm bài và điểm sơ bộ |
| 31 | **reasoning\_analyses** | Hiện có | Phân tích tư duy (AI reasoning/rule fallback) và chấm đè của giáo viên |
| 32 | **ai\_analysis\_jobs** | Hiện có | Hàng đợi xử lý nền AI, phân bổ lease, số lần retry và trạng thái |
| 33 | **permissions** | Hiện có | Danh mục quyền hạn chức năng toàn hệ thống (System catalog) |
| 34 | **permission\_account\_types** | Hiện có | Ràng buộc quyền tương thích với từng loại tài khoản |
| 35 | **roles** | Hiện có | Vai trò người dùng động phân quyền theo từng trung tâm |
| 36 | **role\_permissions** | Hiện có | Danh sách quyền hạn được gán cho từng vai trò |
| 37 | **user\_roles** | Hiện có | Vai trò hiện hành được phân bổ cho từng tài khoản người dùng |
| 38 | **authorization\_audit\_logs** | Hiện có | Nhật ký bất biến ghi nhận mọi thay đổi phân quyền hệ thống |
| 39 | **evidence\_assessments** | Hiện có | Quyết định đánh giá độ tin cậy của minh chứng trước khi cập nhật Twin |
| 40 | **attempt\_attachments** | Hiện có | Lưu trữ minh chứng bảng vẽ nháp vector đính kèm lượt nộp bài |

## PHỤ LỤC B. CÁC TÌNH HUỐNG MINH HỌA DỄ HIỂU

### B.1. Học sinh làm đúng nhưng thiếu tự tin

Học sinh chọn đúng đáp án nhưng mức tự tin rất thấp và thay đổi đáp án nhiều lần. Hệ thống vẫn ghi nhận kết quả đúng, đồng thời Behavior Twin cho thấy tín hiệu thiếu tự tin. Giáo viên có thể dùng thông tin này để hỗ trợ cách kiểm tra và tự đánh giá của học sinh, thay vì coi em đã hoàn toàn nắm chắc kiến thức.

### B.2. AI tự tin nhưng phân tích mâu thuẫn

AI trả mức tự tin cao nhưng nội dung lại mâu thuẫn với đáp án và điểm sơ bộ. Hệ thống không tự động tin kết quả đó. Bộ kiểm soát bằng chứng hạ mức tin cậy hoặc chuyển sang Chờ giáo viên trước khi cho phép ảnh hưởng đến Hồ sơ học tập số.

### B.3. AI mất kết nối

Học sinh nộp bài lúc AI đang lỗi. Bài nộp vẫn được lưu, điểm sơ bộ vẫn hiển thị nếu câu hỏi có thể chấm tự động, dữ liệu thời gian và mức tự tin vẫn được ghi. Chỉ phần phân tích suy luận chuyển sang trạng thái chờ giáo viên.

### B.4. Giáo viên xác nhận bài tự luận

Bài tự luận chưa có kết luận đúng/sai nên không làm thay đổi mức độ thành thạo. Sau khi giáo viên chấm và xác nhận chất lượng cách làm, hệ thống tạo một bằng chứng mới và tính lại trạng thái học tập từ thời điểm liên quan.

### B.5. Quản lý tạo vai trò mới

Trung tâm muốn có người chuyên duyệt câu hỏi nhưng không muốn người đó quản lý tài khoản. Quản lý tạo vai trò 'Người duyệt nội dung', chỉ cấp các quyền đọc/sửa/duyệt nội dung. Giáo viên được gán vai trò này vẫn là Giáo viên về nghiệp vụ, chỉ có thêm đúng các quyền cần thiết.

## PHỤ LỤC C. NGUỒN TÀI LIỆU KỸ THUẬT

| Tài liệu | Nội dung |
| --- | --- |
| PROJECT\_REQUIREMENTS.md | Yêu cầu, use case và tiêu chí chấp nhận chi tiết. |
| DATABASE\_SCHEMA.md | Toàn bộ cột, kiểu dữ liệu, khóa, ràng buộc và chỉ mục. |
| API\_CONTRACTS.md | Endpoint, request/response, lỗi và quyền API. |
| UI\_UX\_SPEC.md | Màn hình và trạng thái giao diện. |
| CONSTITUTION.md | Nguyên tắc kiến trúc và bảo mật không được vi phạm. |
| MASTER\_PLAN.md | Thứ tự triển khai. |
| PROJECT\_TRACKING.md / TEAM\_ASSIGNMENT.md | Tiến độ và phân công. |
| CODEBASE\_CHANGE\_PLAN.md | Khoảng cách từ source hiện tại tới kiến trúc đích. |
| docs/decisions/ADR-POST-R08-PLATFORM.md | Quyết định kiến trúc: Quản trị nền tảng, Root Tenant PLATFORM và quản lý vòng đời trung tâm. |
| docs/decisions/ADR-POST-R08-MATH.md | Quyết định kiến trúc: Bộ công cụ toán học, minh chứng đa phương thức, Scope Guard và xử lý lỗi lưu trữ. |
| docs/decisions/DEC-011-ML-Feasibility-Record.md | Quyết định NO-GO chính thức cho ML.NET tại R07; giữ heuristic opportunity-v1. |
| docs/evaluation/R07-ML-Evaluation-Protocol.md | Quy trình đánh giá data-readiness và metric cho ML; dùng khi tái đánh giá trong tương lai. |
|  |  |

**Lưu ý cuối:** Bản tài liệu này ưu tiên khả năng đọc và trình bày. Khi cần chi tiết kỹ thuật đến mức cột database, endpoint hoặc file source, hãy dùng các tài liệu Markdown chuyên môn tương ứng để tránh tạo hai nguồn sự thật.