using System.Text;
using System.Xml.Linq;

namespace EduTwin.BLL.CurriculumAndQuestions.Import;

/// <summary>A bounded, deliberately explicit Office Math subset. Never discards unknown math structure.</summary>
public static class OmmlLatexConverter
{
    private static readonly XNamespace M = "http://schemas.openxmlformats.org/officeDocument/2006/math";
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly HashSet<string> Functions = new(StringComparer.Ordinal)
    { "sin", "cos", "tan", "cot", "sec", "csc", "arcsin", "arccos", "arctan", "sinh", "cosh", "tanh", "log", "ln", "exp", "lim", "min", "max", "sup", "inf", "det", "gcd" };

    public static string Convert(XElement element, CancellationToken cancellationToken = default)
    {
        var stack = new Stack<(XElement Node, int Depth)>();
        stack.Push((element, 0));
        var count = 0;
        while (stack.TryPop(out var entry))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++count > 10000 || entry.Depth > 32) throw Unsupported("Công thức Office Math vượt giới hạn cấu trúc (32 cấp, 10.000 phần tử).");
            foreach (var child in entry.Node.Elements()) stack.Push((child, entry.Depth + 1));
        }
        var result = Render(element, cancellationToken);
        if (string.IsNullOrWhiteSpace(result)) throw Unsupported("Công thức Office Math rỗng.");
        return result;
    }

    private static string Render(XElement e, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (e.Name.Namespace != M) throw Unsupported($"Phần tử {e.Name.LocalName} ngoài tập Office Math được hỗ trợ.");
        if (e.Elements().Where(c => c.Name.LocalName.EndsWith("Pr", StringComparison.Ordinal)).GroupBy(c => c.Name).Any(g => g.Count() > 1)) throw Unsupported("Cấu trúc thuộc tính Office Math bị trùng.");
        string Child(string name, bool required = true)
        {
            var nodes = e.Elements(M + name).ToArray();
            if (nodes.Length > 1 || required && nodes.Length != 1) throw Unsupported($"Cấu trúc {e.Name.LocalName}/{name} không hợp lệ.");
            return nodes.Length == 0 ? "" : Render(nodes[0], ct);
        }
        void Only(params string[] names)
        {
            if (e.Nodes().OfType<XText>().Any(t => !string.IsNullOrWhiteSpace(t.Value)) || e.Elements().Any(c => c.Name.Namespace != M || !names.Contains(c.Name.LocalName, StringComparer.Ordinal)))
                throw Unsupported($"Chưa hỗ trợ thành phần trong {e.Name.LocalName}.");
        }
        var n = e.Name.LocalName;
        string result;
        switch (n)
        {
            case "oMath": case "e": case "num": case "den": case "deg": case "sub": case "sup": case "lim": case "fName":
                if (e.Nodes().OfType<XText>().Any(t => !string.IsNullOrWhiteSpace(t.Value))) throw Unsupported("Văn bản Office Math nằm ngoài run được hỗ trợ.");
                result = Join(e.Elements().Select(c => Render(c, ct))); break;
            case "oMathPara":
                Only("oMathParaPr", "oMath");
                CheckProperties(e.Element(M + "oMathParaPr"), "jc");
                if (e.Elements(M + "oMath").Count() != 1) throw Unsupported("Mỗi đoạn Office Math phải chứa đúng một công thức.");
                result = Child("oMath"); break;
            case "r":
                if (e.Elements().Any(c => c.Name != M + "t" && c.Name != M + "rPr" && c.Name != W + "rPr")) throw Unsupported("Run Office Math chứa thành phần chưa hỗ trợ.");
                if (e.Elements(M + "rPr").Count() > 1 || e.Elements(W + "rPr").Count() > 1 || e.Elements(M + "t").Any(t => t.HasElements) || e.Descendants(W + "vanish").Any()) throw Unsupported("Run Office Math có nội dung ẩn hoặc cấu trúc không hợp lệ.");
                var props = e.Element(M + "rPr");
                CheckProperties(props, "sty", "scr", "nor", "lit", "brk", "aln");
                if (props?.Element(M + "brk") != null) throw Unsupported("Ngắt dòng thủ công trong Office Math chưa được hỗ trợ.");
                var runText = string.Concat(e.Elements(M + "t").Select(t => t.Value));
                if (Hidden(props, "nor")) { result = @"\text{" + EscapeText(runText) + "}"; break; }
                result = Escape(runText);
                var style = Val(props, "sty");
                result = style switch { null or "i" => result, "p" => @"\mathrm{" + result + "}", "b" => @"\mathbf{" + result + "}", "bi" => @"\boldsymbol{" + result + "}", _ => throw Unsupported("Kiểu chữ Office Math chưa hỗ trợ.") };
                var script = Val(props, "scr");
                result = script switch { null or "roman" => result, "double-struck" => @"\mathbb{" + result + "}", "script" => @"\mathcal{" + result + "}", "fraktur" => @"\mathfrak{" + result + "}", "sans-serif" => @"\mathsf{" + result + "}", "monospace" => @"\mathtt{" + result + "}", _ => throw Unsupported("Bộ chữ Office Math chưa hỗ trợ.") };
                break;
            case "t":
                if (e.HasElements) throw Unsupported("Text Office Math không được chứa cấu trúc con.");
                result = Escape(e.Value); break;
            case "f":
                Only("fPr", "num", "den"); CheckProperties(e.Element(M + "fPr"), "type");
                result = Val(e.Element(M + "fPr"), "type") switch
                {
                    null or "bar" => @"\frac{" + Child("num") + "}{" + Child("den") + "}",
                    "lin" or "skw" => throw Unsupported("Phân số dạng gạch chéo chưa được hỗ trợ; đổi sang phân số có gạch ngang trong Word hoặc dùng LaTeX tường minh."),
                    "noBar" => @"\genfrac{}{}{0pt}{}{ " + Child("num") + "}{" + Child("den") + "}",
                    _ => throw Unsupported("Loại phân số Office Math chưa hỗ trợ.")
                }; break;
            case "rad":
                Only("radPr", "deg", "e"); CheckProperties(e.Element(M + "radPr"), "degHide");
                var deg = Child("deg", false);
                result = @"\sqrt" + (Hidden(e.Element(M + "radPr"), "degHide") || deg.Length == 0 ? "" : "[" + deg + "]") + "{" + Child("e") + "}"; break;
            case "sSup": case "sSub": case "sSubSup": case "sPre":
                Only(n == "sSup" ? [n + "Pr", "e", "sup"] : n == "sSub" ? [n + "Pr", "e", "sub"] : [n + "Pr", "e", "sub", "sup"]); CheckProperties(e.Element(M + (n + "Pr")), "alnScr");
                var basis = "{" + Child("e") + "}";
                var sub = n is "sSub" or "sSubSup" or "sPre" ? "_{" + Child("sub") + "}" : "";
                var sup = n is "sSup" or "sSubSup" or "sPre" ? "^{" + Child("sup") + "}" : "";
                result = n == "sPre" ? "{}" + sub + sup + basis : basis + sub + sup; break;
            case "nary":
                Only("naryPr", "sub", "sup", "e"); var np = e.Element(M + "naryPr");
                CheckProperties(np, "chr", "limLoc", "grow", "subHide", "supHide");
                var op = (Val(np, "chr") ?? "∫") switch { "∫" => @"\int", "∬" => @"\iint", "∭" => @"\iiint", "∮" => @"\oint", "∑" => @"\sum", "∏" => @"\prod", "∐" => @"\coprod", "⋃" => @"\bigcup", "⋂" => @"\bigcap", _ => throw Unsupported("Toán tử Office Math chưa hỗ trợ.") };
                var loc = Val(np, "limLoc");
                if (loc is not (null or "subSup" or "undOvr")) throw Unsupported("Vị trí cận Office Math chưa hỗ trợ.");
                result = op + (loc == "undOvr" ? @"\limits" : "") + (Hidden(np, "subHide") ? "" : "_{" + Child("sub", false) + "}") + (Hidden(np, "supHide") ? "" : "^{" + Child("sup", false) + "}") + "{" + Child("e") + "}"; break;
            case "limLow": case "limUpp":
                Only(n + "Pr", "e", "lim"); CheckProperties(e.Element(M + (n + "Pr")));
                result = @"\" + (n == "limLow" ? "underset" : "overset") + "{" + Child("lim") + "}{" + Function(Child("e")) + "}"; break;
            case "func":
                Only("funcPr", "fName", "e"); CheckProperties(e.Element(M + "funcPr"));
                result = Function(Child("fName"), true) + " {" + Child("e") + "}"; break;
            case "d":
                Only("dPr", "e"); var dp = e.Element(M + "dPr"); CheckProperties(dp, "begChr", "endChr", "sepChr", "grow", "shp");
                var args = e.Elements(M + "e").Select(c => Render(c, ct)).ToArray();
                if (args.Length is 0 or > 20) throw Unsupported("Số thành phần trong dấu ngoặc vượt giới hạn.");
                var separator = Val(dp, "sepChr") ?? "|";
                if (separator is not ("|" or "," or ";" or "")) throw Unsupported("Dấu phân cách Office Math chưa hỗ trợ.");
                result = @"\left" + Delimiter(Val(dp, "begChr") ?? "(") + " " + string.Join(separator == "|" ? @"\mid " : separator, args) + @"\right" + Delimiter(Val(dp, "endChr") ?? ")"); break;
            case "m":
                Only("mPr", "mr"); CheckMatrixProperties(e.Element(M + "mPr"));
                var rows = e.Elements(M + "mr").ToArray();
                var widths = rows.Select(r => r.Elements(M + "e").Count()).ToArray();
                if (rows.Length is 0 or > 20 || widths.Any(w => w is 0 or > 20 || w != widths[0]) || rows.Any(r => r.Elements().Any(c => c.Name != M + "e"))) throw Unsupported("Ma trận phải hình chữ nhật, tối đa 20 × 20 ô.");
                result = @"\begin{matrix}" + string.Join(@"\\", rows.Select(r => string.Join("&", r.Elements().Select(c => Render(c, ct))))) + @"\end{matrix}"; break;
            case "eqArr":
                Only("eqArrPr", "e"); CheckProperties(e.Element(M + "eqArrPr"), "baseJc", "maxDist", "objDist", "rSp", "rSpRule");
                if (e.Elements(M + "e").Count() is 0 or > 20) throw Unsupported("Hệ phương trình tối đa 20 dòng.");
                result = @"\begin{gathered}" + string.Join(@"\\", e.Elements(M + "e").Select(c => Render(c, ct))) + @"\end{gathered}"; break;
            case "acc": case "bar":
                Only(n + "Pr", "e"); var ap = e.Element(M + (n + "Pr")); CheckProperties(ap, n == "acc" ? "chr" : "pos");
                var accent = n == "bar" ? (Val(ap, "pos") switch { null or "top" => "overline", "bot" => "underline", _ => throw Unsupported("Vị trí gạch Office Math chưa hỗ trợ.") }) : (Val(ap, "chr") ?? "̂") switch { "̂" or "^" => "hat", "̃" or "~" => "tilde", "⃗" or "→" => "vec", "̇" => "dot", "̈" => "ddot", "̅" => "overline", _ => throw Unsupported("Dấu nhấn Office Math chưa hỗ trợ.") };
                result = @"\" + accent + "{" + Child("e") + "}"; break;
            default: throw Unsupported($"Chưa hỗ trợ cấu trúc Office Math: {n}. Hãy thay bằng LaTeX hoặc sửa công thức trong Word.");
        }
        if (result.Length > 2000) throw Unsupported("Một công thức vượt quá 2.000 ký tự sau chuyển đổi.");
        return result;
    }

    private static string Function(string value, bool strict = false)
    {
        var plain = value.StartsWith(@"\mathrm{", StringComparison.Ordinal) && value.EndsWith('}') ? value[8..^1] : value;
        if (Functions.Contains(plain)) return @"\" + plain;
        if (!strict) return value;
        if (plain.Length is > 0 and <= 64 && plain.All(char.IsAsciiLetter)) return plain.Length == 1 ? plain : @"\operatorname{" + plain + "}";
        throw Unsupported("Tên hàm Office Math chưa được hỗ trợ; dùng tên hàm Latin hoặc LaTeX tường minh.");
    }
    private static string Join(IEnumerable<string> parts)
    {
        var b = new StringBuilder();
        foreach (var p in parts) { if (b.Length + p.Length > 2000) throw Unsupported("Một công thức vượt quá 2.000 ký tự sau chuyển đổi."); b.Append(p); }
        return b.ToString();
    }
    private static string? Val(XElement? p, string name) => p?.Element(M + name)?.Attribute(M + "val")?.Value;
    private static bool Hidden(XElement? p, string name) => p?.Element(M + name) is { } e && (e.Attribute(M + "val")?.Value is null or "1" or "true" or "on");
    private static void CheckProperties(XElement? p, params string[] allowed)
    {
        if (p == null) return;
        if (p.Elements().GroupBy(c => c.Name).Any(g => g.Count() > 1)) throw Unsupported("Thuộc tính Office Math bị trùng.");
        foreach (var c in p.Elements())
        {
            // ctrlPr carries visual Word run properties only, never equation content.
            if (c.Name == M + "ctrlPr" && c.Elements().All(x => x.Name == W + "rPr")) continue;
            if (c.Name.Namespace != M || !allowed.Contains(c.Name.LocalName, StringComparer.Ordinal) || c.HasElements) throw Unsupported($"Thuộc tính Office Math chưa hỗ trợ: {c.Name.LocalName}.");
        }
    }
    private static void CheckMatrixProperties(XElement? p)
    {
        if (p == null) return;
        CheckProperties(new XElement(M + "mPr", p.Elements().Where(e => e.Name != M + "mcs")), "baseJc", "plcHide", "rSp", "rSpRule", "cGp", "cGpRule", "cSp");
        foreach (var mcs in p.Elements(M + "mcs")) foreach (var mc in mcs.Elements())
        {
            if (mc.Name != M + "mc" || mc.Elements().Any(c => c.Name != M + "mcPr")) throw Unsupported("Thuộc tính cột ma trận chưa hỗ trợ.");
            CheckProperties(mc.Element(M + "mcPr"), "count", "mcJc");
        }
    }
    private static string Delimiter(string text) => text switch
    { "" => ".", "(" or ")" or "[" or "]" or "|" => text, "{" => @"\{", "}" => @"\}", "‖" or "∥" => @"\Vert", "⟨" => @"\langle", "⟩" => @"\rangle", "⌊" => @"\lfloor", "⌋" => @"\rfloor", "⌈" => @"\lceil", "⌉" => @"\rceil", _ => throw Unsupported("Dấu ngoặc Office Math chưa hỗ trợ.") };
    private static string Escape(string value)
    {
        if (value.Length > 2000) throw Unsupported("Công thức Office Math quá dài.");
        var b = new StringBuilder();
        foreach (var c in value)
        {
            b.Append(c switch
            {
                '\\' => @"\backslash ", '{' => @"\{", '}' => @"\}", '_' => @"\_", '^' => @"\hat{}", '%' => @"\%", '$' => @"\$", '&' => @"\&", '#' => @"\#", '~' => @"\sim ",
                'α' => @"\alpha ", 'β' => @"\beta ", 'γ' => @"\gamma ", 'δ' => @"\delta ", 'ε' => @"\varepsilon ", 'θ' => @"\theta ", 'λ' => @"\lambda ", 'μ' => @"\mu ", 'π' => @"\pi ", 'ρ' => @"\rho ", 'σ' => @"\sigma ", 'φ' => @"\varphi ", 'ω' => @"\omega ", 'Δ' => @"\Delta ", 'Σ' => @"\Sigma ", 'Ω' => @"\Omega ",
                '∞' => @"\infty ", '≤' => @"\leq ", '≥' => @"\geq ", '≠' => @"\ne ", '≈' => @"\approx ", '±' => @"\pm ", '∓' => @"\mp ", '×' => @"\times ", '÷' => @"\div ", '·' => @"\cdot ", '−' => "-", '∈' => @"\in ", '∉' => @"\notin ", '⊂' => @"\subset ", '⊆' => @"\subseteq ", '∪' => @"\cup ", '∩' => @"\cap ", '∅' => @"\emptyset ", '∀' => @"\forall ", '∃' => @"\exists ", '∂' => @"\partial ", '∇' => @"\nabla ", '→' => @"\to ", '⇒' => @"\Rightarrow ", '⇔' => @"\Leftrightarrow ", '…' => @"\ldots ", 'ℝ' => @"\mathbb{R}", 'ℕ' => @"\mathbb{N}", 'ℤ' => @"\mathbb{Z}", 'ℚ' => @"\mathbb{Q}", 'ℂ' => @"\mathbb{C}",
                '\r' or '\n' or '\t' => " ", _ => c.ToString()
            });
            if (b.Length > 2000) throw Unsupported("Một công thức vượt quá 2.000 ký tự sau chuyển đổi.");
        }
        return b.ToString();
    }
    private static string EscapeText(string value)
    {
        if (value.Length > 2000) throw Unsupported("Văn bản trong công thức quá dài.");
        return Join(value.Select(c => c switch
        {
            '\\' => @"\textbackslash{}", '{' => @"\{", '}' => @"\}", '_' => @"\_", '^' => @"\textasciicircum{}", '%' => @"\%", '$' => @"\$", '&' => @"\&", '#' => @"\#", '~' => @"\textasciitilde{}", '\r' or '\n' or '\t' => " ", _ => c.ToString()
        }));
    }
    private static QuestionImportFileException Unsupported(string message) => new("UNSUPPORTED_OMML", message);
}
