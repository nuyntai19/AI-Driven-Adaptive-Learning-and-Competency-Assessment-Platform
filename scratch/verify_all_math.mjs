import katex from 'katex';
import { normalizeMathExpression, isPureMathString, tokenizePlainText } from '../web/edutwin-web/src/components/math/RichMathText.tsx';

// Verify all seed math questions and options
const testCases = [
  {
    input: "Cho hàm số bậc nhất y = 2x + 1. Tính giá trị của y khi x = 3.",
    expectedMathCount: 2
  },
  {
    input: "Tìm tập xác định của hàm số y = 1 / (x - 2).",
    expectedMathCount: 1
  },
  {
    input: "R \\ {2}",
    expectedMathCount: 1
  },
  {
    input: "Đồ thị hàm số y = x^2 - 4x + 3 cắt trục hoành tại các điểm có hoành độ là bao nhiêu?",
    expectedMathCount: 1
  },
  {
    input: "x = -1, x = -3",
    expectedMathCount: 1
  },
  {
    input: "Tìm tọa độ đỉnh của parabol y = -2x^2 + 4x - 1.",
    expectedMathCount: 1
  },
  {
    input: "(1, 1)",
    expectedMathCount: 1
  },
  {
    input: "Chứng minh hàm số y = x^3 - 3x nghịch biến trên khoảng (-1; 1).",
    expectedMathCount: 2
  },
  {
    input: "Tập xác định của hàm số y = log_2(x - 1) là:",
    expectedMathCount: 1
  },
  {
    input: "[1; +vô cùng)",
    expectedMathCount: 1
  },
  {
    input: "Tính giá trị biểu thức A = log_3(9) + log_2(8).",
    expectedMathCount: 1
  },
  {
    input: "Nghiệm của phương trình 3^(x-1) = 27 là:",
    expectedMathCount: 1
  },
  {
    input: "Họ tất cả các nguyên hàm của hàm số f(x) = 2x là:",
    expectedMathCount: 1
  },
  {
    input: "x^2 + C",
    expectedMathCount: 1
  },
  {
    input: "Dùng phương pháp đổi biến, tìm nguyên hàm của f(x) = 2x * e^(x^2).",
    expectedMathCount: 1
  },
  {
    input: "Dùng phương pháp nguyên hàm từng phần, tính nguyên hàm của f(x) = x * ln(x).",
    expectedMathCount: 1
  },
  {
    input: "(x^2/2)ln(x) - x^2/4 + C",
    expectedMathCount: 1
  },
  {
    input: "I _____ to school every day.",
    expectedMathCount: 0
  }
];

console.log("=== COMPREHENSIVE MATH RENDERING TEST ===");
let allPassed = true;

for (const tc of testCases) {
  const tokens = tokenizePlainText(tc.input);
  const mathTokens = tokens.filter(t => t.type === 'math');
  
  let validKatex = true;
  for (const m of mathTokens) {
    try {
      const html = katex.renderToString(m.latex, { throwOnError: true, output: 'htmlAndMathml' });
      if (!html) validKatex = false;
    } catch (err) {
      console.error(`[FAIL KaTeX] input: "${tc.input}" -> latex: "${m.latex}": ${err.message}`);
      validKatex = false;
      allPassed = false;
    }
  }

  if (mathTokens.length === tc.expectedMathCount && validKatex) {
    console.log(`[PASS] "${tc.input}" -> ${mathTokens.length} math token(s): ${mathTokens.map(m => `"${m.latex}"`).join(', ') || '(none)'}`);
  } else {
    console.warn(`[WARN] "${tc.input}" -> expected ${tc.expectedMathCount} math token(s), got ${mathTokens.length}`);
    if (mathTokens.length !== tc.expectedMathCount) allPassed = false;
  }
}

if (allPassed) {
  console.log("\n>>> ALL TEST CASES PASSED WITH 100% ACCURACY! <<<");
} else {
  console.log("\n>>> SOME TEST CASES FAILED <<<");
}
