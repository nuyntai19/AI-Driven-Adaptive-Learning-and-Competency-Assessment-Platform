import assert from "node:assert/strict";
import test from "node:test";
import { CalculatorEngine } from "../src/utils/calculatorEngine.ts";

test("calculatorEngine evaluates basic arithmetic and operator precedence", () => {
  const calc = new CalculatorEngine();
  assert.equal(calc.evaluate("2 + 3 * 4"), 14);
  assert.equal(calc.evaluate("(2 + 3) * 4"), 20);
  assert.equal(calc.evaluate("10 - 4 - 2"), 4);
  assert.equal(calc.evaluate("15 / 3 * 2"), 10);
  assert.equal(calc.evaluate("10 % 3"), 1);
  assert.equal(calc.evaluate("2 ^ 3"), 8);
  // Power right-associativity: 2 ^ 3 ^ 2 = 2 ^ 9 = 512
  assert.equal(calc.evaluate("2 ^ 3 ^ 2"), 512);
  assert.equal(calc.evaluate("-5 + 10"), 5);
  assert.equal(calc.evaluate("-(3 + 2)"), -5);
  // Unary minus binds looser than exponentiation: -2^2 = -(2^2) = -4
  assert.equal(calc.evaluate("-2 ^ 2"), -4);
  assert.equal(calc.evaluate("-2^2"), -4);
  assert.equal(calc.evaluate("(-2) ^ 2"), 4);
  assert.equal(calc.evaluate("(-2)^2"), 4);
  assert.equal(calc.evaluate("2 ^ -2"), 0.25);
  assert.equal(calc.evaluate("2^-2"), 0.25);
  assert.equal(calc.evaluate("-2 ^ 3"), -8);
  assert.equal(calc.evaluate("-2^3"), -8);
});

test("calculatorEngine supports unicode symbols ×, ÷, and π", () => {
  const calc = new CalculatorEngine();
  assert.equal(calc.evaluate("6 × 7"), 42);
  assert.equal(calc.evaluate("42 ÷ 6"), 7);
  assert.equal(calc.evaluate("π"), Math.round(Math.PI * 1e12) / 1e12);
});

test("calculatorEngine evaluates trigonometric functions in degrees and radians", () => {
  const calcDeg = new CalculatorEngine("deg");
  assert.equal(calcDeg.evaluate("sin(30)"), 0.5);
  assert.equal(calcDeg.evaluate("cos(60)"), 0.5);
  assert.equal(calcDeg.evaluate("tan(45)"), 1);
  assert.equal(calcDeg.evaluate("sin(90)"), 1);
  assert.equal(calcDeg.evaluate("cos(90)"), 0);

  const calcRad = new CalculatorEngine("rad");
  assert.equal(calcRad.evaluate("sin(pi / 6)"), 0.5);
  assert.equal(calcRad.evaluate("cos(pi / 3)"), 0.5);
  assert.equal(calcRad.evaluate("tan(pi / 4)"), 1);
});

test("calculatorEngine evaluates logarithmic and root functions", () => {
  const calc = new CalculatorEngine();
  assert.equal(calc.evaluate("sqrt(16)"), 4);
  assert.equal(calc.evaluate("cbrt(27)"), 3);
  assert.equal(calc.evaluate("log(100)"), 2);
  assert.equal(calc.evaluate("ln(e)"), 1);
  assert.equal(calc.evaluate("abs(-42)"), 42);
});

test("calculatorEngine throws on invalid inputs and division by zero", () => {
  const calc = new CalculatorEngine();
  assert.throws(() => calc.evaluate(""), /Empty expression/);
  assert.throws(() => calc.evaluate("5 / 0"), /Division by zero/);
  assert.throws(() => calc.evaluate("5 % 0"), /Division by zero/);
  assert.throws(() => calc.evaluate("sqrt(-4)"), /Square root of negative number/);
  assert.throws(() => calc.evaluate("log(-10)"), /Logarithm of non-positive number/);
  assert.throws(() => calc.evaluate("2 + * 3"), /Invalid token|Unexpected token/);
  assert.throws(() => calc.evaluate("foo(5)"), /Invalid token|Unknown function/);
  assert.throws(() => calc.evaluate("5 @ 2"), /Unsupported character/);
});
