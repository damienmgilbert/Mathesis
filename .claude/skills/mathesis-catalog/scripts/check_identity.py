#!/usr/bin/env python3
"""Quick numeric sanity check for a Mathesis law or formula, before it goes into a .mlaw file.

This mirrors the CI verification harness described in 06-knowledge-catalog.md closely enough
to catch wrong statements and missing conditions while drafting. It is not the harness itself:
the real check runs in Mathesis.Knowledge.Tests once the library exists.

Usage:
  check_identity.py --vars "a: real, m: real, n: real" \
      --statement "a^m * a^n = a^(m + n)" \
      --where "a > 0 or (a != 0 and m in Z and n in Z)"
  check_identity.py --vars "u: complex, v: complex" --statement "sin(u + v) = sin(u)*cos(v) + cos(u)*sin(v)" --complex
  check_identity.py ... --sample "n in 1..12" --sample "x in (0, 10)"

Notation (subset of Mathesis linear input notation): + - * / ^, implicit multiplication (2x, 2(x+1),
(a)(b)), sqrt, root(a, n), abs, exp, ln, log(x) (base 10), log(x, b), sin cos tan cot sec csc,
arcsin arccos arctan, sinh cosh tanh, floor, ceil, re, im, conj, pi, e, I.
Conditions: and / or / not, = (or ==), !=, <, <=, >, >=, "x in Z", "x in N", chained comparisons.
Statement: "lhs = rhs" or an inequality. Equivalences (<=>) and quantifiers are not supported.

Real mode follows the Mathesis conventions: 0^0 = 1, (-8)^(1/3) = -2 (real odd roots),
even roots and logs of negatives are undefined (the sample is skipped).
"""
import argparse
import ast
import cmath
import math
import random
import re
import sys
from fractions import Fraction

FUNCS = {"sqrt", "root", "abs", "exp", "ln", "log", "sin", "cos", "tan", "cot", "sec", "csc",
         "arcsin", "arccos", "arctan", "asin", "acos", "atan", "sinh", "cosh", "tanh",
         "floor", "ceil", "re", "im", "conj", "isint", "isnat"}
CONSTS = {"pi", "e", "I"}


class Undefined(Exception):
    pass


def to_python(src: str, names: set) -> str:
    s = src.strip().replace("·", "*").replace("×", "*").replace("π", "pi").replace("≠", "!=")
    s = s.replace("≤", "<=").replace("≥", ">=").replace("−", "-")
    s = re.sub(r"\b(\w+)\s+in\s+Z\b", r"isint(\1)", s)
    s = re.sub(r"\b(\w+)\s+in\s+N\b", r"isnat(\1)", s)
    s = re.sub(r"(?<![<>!=])=(?!=)", "==", s)
    # function application without parentheses: sin x, sin 2x, ln y
    fn = "|".join(sorted(FUNCS, key=len, reverse=True))
    s = re.sub(r"\b(" + fn + r")\s+(\d*\.?\d*[A-Za-z_]\w*|\d+(?:\.\d+)?)", r"\1(\2)", s)
    s = s.replace("^", "**")
    # implicit multiplication
    kw = r"(?!(?:and|or|not|in)\b)"
    s = re.sub(r"(\d)\s*" + kw + r"([A-Za-z_(])", r"\1*\2", s)
    s = re.sub(r"\)\s*" + kw + r"([A-Za-z_(\d])", r")*\1", s)
    # identifier followed by "(" that is not a function: multiplication
    def fix_call(m):
        name = m.group(1)
        return name + "(" if name in FUNCS or name in {"and", "or", "not", "in"} else name + "*("
    s = re.sub(r"\b([A-Za-z_]\w*)\s*\(", fix_call, s)
    # adjacent identifiers "a b" -> a*b
    s = re.sub(r"\b([A-Za-z_]\w*)\s+([A-Za-z_]\w*|\d)", lambda m: m.group(0) if m.group(1) in
               {"and", "or", "not", "in"} or m.group(2) in {"and", "or", "not", "in"}
               else f"{m.group(1)}*{m.group(2)}", s)
    return s


