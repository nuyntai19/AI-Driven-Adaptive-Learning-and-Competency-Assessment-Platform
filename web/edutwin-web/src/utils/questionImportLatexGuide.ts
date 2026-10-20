/** Complete inline sources shared by the import help and the Excel Guide sheet. */
export const QUESTION_IMPORT_LATEX_EXAMPLES: ReadonlyArray<{ label: string; latex: string }> = [
  { label: "Phân số", latex: String.raw`$\frac{1}{2}+\frac{1}{3}=\frac{5}{6}$` },
  { label: "Căn bậc hai", latex: String.raw`$\sqrt{x+1}$` },
  { label: "Căn bậc n", latex: String.raw`$\sqrt[3]{8}=2$` },
  { label: "Lũy thừa", latex: String.raw`$x^{n+1}$` },
  { label: "Chỉ số dưới", latex: String.raw`$a_{n+1}=a_n+2$` },
  { label: "Tích phân", latex: String.raw`$\int_{0}^{1}x^2\,dx=\frac{1}{3}$` },
  { label: "Giới hạn", latex: String.raw`$\lim_{x\to 0}\frac{\sin x}{x}=1$` },
  { label: "Tổng", latex: String.raw`$\sum_{k=1}^{n}k=\frac{n(n+1)}{2}$` },
  { label: "Logarit", latex: String.raw`$\log_{2}8=3,\quad\ln e=1$` },
  { label: "Lượng giác", latex: String.raw`$\sin^2 x+\cos^2 x=1$` },
  { label: "Đạo hàm", latex: String.raw`$f(x)=x^2,\quad f'(x)=2x$` },
  { label: "Hệ phương trình", latex: String.raw`$\begin{cases}x+y=3\\x-y=1\end{cases}$` },
];
