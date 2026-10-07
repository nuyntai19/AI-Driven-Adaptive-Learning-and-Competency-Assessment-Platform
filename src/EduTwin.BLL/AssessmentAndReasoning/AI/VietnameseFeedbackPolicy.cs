namespace EduTwin.BLL.AssessmentAndReasoning.AI;

/// <summary>A conservative guard against wholly English explanations, not a translation service.</summary>
public static class VietnameseFeedbackPolicy
{
    private const string VietnameseLetters = "ăâđêôơưĂÂĐÊÔƠƯàáảãạằắẳẵặầấẩẫậèéẻẽẹềếểễệìíỉĩịòóỏõọồốổỗộờớởỡợùúủũụừứửữựỳýỷỹỵÀÁẢÃẠẰẮẲẴẶẦẤẨẪẬÈÉẺẼẸỀẾỂỄỆÌÍỈĨỊÒÓỎÕỌỒỐỔỖỘỜỚỞỠỢÙÚỦŨỤỪỨỬỮỰỲÝỶỸỴ";
    public static bool HasVietnameseExplanation(string text) => !string.IsNullOrWhiteSpace(text)
        && text.Normalize().Any(c => VietnameseLetters.Contains(c));
}
