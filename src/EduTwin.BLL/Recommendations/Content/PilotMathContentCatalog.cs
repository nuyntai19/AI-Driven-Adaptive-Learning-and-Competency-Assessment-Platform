using System;
using System.Collections.Generic;
using System.Text.Json;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.Recommendations;

namespace EduTwin.BLL.Recommendations.Content;

public static class PilotMathContentCatalog
{
    public const string SubjectName = "Toán học 9";
    public const string ChapterTitle = "Phương trình bậc nhất và Bài toán thực tế";

    public static readonly List<SkillDefinition> Skills = new()
    {
        new("MATH-EQ-01", "Rút gọn và quy đồng phân thức", "Đại số căn bản",
            "Biết tìm mẫu thức chung và quy đồng các phân thức chứa ẩn", new List<string>()),

        new("MATH-EQ-02", "Giải phương trình bậc nhất một ẩn cơ bản", "Phương trình",
            "Giải các phương trình dạng ax + b = 0 với các phép chuyển vế cơ bản", new List<string>()),

        new("MATH-EQ-03", "Phương trình chứa ẩn ở mẫu & ĐKXĐ", "Phương trình",
            "Tìm điều kiện xác định và loại nghiệm ngoại lai", new List<string> { "MATH-EQ-01", "MATH-EQ-02" }),

        new("MATH-EQ-04", "Xử lý dấu ngoặc và phân phối có hệ số âm", "Kỹ năng tính toán",
            "Áp dụng quy tắc nhân phân phối có dấu trừ phía trước không mắc lỗi đổi dấu", new List<string> { "MATH-EQ-02" }),

        new("MATH-EQ-05", "Mô hình hóa: Chọn ẩn và đặt điều kiện", "Bài toán thực tế",
            "Đọc hiểu bài toán văn bản, chọn ẩn số thích hợp và đặt đơn vị, điều kiện thực tế", new List<string>()),

        new("MATH-EQ-06", "Biểu diễn đại lượng theo ẩn số", "Bài toán thực tế",
            "Biểu diễn quãng đường, vận tốc, thời gian hoặc năng suất qua biến số đã chọn", new List<string> { "MATH-EQ-05" }),

        new("MATH-EQ-07", "Lập và giải phương trình bài toán thực tế", "Vận dụng",
            "Thiết lập phương trình cân bằng đại lượng và giải tìm kết quả", new List<string> { "MATH-EQ-03", "MATH-EQ-04", "MATH-EQ-06" }),

        new("MATH-EQ-08", "Thử thách vận dụng: Bài toán tối ưu chi phí", "Vận dụng cao",
            "So sánh phương án thuê bao, chi phí năng lượng thực tế và đưa ra khuyến nghị", new List<string> { "MATH-EQ-07" })
    };

    public record SkillDefinition(
        string SkillId,
        string Name,
        string Topic,
        string Description,
        List<string> Prerequisites);

