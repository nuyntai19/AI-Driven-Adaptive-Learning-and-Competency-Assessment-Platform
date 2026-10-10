SET NAMES utf8mb4;

-- Fresh grade-specific seed only. This script is not a migration for old test
-- submissions. Existing assignment/progress rows are never reset on rerun.

-- 1. Assignment 1: Chua bat dau / Dang lam (de hoc sinh tu lam va trai nghiem)
INSERT INTO assignments (
    assignment_id, center_id, class_id, created_by_teacher_id,
    title, instructions, due_at, status, published_at,
    time_limit_minutes, allow_grade_mismatch, target_mode,
    created_at, updated_at, is_deleted, row_version
) VALUES (
    'a1111111-1111-1111-1111-111111111111', '10000000-0000-0000-0000-000000000001', '51000000-0000-0000-0000-000000000010', 'd0000000-0000-0000-0001-000000000002',
    'Luyện tập tư duy: Phương trình bậc nhất & Lập luận toán học',
    'Các em đọc kỹ câu hỏi, giải thích chi tiết bước lập luận và có thể dùng Bảng vẽ nháp Scratchpad để hỗ trợ lời giải.',
    DATE_ADD(UTC_TIMESTAMP(), INTERVAL 7 DAY), 'Published', UTC_TIMESTAMP(),
    45, 0, 'WholeClass',
    UTC_TIMESTAMP(), UTC_TIMESTAMP(), 0, 1
) ON DUPLICATE KEY UPDATE assignment_id = assignment_id;

INSERT IGNORE INTO assignment_questions (center_id, assignment_id, question_id, order_index, points, created_at, is_voided)
VALUES
('10000000-0000-0000-0000-000000000001', 'a1111111-1111-1111-1111-111111111111', 10000, 1, 10.00, UTC_TIMESTAMP(), 0),
('10000000-0000-0000-0000-000000000001', 'a1111111-1111-1111-1111-111111111111', 10002, 2, 10.00, UTC_TIMESTAMP(), 0),
('10000000-0000-0000-0000-000000000001', 'a1111111-1111-1111-1111-111111111111', 10001, 3, 10.00, UTC_TIMESTAMP(), 0);

INSERT IGNORE INTO assignment_targets (center_id, assignment_id, student_id, target_source, created_at, created_by)
VALUES ('10000000-0000-0000-0000-000000000001', 'a1111111-1111-1111-1111-111111111111', 'd0000000-0000-0000-0001-000000000004', 'Class', UTC_TIMESTAMP(), 'd0000000-0000-0000-0001-000000000002');

INSERT INTO student_assignment_progress (
    progress_id, center_id, assignment_id, student_id,
    status, completed_question_count, total_question_count,
    created_at, updated_at, row_version
) VALUES (
    999001, '10000000-0000-0000-0000-000000000001', 'a1111111-1111-1111-1111-111111111111', 'd0000000-0000-0000-0001-000000000004',
    'NotStarted', 0, 3,
    UTC_TIMESTAMP(), UTC_TIMESTAMP(), 1
) ON DUPLICATE KEY UPDATE progress_id = progress_id;

-- 2. Assignment 2: Da hoan thanh (Xem o tab Da xong)
INSERT INTO assignments (
    assignment_id, center_id, class_id, created_by_teacher_id,
    title, instructions, due_at, status, published_at,
    time_limit_minutes, allow_grade_mismatch, target_mode,
    created_at, updated_at, is_deleted, row_version
) VALUES (
    'a2222222-2222-2222-2222-222222222222', '10000000-0000-0000-0000-000000000001', '51000000-0000-0000-0000-000000000010', 'd0000000-0000-0000-0001-000000000002',
    'Khảo sát năng lực đầu năm: Hàm số và Đồ thị (Đã hoàn thành)',
    'Bài khảo sát đánh giá năng lực toán học đầu năm.',
    DATE_ADD(UTC_TIMESTAMP(), INTERVAL 5 DAY), 'Published', DATE_SUB(UTC_TIMESTAMP(), INTERVAL 2 DAY),
    45, 0, 'WholeClass',
    DATE_SUB(UTC_TIMESTAMP(), INTERVAL 2 DAY), DATE_SUB(UTC_TIMESTAMP(), INTERVAL 2 DAY), 0, 1
) ON DUPLICATE KEY UPDATE assignment_id = assignment_id;

INSERT IGNORE INTO assignment_questions (center_id, assignment_id, question_id, order_index, points, created_at, is_voided)
VALUES
('10000000-0000-0000-0000-000000000001', 'a2222222-2222-2222-2222-222222222222', 10000, 1, 10.00, UTC_TIMESTAMP(), 0),
('10000000-0000-0000-0000-000000000001', 'a2222222-2222-2222-2222-222222222222', 10002, 2, 10.00, UTC_TIMESTAMP(), 0);

INSERT IGNORE INTO assignment_targets (center_id, assignment_id, student_id, target_source, created_at, created_by)
VALUES ('10000000-0000-0000-0000-000000000001', 'a2222222-2222-2222-2222-222222222222', 'd0000000-0000-0000-0001-000000000004', 'Class', UTC_TIMESTAMP(), 'd0000000-0000-0000-0001-000000000002');

INSERT INTO student_assignment_progress (
    progress_id, center_id, assignment_id, student_id,
    status, completed_question_count, total_question_count,
    started_at, completed_at,
    created_at, updated_at, row_version
) VALUES (
    999002, '10000000-0000-0000-0000-000000000001', 'a2222222-2222-2222-2222-222222222222', 'd0000000-0000-0000-0001-000000000004',
    'Completed', 2, 2,
    DATE_SUB(UTC_TIMESTAMP(), INTERVAL 3 HOUR), DATE_SUB(UTC_TIMESTAMP(), INTERVAL 2 HOUR),
    DATE_SUB(UTC_TIMESTAMP(), INTERVAL 2 DAY), DATE_SUB(UTC_TIMESTAMP(), INTERVAL 2 HOUR), 1
) ON DUPLICATE KEY UPDATE progress_id = progress_id;
