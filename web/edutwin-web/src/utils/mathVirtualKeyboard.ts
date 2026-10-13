import type { VirtualKeyboardLayout, VirtualKeyboardOptions } from "mathlive";

export const SET_FORMULA_KEYS = [
  { label: "ℝ", tooltip: "Số thực", latex: "\\mathbb{R}" },
  { label: "{…}", tooltip: "Ngoặc nhọn tập hợp", latex: "\\left\\{#?\\right\\}" },
  { label: "∖", tooltip: "Hiệu tập hợp", latex: "\\setminus" },
  { label: "∈", tooltip: "Thuộc tập hợp", latex: "\\in" },
  { label: "∉", tooltip: "Không thuộc tập hợp", latex: "\\notin" },
  { label: "∪", tooltip: "Hợp hai tập hợp", latex: "\\cup" },
  { label: "∩", tooltip: "Giao hai tập hợp", latex: "\\cap" },
  { label: "∅", tooltip: "Tập rỗng", latex: "\\emptyset" },
];

export const SET_KEYBOARD_LAYOUT: VirtualKeyboardLayout = {
  id: "edutwin-sets",
  label: "Tập hợp",
  tooltip: "Ký hiệu tập hợp",
  rows: [
    SET_FORMULA_KEYS,
    ["[1]", "[2]", "[3]", "[4]", "[5]", "[6]", "[7]", "[8]", "[9]", "[0]"],
    ["x", "y", ",", ";", "=", "\\ne", "[left]", "[right]", "[backspace]", "[hide-keyboard]"],
  ],
};

/** MathLive has one shared keyboard. Preserve its layouts and append ours only once. */
export function installSetKeyboardLayout(keyboard: Pick<VirtualKeyboardOptions, "layouts"> | undefined): void {
  if (!keyboard) return;
  const layouts = keyboard.layouts?.length ? [...keyboard.layouts] : ["default" as const];
  if (layouts.some(layout => typeof layout === "object" && layout.id === SET_KEYBOARD_LAYOUT.id)) return;
  keyboard.layouts = [...layouts, SET_KEYBOARD_LAYOUT];
}