class PowToCall(ast.NodeTransformer):
    def visit_BinOp(self, node):
        self.generic_visit(node)
        if isinstance(node.op, ast.Pow):
            return ast.copy_location(ast.Call(func=ast.Name("rpow", ast.Load()), args=[node.left, node.right], keywords=[]), node)
        if isinstance(node.op, ast.Div):
            return ast.copy_location(ast.Call(func=ast.Name("rdiv", ast.Load()), args=[node.left, node.right], keywords=[]), node)
        return node


def compile_expr(src: str, names: set, mode="eval"):
    py = to_python(src, names)
    tree = ast.parse(py, mode="eval")
    tree = ast.fix_missing_locations(PowToCall().visit(tree))
    return compile(tree, "<expr>", "eval"), py


def real_namespace():
    def chk(x):
        if isinstance(x, complex):
            if abs(x.imag) > 1e-12 * max(1.0, abs(x.real)):
                raise Undefined()
            x = x.real
        if isinstance(x, float) and not math.isfinite(x):
            raise Undefined()
        return x

    def rdiv(a, b):
        if b == 0:
            raise Undefined()
        return a / b

    def rpow(a, b):
        if a == 0:
            if b == 0:
                return 1.0  # conv.zero-to-the-zero
            if b < 0:
                raise Undefined()
            return 0.0
        if a > 0:
            return chk(a ** b)
        bi = round(b)
        if abs(b - bi) < 1e-12:
            return chk(a ** int(bi))
        fr = Fraction(b).limit_denominator(99)
        if abs(float(fr) - b) < 1e-12 and fr.denominator % 2 == 1:  # conv.real-odd-root
            mag = abs(a) ** b
            return -mag if fr.numerator % 2 else mag
        raise Undefined()

    def sqrt(x):
        if x < 0:
            raise Undefined()
        return math.sqrt(x)

    def root(x, n):
        n = int(round(n))
        if n % 2 == 0 and x < 0:
            raise Undefined()
        return math.copysign(abs(x) ** (1.0 / n), x)

    def ln(x):
        if x <= 0:
            raise Undefined()
        return math.log(x)

    def log(x, b=10):
        if x <= 0 or b <= 0 or b == 1:
            raise Undefined()
        return math.log(x) / math.log(b)

    def dom(f, lo, hi):
        def g(x):
            if not (lo <= x <= hi):
                raise Undefined()
            return f(x)
        return g

    def tan(x):
        c = math.cos(x)
        if abs(c) < 1e-12:
            raise Undefined()
        return math.sin(x) / c

    def recip(f):
        def g(x):
            v = f(x)
            if abs(v) < 1e-12:
                raise Undefined()
            return 1 / v
        return g

    ns = dict(rpow=rpow, rdiv=rdiv, sqrt=sqrt, root=root, abs=abs, exp=math.exp, ln=ln, log=log,
              sin=math.sin, cos=math.cos, tan=tan, cot=recip(tan), sec=recip(math.cos), csc=recip(math.sin),
              arcsin=dom(math.asin, -1, 1), arccos=dom(math.acos, -1, 1), arctan=math.atan,
              asin=dom(math.asin, -1, 1), acos=dom(math.acos, -1, 1), atan=math.atan,
              sinh=math.sinh, cosh=math.cosh, tanh=math.tanh, floor=math.floor, ceil=math.ceil,
              re=lambda z: z, im=lambda z: 0.0, conj=lambda z: z,
              isint=lambda x: abs(x - round(x)) < 1e-12, isnat=lambda x: abs(x - round(x)) < 1e-12 and x >= 0,
              pi=math.pi, e=math.e)
    return ns


