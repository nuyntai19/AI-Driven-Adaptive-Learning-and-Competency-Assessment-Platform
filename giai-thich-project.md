# Giải thích dự án EduTwin

## 1. Dự án này làm về cái gì?

EduTwin là một đồ án xây dựng nền tảng giáo dục thông minh dành cho các trung tâm giáo dục THPT. Mục tiêu của hệ thống là giúp theo dõi quá trình học tập của học sinh một cách sâu sắc hơn, không chỉ dừng ở điểm số.

Nói ngắn gọn, đây là một hệ thống giúp:

- Quản lý trung tâm, giáo viên, học sinh và lớp học.
- Giáo viên giao bài cho học sinh.
- Học sinh làm bài và nhập cả quá trình giải thích lý do cho đáp án.
- AI phân tích cách học, cách trả lời và mức độ hiểu bài của học sinh.
- Từ đó đề xuất nội dung học tiếp theo, chủ đề cần ôn tập và các hành động hỗ trợ phù hợp.

## 2. Ý tưởng cốt lõi của đồ án

Dự án không chỉ là một hệ thống quản lý học tập thông thường. Nó hướng tới việc xây dựng một “bản sao số hóa” của quá trình học của từng học sinh, gọi là Learning Digital Twin.

Điều này có nghĩa là hệ thống cố gắng hiểu:

- Học sinh đã hiểu bài đến đâu.
- Em đang gặp khó ở đâu.
- Em có đang làm sai vì thiếu kiến thức, hay vì trình bày không rõ, hay vì tư duy còn yếu.
- Cần ôn lại chủ đề nào để cải thiện hiệu quả nhất.

## 3. Các chức năng chính

### 3.1. Quản lý tổ chức

Hệ thống hỗ trợ quản lý:

- Trung tâm giáo dục.
- Người dùng và vai trò như học sinh, giáo viên, quản lý trung tâm.
- Lớp học và thành viên trong lớp.

### 3.2. Quản lý nội dung học tập

Giáo viên có thể tạo:

- Môn học.
- Cấu trúc kiến thức theo các mức như chương, chủ đề, kỹ năng, khái niệm.
- Câu hỏi và bài tập.
- Bài assignment cho lớp hoặc nhóm học sinh.

### 3.3. Học sinh làm bài

Khi học sinh làm bài, họ không chỉ chọn đáp án mà còn có thể nhập reasoning_text, tức là giải thích lý do tại sao chọn đáp án đó.

Đây là dữ liệu rất quan trọng vì AI sẽ phân tích cách tư duy của học sinh, không chỉ đúng sai.

### 3.4. AI phân tích và đề xuất

Sau khi học sinh nộp bài:

- AI sẽ phân tích câu trả lời và cách giải thích.
- Hệ thống sẽ cập nhật “Digital Twin” cho học sinh về kiến thức và hành vi học tập.
- Từ đó đưa ra các gợi ý như:
  - Chủ đề nào cần ôn lại.
  - Câu hỏi nào nên làm tiếp.
  - Học sinh nào có nguy cơ học yếu.

### 3.5. Dashboard và theo dõi

Hệ thống cung cấp dashboard cho:

- Học sinh: xem tiến độ, mức độ hiểu bài và các gợi ý học tiếp.
- Giáo viên: theo dõi lớp học, học sinh có nguy cơ thấp năng lực, chủ đề nào đang yếu.
- Quản lý trung tâm: xem tổng quan toàn trung tâm.

## 4. Vì sao đồ án này có ý nghĩa?

Đồ án này không chỉ là “một website học tập”. Nó hướng tới việc chuyển từ việc chấm điểm thô sang việc hiểu sâu về quá trình học của học sinh.

Nó giúp:

- Giáo viên nhìn rõ hơn tình trạng học tập của học sinh.
- Học sinh nhận được gợi ý học tập cá nhân hóa.
- Trung tâm có dữ liệu để đưa ra quyết định giáo dục tốt hơn.

## 5. Công nghệ chính được dùng

Theo các tài liệu trong dự án, hệ thống được thiết kế với các công nghệ chính như:

- Backend: .NET, ASP.NET Core Web API.
- Frontend: React, TypeScript, Vite.
- Database: MySQL.
- AI: Gemini cho phân tích reasoning.
- Hạ tầng: Docker Compose.

## 6. Cách hiểu đơn giản nhất

Nếu tóm tắt theo một câu thì đây là một hệ thống “giáo dục thông minh” giúp:

“Theo dõi cách học của học sinh, phân tích tư duy của các em qua bài làm, và đề xuất những việc nên học tiếp để cải thiện hiệu quả.”

## 7. Tiến độ hiện tại của dự án

Dựa trên `MASTER_PLAN.md` và cấu trúc source code hiện tại, dự án đang ở **cuối P07 — Center Organization Management**. Điều này có nghĩa là các phần nền tảng quan trọng đã được hoàn thành hoặc đi rất xa, đặc biệt là:

- `P00` đến `P05`: đã có nền tảng tài liệu, solution, Docker local và dữ liệu cơ sở.
- `P06`: đã có đăng nhập, tenant isolation và các cơ chế bảo vệ theo trung tâm.
- `P07`: đã có luồng quản lý trung tâm, giáo viên, học sinh và lớp học ở mức backend nghiệp vụ khá đầy đủ.

Các phase phía sau hiện mới ở các mức khác nhau:

- `P08` đến `P10`: mới bắt đầu hoặc mới có schema / contract / nền logic ban đầu.
- `P11` đến `P14`: chưa hoàn thiện luồng analysis job, AI, digital twin và recommendation.
- `P15` đến `P17`: frontend dashboard cho học sinh, giáo viên và quản lý trung tâm chưa hoàn chỉnh.
- `P18`: hardening và release cuối cùng chưa đến giai đoạn chốt.

Nói ngắn gọn, hệ thống hiện đã có “xương sống” tốt, nhưng phần trải nghiệm học tập cá nhân hóa và AI phân tích vẫn là phần cần tiếp tục xây dựng.

## 8. Kết luận

EduTwin là một dự án xây dựng nền tảng học tập thông minh, kết hợp quản lý giáo dục, dữ liệu học tập, AI phân tích và đề xuất cá nhân hóa. Đây là một đồ án phù hợp cho việc nghiên cứu về ứng dụng AI vào giáo dục, đặc biệt là việc sử dụng dữ liệu học tập để tạo ra trải nghiệm học tập tốt hơn cho học sinh.
