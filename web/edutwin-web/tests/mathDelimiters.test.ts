import assert from "node:assert/strict";
import test from "node:test";
import { displayMathText, tokenizeDelimitedMath } from "../src/utils/mathDelimiters.ts";
import { hydrateEditorDom, serializeEditorDom } from "../src/components/math/richMathEditorHelpers.ts";

test("rich math tokens preserve literal dollar escapes and complete formula source", () => {
  const source = String.raw`Chi phí \$5; tính $\text{Giá \$5}+x$ và $y+1$.`;
  const tokens = tokenizeDelimitedMath(source);
  assert.equal(tokens.map((token) => token.raw).join(""), source);
  assert.deepEqual(tokens.filter((token) => token.type === "math").map((token) => token.latex), [String.raw`\text{Giá \$5}+x`, "y+1"]);
  assert.equal(displayMathText(tokens[0].raw), "Chi phí $5; tính ");
});

test("backslash parity applies to dollar and bracket delimiters without changing source", () => {
  for (const count of [1, 2, 3, 4]) {
    const slashes = "\\".repeat(count);
    const source = `$x${slashes}$y$`;
    const tokens = tokenizeDelimitedMath(source);
    assert.equal(tokens.map((token) => token.raw).join(""), source);
    const formulas = tokens.filter((token) => token.type === "math");
    assert.equal(formulas.length, 1);
    assert.equal(formulas[0].latex, count % 2 ? `x${slashes}$y` : `x${slashes}`);
  }
  const bracket = String.raw`\\(literal\\) and \(x+\$5\)`;
  const tokens = tokenizeDelimitedMath(bracket);
  assert.equal(tokens.map((token) => token.raw).join(""), bracket);
  assert.deepEqual(tokens.filter((token) => token.type === "math").map((token) => token.latex), [String.raw`x+\$5`]);
});

test("all display delimiters and adjacent inline formulas keep their own boundaries", () => {
  const source = String.raw`$x$$y$; $$z\$5$$; \(a\$5\); \[b\$5\]`;
  const tokens = tokenizeDelimitedMath(source);
  assert.equal(tokens.map((token) => token.raw).join(""), source);
  assert.deepEqual(tokens.filter((token) => token.type === "math").map((token) => [token.latex, token.displayMode]), [
    ["x", false], ["y", false], [String.raw`z\$5`, true], [String.raw`a\$5`, false], [String.raw`b\$5`, true],
  ]);
  for (const raw of [String.raw`Giá \$5`, "$x", "$x\ny$", String.raw`\(x`, String.raw`\[x`]) {
    assert.deepEqual(tokenizeDelimitedMath(raw), [{ type: "text", raw }]);
  }
});

interface MockNode { nodeType: number; nodeValue?: string; }
class MockElement implements MockNode {
  nodeType = 1;
  tagName: string;
  childNodes: MockNode[] = [];
  className = "";
  dataset: Record<string, string> = {};
  style: Record<string, string> = {};
  title = "";
  contentEditable = "true";
  private html = "";
  constructor(tag: string) { this.tagName = tag.toUpperCase(); }
  classList = { contains: (name: string) => this.className.split(/\s+/).includes(name) };
  appendChild(node: MockNode) { this.childNodes.push(node); return node; }
  set innerHTML(value: string) { this.html = value; this.childNodes = []; }
  get innerHTML() { return this.html; }
}

test("editor hydrate, edit and serialize retain escaped dollar formulas and surrounding text", () => {
  const previousDocument = Object.getOwnPropertyDescriptor(globalThis, "document");
  Object.defineProperty(globalThis, "document", { configurable: true, value: {
    createElement: (tag: string) => new MockElement(tag),
    createTextNode: (text: string) => ({ nodeType: 3, nodeValue: text }),
  } });
  try {
    const source = String.raw`Chi phí \$5; tính $\text{Giá \$5}+x$.` + "\n" + String.raw`Tiếp theo $y+1$.`;
    const container = new MockElement("div");
    hydrateEditorDom(container as unknown as HTMLElement, source);
    const formulas = container.childNodes.filter((node): node is MockElement => node instanceof MockElement && node.classList.contains("inline-math-node"));
    assert.deepEqual(formulas.map((node) => node.dataset.latex), [String.raw`\text{Giá \$5}+x`, "y+1"]);
    for (const node of formulas) assert.doesNotMatch(node.innerHTML, /katex-error/);
    assert.equal(serializeEditorDom(container as unknown as HTMLElement), source);
    formulas[0].dataset.latex = String.raw`\text{Giá \$5}+2x`;
    assert.equal(serializeEditorDom(container as unknown as HTMLElement), source.replace("+x$", "+2x$"));
    const literal = String.raw`Chỉ văn bản \$5 và \$10.`;
    hydrateEditorDom(container as unknown as HTMLElement, literal);
    assert.ok(container.childNodes.every((node) => node.nodeType === 3));
    assert.equal(serializeEditorDom(container as unknown as HTMLElement), literal);
    const display = "Tính $x+1$: $$\\int_0^1 x^2\\,dx$$\n$$\\begin{matrix}1&2\\\\3&4\\end{matrix}$$";
    hydrateEditorDom(container as unknown as HTMLElement, display);
    const blocks = container.childNodes.filter((node): node is MockElement => node instanceof MockElement && node.dataset.mathDisplay === "true");
    assert.equal(blocks.length, 2);
    assert.ok(blocks.every((node) => node.style.display === "block" && node.innerHTML.includes("katex-display")));
    assert.equal(serializeEditorDom(container as unknown as HTMLElement), display);
    blocks[0].dataset.latex = String.raw`\int_0^2 x^2\,dx`;
    const editedDisplay = serializeEditorDom(container as unknown as HTMLElement);
    assert.equal(editedDisplay, display.replace("int_0^1", "int_0^2"));
    hydrateEditorDom(container as unknown as HTMLElement, editedDisplay);
    assert.equal(serializeEditorDom(container as unknown as HTMLElement), editedDisplay);
  } finally {
    if (previousDocument) Object.defineProperty(globalThis, "document", previousDocument);
    else Reflect.deleteProperty(globalThis, "document");
  }
});