def complex_namespace():
    def rdiv(a, b):
        if b == 0:
            raise Undefined()
        return a / b

    def rpow(a, b):
        if a == 0:
            if b == 0:
                return 1
            if complex(b).real <= 0:
                raise Undefined()
            return 0
        return cmath.exp(b * cmath.log(a))

    def log(x, b=10):
        if x == 0:
            raise Undefined()
        return cmath.log(x) / cmath.log(b)

    def ln(x):
        if x == 0:
            raise Undefined()
        return cmath.log(x)

    def recip(f):
        def g(x):
            v = f(x)
            if abs(v) < 1e-12:
                raise Undefined()
            return 1 / v
        return g

    def tan(x):
        return rdiv(cmath.sin(x), cmath.cos(x))

    ns = dict(rpow=rpow, rdiv=rdiv, sqrt=cmath.sqrt, root=lambda x, n: rpow(x, 1 / n), abs=abs, exp=cmath.exp,
              ln=ln, log=log, sin=cmath.sin, cos=cmath.cos, tan=tan, cot=recip(tan), sec=recip(cmath.cos),
              csc=recip(cmath.sin), arcsin=cmath.asin, arccos=cmath.acos, arctan=cmath.atan,
              asin=cmath.asin, acos=cmath.acos, atan=cmath.atan, sinh=cmath.sinh, cosh=cmath.cosh,
              tanh=cmath.tanh, re=lambda z: complex(z).real, im=lambda z: complex(z).imag,
              conj=lambda z: complex(z).conjugate(),
              isint=lambda x: complex(x).imag == 0 and abs(complex(x).real - round(complex(x).real)) < 1e-12,
              isnat=lambda x: complex(x).imag == 0 and complex(x).real >= 0 and abs(complex(x).real - round(complex(x).real)) < 1e-12,
              pi=math.pi, e=math.e, I=1j, floor=math.floor, ceil=math.ceil)
    return ns


def parse_vars(spec: str):
    out = {}
    for part in [p.strip() for p in spec.split(",") if p.strip()]:
        if ":" in part:
            name, sort = [s.strip() for s in part.split(":", 1)]
        else:
            name, sort = part, "real"
        out[name] = sort
    return out


def parse_hints(hints):
    res = {}
    for h in hints or []:
        m = re.match(r"\s*(\w+)\s+in\s+(-?\d+)\s*\.\.\s*(-?\d+)\s*$", h)
        if m:
            res[m.group(1)] = ("int", int(m.group(2)), int(m.group(3)))
            continue
        m = re.match(r"\s*(\w+)\s+in\s+[\(\[]\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)\s*[\)\]]\s*$", h)
        if m:
            res[m.group(1)] = ("real", float(m.group(2)), float(m.group(3)))
            continue
        sys.exit(f"Unrecognized --sample hint: {h}")
    return res


def draw(sort: str, hint, rng: random.Random, complex_mode: bool):
    if hint:
        kind, lo, hi = hint
        return rng.randint(lo, hi) if kind == "int" else rng.uniform(lo, hi)
    s = sort.lower()
    if s in ("integer", "int", "z"):
        return rng.choice([rng.randint(-6, 6), rng.randint(-30, 30)])
    if s in ("natural", "n"):
        return rng.randint(0, 12)
    if s in ("complex", "c") and complex_mode:
        return complex(rng.choice([0.0, rng.uniform(-5, 5), rng.randint(-3, 3) / 2]),
                       rng.choice([0.0, rng.uniform(-5, 5), rng.randint(-3, 3) / 2]))
    choice = rng.random()
    if choice < 0.2:
        return float(rng.randint(-6, 6))
    if choice < 0.4:
        return rng.randint(-12, 12) / rng.choice([2, 3, 4, 5, 6, 7])
    if choice < 0.8:
        return rng.uniform(-10, 10)
    if choice < 0.9:
        return rng.uniform(-1e-3, 1e-3)
    return rng.choice([-1, 1]) * rng.uniform(50, 1e3)


def finite(v):
    v = complex(v)
    return math.isfinite(v.real) and math.isfinite(v.imag)


def sensitivity(code, scope, env):
    """Sum over variables of |df/dx_i * x_i|, estimated by relative perturbation.
    Large values mean the expression cancels badly in double precision, so the
    comparison tolerance grows with it (a rough stand-in for the harness's condition estimate)."""
    base = eval(code, scope)
    total = 0.0
    for k, v in env.items():
        if v == 0 or isinstance(v, int):
            continue
        s2 = dict(scope)
        s2[k] = v * (1 + 1e-7)
        try:
            total += abs(eval(code, s2) - base) / 1e-7
        except Exception:
            pass
    return total


def show(v):
    return float(v) if isinstance(v, Fraction) else v


