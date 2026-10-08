# Kiểm thử Chrome: học sinh thứ 3, bài 50 câu — 07/10/2026

## Phạm vi

Làm và nộp đúng một bài `Test 50 câu` bằng giao diện Chrome của tài khoản
học sinh đã được người dùng đăng nhập. Dùng đáp án đúng, sai, gần đúng,
diễn đạt tương đương, ví dụ khác lời giải mẫu, và đáp án đúng nhưng lập luận sai.
Đáp án môn Tiếng Anh có thể là tiếng Anh; mọi lập luận nhập thêm đều bằng tiếng Việt.
Không đính kèm ảnh nháp trong lượt đo này. Không chấm lại bài cũ, không duyệt/chốt
thay giáo viên, không sửa database hay mã xử lý AI trong khi đo.

## Kết quả thời gian

Các mốc server bên dưới dùng UTC, được đọc từ database; đây không phải thời gian
ước lượng từ spinner hoặc thời gian giáo viên duyệt.

| Mốc | UTC |
|---|---|
| Bấm xác nhận nộp bài trên Chrome | 2026-10-07 15:23:18.684 |
| Server lưu 50 bài làm | 2026-10-07 15:23:19.454812 |
| Phân tích AI cuối cùng được lưu | 2026-10-07 15:24:41.423448 |
| Job AI cuối cùng hoàn tất | 2026-10-07 15:24:41.429341 |
| Nhận xét tổng bài được tạo | 2026-10-07 15:24:43.299824 |
| Quan sát Chrome hiển thị AI đã phân tích 50/50 | 2026-10-07 15:25:14.054 |

- Server nhận bài → tất cả job AI hoàn tất: **81,974529 giây**, khoảng **1 phút 22 giây**.
- Server nhận bài → có nhận xét tổng bài: **83,845012 giây**.
- Bấm xác nhận → tất cả job hoàn tất: khoảng **82,75 giây**.
- Mốc quan sát Chrome chỉ là thời điểm kiểm tra, không phải mốc UI chuyển trạng thái
  chính xác, vì không lấy mẫu màn hình liên tục.

Lượt học sinh thứ 2 trước sửa mất 190,115543 giây. Lượt này ngắn hơn khoảng
**57%** (108,14 giây). Cùng bài 50 câu nhưng một số đáp án/lập luận đã đổi;
đây là một phép đo thực tế, không phải SLA hay benchmark có kiểm soát nhiều lần.

## Request, retry và hoàn tất pipeline

- Model: `gemini-3.5-flash-lite`; profile `vietnamese-grade-proposal-v4`.
- **22 request/batch Gemini**: 17 batch ba câu và 5 batch một câu, tính cả xử lý lại.
- Tổng bộ đếm local của bảy pool tăng **64 → 86**, khớp 22 request.
- Log có hai batch Partial do ba item vi phạm quy tắc `VietnameseExplanation`;
  có một batch Failed sau khoảng 30 giây. Các câu liên quan được xử lý lại thành công.
- Không ghi nhận log lỗi quota/credential của lượt này. Không dùng lỗi ngôn ngữ
  hoặc timeout để suy ra key đã hết quota.
- **50 job Completed; 50 phân tích Gemini; 0 fallback; 0 job còn Pending/Processing.**
- **50 bản ghi bằng chứng và 50 lịch sử cập nhật Twin**, mỗi lượt nộp có đúng một bản ghi.
- Không còn checkpoint dở dang. Những kết quả AI trả về trước được giữ checkpoint
  trong lúc chờ bằng chứng trước đó để cập nhật Twin theo thứ tự, không gửi lại chỉ vì chờ.
- Cả 50 cặp feedback/AI solution có nội dung tiếng Việt theo kiểm tra ký tự và
  đã vượt qua kiểm tra ngôn ngữ của pipeline; phần đáp án Tiếng Anh vẫn được giữ.
- Chrome đã hết banner đang phân tích, hiện **AI đã phân tích 50/50**, chế độ chỉ đọc,
  không còn nút nộp bài và không hiện thông báo AI tạm thời không khả dụng.

So với lượt trước: 32 request/20 singleton → **22 request/5 singleton**.
Năm singleton của lượt này gồm xử lý lỗi riêng và phần dư; không thể kết luận
gom batch thất bại chỉ vì có batch một câu.

## Các trường hợp trọng tâm

Điểm đáp án và chỉ số chất lượng lập luận là hai đánh giá riêng. Điểm trong bảng
đã quy đổi về thang 10; chỉ số lập luận vẫn là thang 100.