    public static List<LearningContentVersion> GeneratePilotContent(Guid centerId, Guid subjectId)
    {
        var now = DateTime.UtcNow;
        var list = new List<LearningContentVersion>();

        // 1. Worked Example for MATH-EQ-04: Xử lý dấu ngoặc và hệ số âm
        var wePayload = new
        {
            scenario = "Giải phương trình có chứa dấu ngoặc và hệ số âm: 5 - 2(3x - 4) = 3(x + 1) - 8",
            steps = new[]
            {
                new { stepIndex = 1, explanation = "Nhân phân phối hệ số -2 vào ngoặc (3x - 4). Chú ý: (-2) * (-4) = +8.", formula = "5 - 6x + 8 = 3x + 3 - 8" },
                new { stepIndex = 2, explanation = "Thu gọn hai vế: (5 + 8) - 6x = 3x + (3 - 8)", formula = "13 - 6x = 3x - 5" },
                new { stepIndex = 3, explanation = "Chuyển vế các hạng tử chứa x sang trái, hằng số sang phải", formula = "-6x - 3x = -5 - 13 => -9x = -18" },
                new { stepIndex = 4, explanation = "Chia cả hai vế cho -9", formula = "x = 2" }
            },
            reflectionQuestion = new
            {
                question = "Lỗi phổ biến nhất khi nhân -2 vào (3x - 4) là gì?",
                options = new[]
                {
                    new { id = "A", text = "Quên nhân 2 với 4", isCorrect = false },
                    new { id = "B", text = "Viết thành -6x - 8 do quên đổi dấu của -4", isCorrect = true },
                    new { id = "C", text = "Chuyển vế sai dấu ở bước cuối", isCorrect = false }
                },
                explanation = "Quy tắc nhân dấu: âm nhân âm thành dương. -2 * (-4) = +8. Lỗi quên đổi dấu là lỗi sai thường gặp nhất!"
            }
        };

        list.Add(new LearningContentVersion
        {
            ContentVersionId = Guid.Parse("11111111-0000-0000-0000-000000000001"),
            CenterId = centerId,
            SubjectId = subjectId,
            SkillId = "MATH-EQ-04",
            ActivityType = "WorkedExample",
            Title = "Ví dụ từng bước: Phân phối có dấu âm",
            Description = "Nắm vững kỹ thuật nhân phân phối có dấu trừ phía trước, tránh lỗi sai dấu kinh điển.",
            EstimatedDurationMinutes = 15,
            Payload = JsonSerializer.SerializeToDocument(wePayload),
            Prerequisites = JsonSerializer.SerializeToDocument(new[] { "MATH-EQ-02" }),
            Version = 1,
            Status = "Published",
            CreatedAt = now,
            UpdatedAt = now
        });

        // 2. Find Error for MATH-EQ-04 / MATH-EQ-01: Tìm lỗi sai trong lời giải
        var fePayload = new
        {
            problem = "Một học sinh giải phương trình: 4x - (2x - 6) = 18",
            studentSolution = new[]
            {
                new { stepIndex = 1, text = "Bước 1: Bỏ dấu ngoặc: 4x - 2x - 6 = 18" },
                new { stepIndex = 2, text = "Bước 2: Thu gọn: 2x - 6 = 18" },
                new { stepIndex = 3, text = "Bước 3: Chuyển vế: 2x = 18 + 6 = 24" },
                new { stepIndex = 4, text = "Bước 4: Kết luận: x = 12" }
            },
            errorStep = 1,
            misconceptionCode = "MISCONCEPTION_SIGN_DISTRIBUTION",
            misconceptionExplanation = "Học sinh quên đổi dấu của -6 khi đằng trước ngoặc là dấu trừ. Đúng phải là: 4x - 2x + 6 = 18.",
            correctSolution = "4x - 2x + 6 = 18 <=> 2x + 6 = 18 <=> 2x = 12 <=> x = 6.",
            correctFinalAnswer = "6"
        };

        list.Add(new LearningContentVersion
        {
            ContentVersionId = Guid.Parse("11111111-0000-0000-0000-000000000002"),
            CenterId = centerId,
            SubjectId = subjectId,
            SkillId = "MATH-EQ-04",
            ActivityType = "FindError",
            Title = "Tìm lỗi sai: Bẫy dấu ngoặc",
            Description = "Tìm và chỉ ra bước biến đổi sai lầm trong bài giải của bạn học sinh.",
            EstimatedDurationMinutes = 15,
            Payload = JsonSerializer.SerializeToDocument(fePayload),
            Prerequisites = JsonSerializer.SerializeToDocument(new[] { "MATH-EQ-02" }),
            Version = 1,
            Status = "Published",
            CreatedAt = now,
            UpdatedAt = now
        });

        // 3. Independent Check for MATH-EQ-05 / MATH-EQ-07: Bài tập độc lập
        var icPayload = new
        {
            prompt = "Một người đi xe đạp từ A đến B với vận tốc 15 km/h. Lúc về người đó đi với vận tốc 12 km/h nên thời gian về nhiều hơn thời gian đi là 45 phút (3/4 giờ). Tính quãng đường AB (km).",
            mathSubject = "Toán chuyển động",
            inputMode = "ShortAnswer",
            correctAnswer = "45",
            acceptableAnswers = new[] { "45", "45 km" },
            rubric = new[]
            {
                new { criterion = "Chọn ẩn và điều kiện", maxScore = 2, note = "Gọi quãng đường AB là x (km, x > 0)" },
                new { criterion = "Biểu diễn thời gian đi và về", maxScore = 3, note = "t_đi = x/15, t_về = x/12" },
                new { criterion = "Lập phương trình chính xác", maxScore = 3, note = "x/12 - x/15 = 3/4" },
                new { criterion = "Giải ra đáp số và kết luận", maxScore = 2, note = "x = 45 (thỏa mãn)" }
            }
        };

        list.Add(new LearningContentVersion
        {
            ContentVersionId = Guid.Parse("11111111-0000-0000-0000-000000000003"),
            CenterId = centerId,
            SubjectId = subjectId,
            SkillId = "MATH-EQ-07",
            ActivityType = "IndependentCheck",
            Title = "Luyện tập độc lập: Bài toán chuyển động",
            Description = "Tự giải quyết bài toán thực tế bằng phương trình mà không dùng trợ giúp.",
            EstimatedDurationMinutes = 20,
            Payload = JsonSerializer.SerializeToDocument(icPayload),
            Rubric = JsonSerializer.SerializeToDocument(icPayload.rubric),
            Prerequisites = JsonSerializer.SerializeToDocument(new[] { "MATH-EQ-05", "MATH-EQ-06" }),
            Version = 1,
            Status = "Published",
            CreatedAt = now,
            UpdatedAt = now
        });

        // 4. Transfer Challenge for MATH-EQ-08: Thử thách vận dụng thực tế
        var tcPayload = new
        {
            title = "Thử thách vận dụng: Lựa chọn gói cước Internet tối ưu",
            context = "Gia đình An đang cân nhắc giữa hai gói cước mạng: Gói A có cước thuê bao cố định 120.000đ/tháng và 5.000đ cho mỗi GB vượt dung lượng. Gói B có cước cố định 180.000đ/tháng và 2.000đ cho mỗi GB vượt dung lượng (cả hai gói đều có 20 GB miễn phí ban đầu).",
            questions = new[]
            {
                new { part = "a", prompt = "Nếu mỗi tháng gia đình dùng x GB (x > 20), hãy viết biểu thức chi phí của gói A và gói B.", weight = 30, answer = (string?)null },
                new { part = "b", prompt = "Tìm mức dung lượng x mà tại đó chi phí hai gói bằng nhau.", weight = 30, answer = (string?)"40" },
                new { part = "c", prompt = "Nếu gia đình An thường xuyên dùng 50 GB/tháng, gia đình nên chọn gói nào để tiết kiệm chi phí?", weight = 40, answer = (string?)"Gói B" }
            },
            rubricCriteria = new[]
            {
                new { skillId = "MATH-EQ-05", name = "Mô hình hóa biến số", maxScore = 30 },
                new { skillId = "MATH-EQ-07", name = "Lập và giải phương trình cân bằng", maxScore = 30 },
                new { skillId = "MATH-EQ-08", name = "Biện luận và tối ưu hóa quyết định", maxScore = 40 }
            }
        };

        list.Add(new LearningContentVersion
        {
            ContentVersionId = Guid.Parse("11111111-0000-0000-0000-000000000004"),
            CenterId = centerId,
            SubjectId = subjectId,
            SkillId = "MATH-EQ-08",
            ActivityType = "TransferChallenge",
            Title = "Thử thách vận dụng: Tối ưu gói cước Internet",
            Description = "Áp dụng phương trình giải quyết bài toán kinh tế gia đình và ra quyết định thông minh.",
            EstimatedDurationMinutes = 25,
            Payload = JsonSerializer.SerializeToDocument(tcPayload),
            Rubric = JsonSerializer.SerializeToDocument(tcPayload.rubricCriteria),
            Prerequisites = JsonSerializer.SerializeToDocument(new[] { "MATH-EQ-07" }),
            Version = 1,
            Status = "Published",
            CreatedAt = now,
            UpdatedAt = now
        });

        return list;
    }
}
