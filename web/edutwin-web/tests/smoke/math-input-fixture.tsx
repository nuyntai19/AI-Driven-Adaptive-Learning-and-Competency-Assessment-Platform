import { useRef, useState } from "react";
import { createRoot } from "react-dom/client";
import { VisualMathField, type VisualMathFieldRef } from "../../src/components/math/VisualMathField";

// Test-only surface mounting the real React component, not a copy of its insertion code.
export function MathInputFixture() {
  const field = useRef<VisualMathFieldRef>(null);
  const [value, setValue] = useState("");
  const [plainText, setPlainText] = useState("");
  const [changes, setChanges] = useState(0);
  return <main style={{ padding: 30, maxWidth: 700 }}>
    <h1>Production VisualMathField</h1>
    <VisualMathField ref={field} value={value} onChange={(latex, plain) => {
      setValue(latex);
      setPlainText(plain);
      setChanges((count) => count + 1);
    }} />
    <div style={{ display: "flex", gap: 12, marginTop: 20 }}>
      <button id="sqrt" onClick={() => field.current?.insertAtCursor("\\sqrt{#0}")}>Căn</button>
      <button id="abs" onClick={() => field.current?.insertAtCursor("\\left|#0\\right|")}>Trị tuyệt đối</button>
      <button id="fraction" onClick={() => field.current?.insertAtCursor("\\frac{#0}{#?}")}>Phân số</button>
      <button id="clear" onClick={() => field.current?.clear()}>Xóa</button>
    </div>
    <output id="latex" style={{ display: "block", whiteSpace: "pre-wrap" }}>{value}</output>
    <output id="plain" style={{ display: "block", whiteSpace: "pre-wrap" }}>{plainText}</output>
    <output id="changes">{changes}</output>
  </main>;
}

createRoot(document.getElementById("root")!).render(<MathInputFixture />);
