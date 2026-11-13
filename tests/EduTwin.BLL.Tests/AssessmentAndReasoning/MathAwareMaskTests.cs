using System.Text.RegularExpressions;
using EduTwin.BLL.AssessmentAndReasoning;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning;

public class MathAwareMaskTests
{
    private static readonly Regex Two = new(@"(?<![\p{L}\p{N}.,])2(?![\p{L}\p{N}])");

    [Fact]
    public void Answer_inside_a_formula_becomes_a_box_and_prose_keeps_the_phrase()
    {
        var masked = MathAwareMask.Replace("Chuyển 1 sang vế phải: $x = 3 - 1 = 2$. Đáp số 2.", Two, "[kết quả cần tìm]");
        Assert.Equal(@"Chuyển 1 sang vế phải: $x = 3 - 1 = \boxed{?}$. Đáp số [kết quả cần tìm].", masked);
    }

    [Fact]
    public void Display_and_paren_math_are_masked_as_math()
    {
        Assert.Equal(@"$$x=\boxed{?}$$ và \(y=\boxed{?}\)", MathAwareMask.Replace(@"$$x=2$$ và \(y=2\)", Two, "___"));
    }
}