def close(l, r, sens=0.0):
    return abs(l - r) <= 1e-9 * max(1.0, abs(l), abs(r)) + 1e-12 * sens


def split_statement(stmt: str):
    if "<=>" in stmt:
        sys.exit("Equivalences (<=>) are not supported; check each direction's equation separately.")
    depth = 0
    for i, ch in enumerate(stmt):
        if ch in "([{":
            depth += 1
        elif ch in ")]}":
            depth -= 1
        elif depth == 0 and ch == "=" and stmt[i - 1:i] not in ("<", ">", "!", "=") and stmt[i + 1:i + 2] != "=":
            return stmt[:i], stmt[i + 1:]
    return None, None


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--vars", required=True)
    ap.add_argument("--statement", required=True)
    ap.add_argument("--where", default="")
    ap.add_argument("--complex", action="store_true", help="complex mode (principal branches)")
    ap.add_argument("--sample", action="append", help='sampler hint, e.g. "n in 1..12" or "x in (0, 10)"')
    ap.add_argument("--samples", type=int, default=2000)
    ap.add_argument("--seed", type=int, default=1)
    a = ap.parse_args()

    vars_ = parse_vars(a.vars)
    hints = parse_hints(a.sample)
    ns = complex_namespace() if a.complex else real_namespace()
    names = set(vars_)

    lhs, rhs = split_statement(a.statement)
    if lhs is not None:
        lc, lpy = compile_expr(lhs, names)
        rc, rpy = compile_expr(rhs, names)
        is_eq = True
    else:
        sc, spy = compile_expr(a.statement, names)
        is_eq = False
    wc = compile_expr(a.where, names)[0] if a.where.strip() else None

    rng = random.Random(a.seed)
    inside = inside_fail = outside = outside_fail = skipped = 0
    counterexamples, outside_fails = [], []
    for _ in range(a.samples):
        env = {k: draw(v, hints.get(k), rng, a.complex) for k, v in vars_.items()}
        # Real mode evaluates with exact rationals where it can (the harness does the same for
        # rational expressions), so cancellation in double precision can't fake a failure.
        exact_env = env if a.complex else {k: Fraction(v) if isinstance(v, float) else v for k, v in env.items()}
        scope = dict(ns, **exact_env)
        try:
            ok_cond = True if wc is None else bool(eval(wc, scope))
        except (Undefined, ValueError, ZeroDivisionError, OverflowError, TypeError):
            skipped += 1
            continue
        try:
            if is_eq:
                l, r = eval(lc, scope), eval(rc, scope)
                if not (finite(l) and finite(r)):
                    raise Undefined()
                holds = close(l, r)
                if not holds:
                    holds = close(l, r, sensitivity(lc, scope, env) + sensitivity(rc, scope, env))
            else:
                holds = bool(eval(sc, scope))
                l = r = None
        except (Undefined, ValueError, ZeroDivisionError, OverflowError):
            skipped += 1
            continue
        if ok_cond:
            inside += 1
            if not holds:
                inside_fail += 1
                if len(counterexamples) < 5:
                    counterexamples.append((env, l, r))
        else:
            outside += 1
            if not holds:
                outside_fail += 1
                if len(outside_fails) < 3:
                    outside_fails.append((env, l, r))

    mode = "complex" if a.complex else "real"
    print(f"Mode: {mode}   samples: {a.samples}   undefined/skipped: {skipped}")
    print(f"Inside conditions:  {inside} checked, {inside_fail} failed")
    for env, l, r in counterexamples:
        print(f"  COUNTEREXAMPLE {env}  lhs={show(l)}  rhs={show(r)}")
    if wc is not None:
        print(f"Outside conditions: {outside} checked, {outside_fail} failed")
        if outside and outside_fail == 0:
            print("  WARNING: held at every sampled point outside the conditions; they may be stronger than necessary.")
        for env, l, r in outside_fails:
            print(f"  e.g. fails at {env}  lhs={show(l)}  rhs={show(r)}")
    if inside == 0:
        print("No samples satisfied the conditions; add --sample hints.")
        sys.exit(2)
    sys.exit(1 if inside_fail else 0)


if __name__ == "__main__":
    main()
