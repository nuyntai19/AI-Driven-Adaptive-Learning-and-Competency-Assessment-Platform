using System.Xml.Linq;
using EduTwin.BLL.CurriculumAndQuestions.Import;
using Xunit;

namespace EduTwin.BLL.Tests.CurriculumAndQuestions;

public sealed class OmmlLatexConverterTests
{
    private static XElement Math(string inner) => XElement.Parse("<m:oMath xmlns:m=\"http://schemas.openxmlformats.org/officeDocument/2006/math\">" + inner + "</m:oMath>");
    private const string X = "<m:r><m:t>x</m:t></m:r>";
    private const string Two = "<m:r><m:t>2</m:t></m:r>";

    [Theory]
    [InlineData("<m:f><m:num><m:r><m:t>1</m:t></m:r></m:num><m:den><m:r><m:t>2</m:t></m:r></m:den></m:f>", @"\frac{1}{2}")]
    [InlineData("<m:rad><m:radPr><m:degHide m:val=\"1\"/></m:radPr><m:deg/><m:e><m:r><m:t>x</m:t></m:r></m:e></m:rad>", @"\sqrt{x}")]
    [InlineData("<m:rad><m:deg><m:r><m:t>3</m:t></m:r></m:deg><m:e><m:r><m:t>x</m:t></m:r></m:e></m:rad>", @"\sqrt[3]{x}")]
    [InlineData("<m:func><m:fName><m:r><m:t>sin</m:t></m:r></m:fName><m:e><m:r><m:t>x</m:t></m:r></m:e></m:func>", @"\sin {x}")]
    [InlineData("<m:r><m:t>ℝ∈α≤∞</m:t></m:r>", @"\mathbb{R}\in \alpha \leq \infty ")]
    [InlineData("<m:r><m:rPr><m:sty m:val=\"p\"/></m:rPr><m:t>ABC</m:t></m:r>", @"\mathrm{ABC}")]
    [InlineData("<m:r><m:rPr><m:nor/></m:rPr><m:t>khi x &gt; 0</m:t></m:r>", @"\text{khi x > 0}")]
    [InlineData("<m:func><m:fName><m:r><m:t>foo</m:t></m:r></m:fName><m:e><m:r><m:t>x</m:t></m:r></m:e></m:func>", @"\operatorname{foo} {x}")]
    [InlineData("<m:r><m:t>\\href{x}$&amp;_</m:t></m:r>", @"\backslash href\{x\}\$\&\_")]
    public void Converts_Supported_Math_Exactly(string source, string expected) => Assert.Equal(expected, OmmlLatexConverter.Convert(Math(source)));

    [Fact]
    public void Scripts_And_Integral_Preserve_All_Arguments()
    {
        Assert.Equal("{x}_{2}^{2}", OmmlLatexConverter.Convert(Math($"<m:sSubSup><m:e>{X}</m:e><m:sub>{Two}</m:sub><m:sup>{Two}</m:sup></m:sSubSup>")));
        Assert.Equal(@"\int_{2}^{2}{x}", OmmlLatexConverter.Convert(Math($"<m:nary><m:naryPr><m:chr m:val=\"∫\"/></m:naryPr><m:sub>{Two}</m:sub><m:sup>{Two}</m:sup><m:e>{X}</m:e></m:nary>")));
        Assert.Equal(@"\sum\limits_{2}^{2}{x}", OmmlLatexConverter.Convert(Math($"<m:nary><m:naryPr><m:chr m:val=\"∑\"/><m:limLoc m:val=\"undOvr\"/></m:naryPr><m:sub>{Two}</m:sub><m:sup>{Two}</m:sup><m:e>{X}</m:e></m:nary>")));
    }
    [Fact]
    public void Matrix_Cases_And_Limits_Preserve_Structure()
    {
        var matrix = $"<m:m><m:mr><m:e>{X}</m:e><m:e>{Two}</m:e></m:mr><m:mr><m:e>{Two}</m:e><m:e>{X}</m:e></m:mr></m:m>";
        Assert.Equal(@"\begin{matrix}x&2\\2&x\end{matrix}", OmmlLatexConverter.Convert(Math(matrix)));
        Assert.Equal(@"\left\{ \begin{gathered}x\\2\end{gathered}\right.", OmmlLatexConverter.Convert(Math($"<m:d><m:dPr><m:begChr m:val=\"{{\"/><m:endChr m:val=\"\"/></m:dPr><m:e><m:eqArr><m:e>{X}</m:e><m:e>{Two}</m:e></m:eqArr></m:e></m:d>")));
        Assert.Equal(@"\underset{x}{\lim}", OmmlLatexConverter.Convert(Math($"<m:limLow><m:e><m:r><m:t>lim</m:t></m:r></m:e><m:lim>{X}</m:lim></m:limLow>")));
    }
    [Theory]
    [InlineData("<m:phant><m:e><m:r><m:t>x</m:t></m:r></m:e></m:phant>")]
    [InlineData("<m:unknown/>")]
    [InlineData("<m:f><m:fPr><m:type m:val=\"unknown\"/></m:fPr><m:num/><m:den/></m:f>")]
    [InlineData("<m:r><m:rPr><m:brk/></m:rPr><m:t>x</m:t></m:r>")]
    [InlineData("<m:r><m:rPr><m:unknown/></m:rPr><m:t>x</m:t></m:r>")]
    [InlineData("<m:sSup><m:e/><m:sup/><m:sub><m:r><m:t>2</m:t></m:r></m:sub></m:sSup>")]
    [InlineData("<m:sSub><m:e/><m:sub/><m:sup><m:r><m:t>2</m:t></m:r></m:sup></m:sSub>")]
    [InlineData("<m:f><m:fPr/><m:fPr/><m:num/><m:den/></m:f>")]
    [InlineData("<m:t><m:unknown>x</m:unknown></m:t>")]
    [InlineData("<m:e>hidden</m:e>")]
    [InlineData("<m:f><m:fPr><m:type m:val=\"lin\"/></m:fPr><m:num><m:r><m:t>a+b</m:t></m:r></m:num><m:den><m:r><m:t>c+d</m:t></m:r></m:den></m:f>")]
    [InlineData("<m:f><m:fPr><m:type m:val=\"skw\"/></m:fPr><m:num/><m:den/></m:f>")]
    public void Unknown_Structures_Are_Explicit_Errors(string source) => Assert.Equal("UNSUPPORTED_OMML", Assert.Throws<QuestionImportFileException>(() => OmmlLatexConverter.Convert(Math(source))).Code);

    [Fact]
    public void Bounds_And_Cancellation_Are_Enforced()
    {
        var deep = X;
        for (var i = 0; i < 33; i++) deep = "<m:e>" + deep + "</m:e>";
        Assert.Throws<QuestionImportFileException>(() => OmmlLatexConverter.Convert(Math(deep)));
        Assert.Throws<QuestionImportFileException>(() => OmmlLatexConverter.Convert(Math("<m:r><m:t>" + new string('x', 2001) + "</m:t></m:r>")));
        Assert.Throws<QuestionImportFileException>(() => OmmlLatexConverter.Convert(Math(string.Concat(Enumerable.Repeat(X, 2001)))));
        Assert.Throws<OperationCanceledException>(() => OmmlLatexConverter.Convert(Math(X), new CancellationToken(true)));
    }
}
