# Adaptive Practice E2E – 2026-10-25

Branch: `origin/SkinnySnail/adaptive-practice`. HEAD: `dd47b89dc5133022f7cb298c9bdf0adde5dbab2a`.

**BLOCKED: chưa thể thực thi E2E trên source hiện tại vì build Docker thất bại.**

| Kiểm tra điều kiện chạy | Kết quả | Lỗi biên dịch phân biệt | Log |
|---|---|---:|---|
| BUILD_API | FAIL (exit 1) | 33 | [Log](api-build.txt) |
| BUILD_WEB | FAIL (exit 1) | 3 | [Log](web-build.txt) |

Đã chạy **0 test chức năng**. 36 ca HTTP/MySQL và 12 ca browser BLOCKED. Lỗi build là lỗi điều kiện khởi chạy, không được tính thành FAIL hay PASS cho các luồng chức năng.

| Mã | Loại | Kịch bản | Kết quả |
|---|---|---|---|
| APAPI01 | HTTP/MySQL integration | Unauthenticated adaptive access is denied | BLOCKED |
| APAPI02 | HTTP/MySQL integration | Student login in isolated schema | BLOCKED |
| APAPI03 | HTTP/MySQL integration | Second student login in isolated schema | BLOCKED |
| APAPI04 | HTTP/MySQL integration | Teacher login in isolated schema | BLOCKED |
| APAPI05 | HTTP/MySQL integration | Other-center student login in isolated schema | BLOCKED |
| APAPI06 | HTTP/MySQL integration | Weakness proposal explains target without issuing questions | BLOCKED |
| APAPI07 | HTTP/MySQL integration | Learning Path mode returns a meaningful proposal | BLOCKED |
| APAPI08 | HTTP/MySQL integration | Custom topic mode preserves selected topic | BLOCKED |
| APAPI09 | HTTP/MySQL integration | Adaptive persistence tables exist in isolated schema | BLOCKED |
| APAPI10 | HTTP/MySQL integration | Start a ten-minute session | BLOCKED |
| APAPI11 | HTTP/MySQL integration | Ten-minute session has four/eight question limits | BLOCKED |
| APAPI12 | HTTP/MySQL integration | Repeated start returns the active session | BLOCKED |
| APAPI13 | HTTP/MySQL integration | First question belongs to the selected session topic | BLOCKED |
| APAPI14 | HTTP/MySQL integration | Repeated next request preserves unanswered question identity | BLOCKED |
| APAPI15 | HTTP/MySQL integration | Progressive hint is persisted on issued item | BLOCKED |
| APAPI16 | HTTP/MySQL integration | Second student cannot read another session | BLOCKED |
| APAPI17 | HTTP/MySQL integration | Second student cannot request another students hint | BLOCKED |
| APAPI18 | HTTP/MySQL integration | Pause persists Paused state | BLOCKED |
| APAPI19 | HTTP/MySQL integration | Paused session does not issue a new question | BLOCKED |
| APAPI20 | HTTP/MySQL integration | Resume preserves the current question | BLOCKED |
| APAPI21 | HTTP/MySQL integration | Submit stores one graded answer and updates session progress | BLOCKED |
| APAPI22 | HTTP/MySQL integration | Same-submission replay does not duplicate attempt or evidence | BLOCKED |
| APAPI23 | HTTP/MySQL integration | Session progress remains one answer after replay | BLOCKED |
| APAPI24 | HTTP/MySQL integration | Voluntary close produces an incomplete-goal summary | BLOCKED |
| APAPI25 | HTTP/MySQL integration | Summary can be retrieved after close | BLOCKED |
| APAPI26 | HTTP/MySQL integration | Second student cannot read another students summary | BLOCKED |
| APAPI27 | HTTP/MySQL integration | Closed session appears in student history | BLOCKED |
| APAPI28 | HTTP/MySQL integration | Closed session cannot issue another question | BLOCKED |
| APAPI29 | HTTP/MySQL integration | Closed session cannot accept a new answer | BLOCKED |
| APAPI30 | HTTP/MySQL integration | Follow-up practice creates a distinct session and preserves history | BLOCKED |
| APAPI31 | HTTP/MySQL integration | Fifteen-minute session has five/twelve limits | BLOCKED |
| APAPI32 | HTTP/MySQL integration | Twenty-minute session has six/fifteen limits | BLOCKED |
| APAPI33 | HTTP/MySQL integration | Teacher cannot see sessions through an unrelated class scope | BLOCKED |
| APAPI34 | HTTP/MySQL integration | Student cannot use teacher monitoring endpoint | BLOCKED |
| APAPI35 | HTTP/MySQL integration | Other-center student cannot read session or summary | BLOCKED |
| APAPI36 | HTTP/MySQL integration | Teacher cannot create a student practice session | BLOCKED |
| APWEB01 | Browser E2E | Login and open adaptive overview; show a goal before a question | BLOCKED |
| APWEB02 | Browser E2E | Switch Weakness, LearningPath and CustomTopic modes | BLOCKED |
| APWEB03 | Browser E2E | Select subject, topic and 10/15/20 minute budget; start session | BLOCKED |
| APWEB04 | Browser E2E | Question, progress, selection reason and answer controls render | BLOCKED |
| APWEB05 | Browser E2E | Request progressive hints and verify support usage | BLOCKED |
| APWEB06 | Browser E2E | Submit an answer; verify graded feedback and next question | BLOCKED |
| APWEB07 | Browser E2E | Pause, reload, resume and retain question/progress/timer | BLOCKED |
| APWEB08 | Browser E2E | Voluntarily stop and distinguish incomplete from achieved | BLOCKED |
| APWEB09 | Browser E2E | Summary counts, skill evaluation and next action match API data | BLOCKED |
| APWEB10 | Browser E2E | Create follow-up session and retain linked prior history | BLOCKED |
| APWEB11 | Browser E2E | Reopen history and a closed session without issuing new questions | BLOCKED |
| APWEB12 | Browser E2E | Teacher sees practice monitoring within the assigned class | BLOCKED |

Không tạo schema test hay ghi dữ liệu học sinh. Không dùng image cũ để suy ra kết quả của HEAD này.

Sau khi sửa build: chạy gate lại, khởi tạo acceptance stack bằng đúng image vừa build, chạy API suite rồi kiểm tra trình duyệt theo README. API suite là kiểm tra HTTP/MySQL; chưa thay thế browser E2E.

[Chi tiết nguyên nhân và cách chạy lại](README.md) · [Kết quả JSON](e2e-results.json)