| Câu | Nội dung thử | Kết quả ghi nhận |
|---|---|---|
| 1 | Diễn đạt lại nghĩa sustainable | Correct / Valid, đề xuất 10/10 |
| 2 | Conserve: rút sạc, tắt thiết bị chờ, khác ví dụ LED mẫu | Correct / Valid, rubric 4+4+2=10/10, lập luận 100/100; không fallback |
| 3, 13, 37, 47, 50 | Từ đúng nhưng đổi chữ hoa/thường | Correct / Valid |
| 4 | Ghi healthy nhưng lập luận nhận ra cần health | Đáp án Incorrect / 0; AI nhận ra phần quy tắc đúng, chỉ số lập luận 50/100 |
| 7, 9, 14, 17 | Chọn từ/đại từ sai và giải thích sai | Incorrect / Invalid |
| 8 | Chọn conserve đúng nhưng nói tắt đèn tạo điện | Correct / Invalid; giữ điểm đáp án 10/10, lập luận 50/100 |
| 10 | Rút gọn who live → living và dùng in the vicinity of | Correct / Valid, đề xuất 10/10; ghi nhận cách diễn đạt khác |
| 15 | Chọn whom đúng nhưng gọi nó là đại từ chủ ngữ | Correct / Invalid; giữ điểm đáp án 10/10, lập luận 70/100 |
| 20 | Hiểu sai was cooking là việc đã hoàn tất trước phone rang | Incorrect / Invalid, rubric đề xuất 2/10 |
| 21 | Dùng have been living, đúng nghĩa và yêu cầu đề | Correct / Valid, đề xuất 10/10 |
| 23 | Dùng began sau had | Incorrect / Invalid |
| 25 | Viết writen thiếu một t | Incorrect / Invalid, chỉ số lập luận 50/100; điểm đáp án 0 theo quy tắc từ vựng |
| 28 | Dùng have bought trong trình tự quá khứ | Incorrect / Invalid |
| 30 | Chọn was studying đúng nhưng nói cứ last night là tiếp diễn | Correct / Invalid; giữ điểm đáp án 10/10, lập luận 60/100 |
| 31, 36, 43 | Sai mốc thì/chủ ngữ/mệnh đề không xác định | Incorrect / Invalid |
| 39 | Has worked đúng nghĩa nhưng đề yêu cầu Present Perfect Continuous | Incorrect / Invalid, lập luận 40/100; không coi khác cấu trúc là đúng khi trái yêu cầu riêng của đề |
| 49 | Giải thích mitigate và dùng ví dụ flood barriers mới | Correct / Valid, đề xuất 10/10 |

Tổng kết phân loại AI: **34 Correct/Valid, 3 Correct/Invalid,
12 Incorrect/Invalid, 1 Incorrect/Valid**. Câu Incorrect/Valid là câu 4:
phần quy tắc đã nêu đúng nhưng đáp án học sinh nhập vẫn sai; chỉ số lập luận không phải 100.

## 14 câu chờ giáo viên không phải AI chưa chấm

- 9 câu tự luận: 1, 2, 10, 11, 20, 21, 40, 45, 49; AI đã đề xuất điểm,
  giáo viên vẫn duyệt điểm theo quy trình đã thống nhất.
- 3 câu đáp án đúng nhưng lập luận sai: 8, 15, 30; bằng chứng ghi
  `CORRECT_ANSWER_INVALID_REASONING` và `CONTRADICTION_DETECTED`.
- 2 câu: 44, 50; bằng chứng ghi `ANOMALY_UNREALISTIC_COMPLETION_TIME`.
  Đây là tác động của việc nhập bài tự động rất nhanh, không phải lỗi quota hoặc
  việc AI không chấm được. Không tắt hàng rào này trong quá trình benchmark.

Chrome hiển thị điểm **sơ bộ 5,8/10**, 29/50 câu đã được xác định đúng,
36/50 đã có điểm chính thức và 14 câu chờ duyệt. Đây chưa phải điểm cuối bài.
Các câu đúng có điểm AI đề xuất nhưng chưa được giáo viên duyệt không được
coi là đã có điểm chính thức.

## Ảnh dẫn chứng local

Trong thư mục bị git-ignore:
`storage/verification/ai-grading-50-student3-2026-10-07/`

- `summary.jpg`: tổng quan 50/50 AI hoàn tất, điểm sơ bộ, 14 câu chờ duyệt.
- `question-2.jpg`: ví dụ conserve khác đáp án mẫu, rubric và nhận xét tiếng Việt.
- `question-15.jpg`: đáp án đúng, lập luận chủ ngữ/tân ngữ sai.
- `question-30.jpg`: đáp án đúng, quy tắc last night máy móc bị chỉ ra.

Không lưu key hoặc lỗi SDK thô vào báo cáo. Không push trong lượt kiểm thử này.
