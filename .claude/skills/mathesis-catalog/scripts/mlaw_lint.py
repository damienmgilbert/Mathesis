#!/usr/bin/env python3
"""Lightweight lint for Mathesis .mlaw files, for use while drafting catalog entries in chat.

Approximates eng/mlaw-lint.cs (06-knowledge-catalog.md, Tooling): header, entry kinds, known
field keys, kebab-case names, duplicate IDs, required fields per kind, curriculum levels,
course tags, orient values, verify: none needs rationale, and undeclared variables.

Usage: mlaw_lint.py file.mlaw [more.mlaw ...]
Exit code 1 if any error is found; warnings alone exit 0.
"""
import re
import sys

KINDS = {"definition", "axiom", "law", "theorem", "formula", "pattern", "method", "convention"}
KEYS = ["vars", "statement", "given", "then", "defines", "iff", "where", "complex", "orient",
        "match", "yields", "applies-to", "steps", "result", "solve-for", "quantities",
        "level", "course", "tags", "explain", "aliases", "refs", "see", "verify", "sample",
        "implemented-by", "rationale"]
LEVELS = ["Arithmetic", "PreAlgebra", "Algebra1", "Geometry", "Algebra2", "PreCalculus",
          "Calculus1", "Calculus2", "Calculus3", "University", "Advanced"]
COURSES = {"Algebra", "Trigonometry", "Proofs", "PreCalculus", "Calculus", "DifferentialEquations",
           "LinearAlgebra", "FiniteMath", "NumericalAnalysis"}
PREFIXES = {"alg", "trig", "logic", "pre", "calc", "ode", "linalg", "fin", "num", "conv"}
REQUIRED = {
    "law": [["statement"]],
    "formula": [["statement"]],
    "theorem": [["then"]],
    "pattern": [["match"], ["yields"]],
    "method": [["applies-to"]],
    "definition": [["defines", "statement"]],
    "axiom": [["statement"], ["refs"]],
    "convention": [["statement", "rationale"]],
}
NEEDS_LEVEL = {"definition", "axiom", "law", "theorem", "formula", "pattern", "method"}
KEBAB = re.compile(r"^[a-z0-9]+(-[a-z0-9]+)*$")
DOTTED = re.compile(r"^[a-z0-9]+(-[a-z0-9]+)*(\.[a-z0-9]+(-[a-z0-9]+)*)*$")

# Names that may appear free in statements without being declared.
KNOWN = {
    # constants and sets
    "pi", "e", "I", "oo", "inf", "zoo", "undefined", "true", "false", "EmptySet", "N", "Z", "Q", "R", "C",
    "GoldenRatio", "EulerGamma", "CatalanG",
    # keywords
    "and", "or", "not", "xor", "nand", "nor", "in", "mod", "forall", "exists", "where", "for", "some",
    "odd", "even", "over", "on", "of", "then", "otherwise", "Piecewise",
    # functions and operators (05-syntax-trees-and-notation.md, abbreviated)
    "sqrt", "root", "abs", "sign", "floor", "ceil", "round", "frac", "quo", "gcd", "lcm", "max", "min",
    "binomial", "perm", "multinomial", "re", "im", "conj", "arg", "cis", "exp", "ln", "log", "lambertw", "W",
    "sin", "cos", "tan", "cot", "sec", "csc", "arcsin", "arccos", "arctan", "arccot", "arcsec", "arccsc",
    "asin", "acos", "atan", "atan2", "sinh", "cosh", "tanh", "coth", "sech", "csch", "arsinh", "arcosh",
    "artanh", "arcoth", "arsech", "arcsch", "asinh", "acosh", "atanh", "gamma", "beta", "digamma", "erf",
    "erfc", "erfi", "zeta", "polylog", "besselj", "bessely", "besseli", "besselk", "airyai", "airybi",
    "ellipk", "ellipe", "si", "ci", "ei", "li", "fresnels", "fresnelc", "heaviside", "dirac", "kronecker",
    "legendreP", "hermiteH", "laguerreL", "chebyshevT", "chebyshevU", "jacobiP", "gegenbauerC",
    "totient", "mobius", "divisorSigma", "primePi", "prime", "fibonacci", "lucas", "catalan", "stirling1",
    "stirling2", "bell", "partitions", "harmonic", "divides", "diff", "integrate", "limit", "sum", "product",
    "grad", "divergence", "curl", "laplacian", "jacobian", "hessian", "transpose", "inverse", "det", "tr",
    "trace", "rank", "adj", "dot", "cross", "outer", "kron", "norm", "rref", "nullspace", "colspace",
    "rowspace", "eigenvalues", "eigenvectors", "charpoly", "identity", "zeros", "diag", "proj", "P", "E",
    "Var", "Cov", "corr", "deg", "content", "primitive", "monic", "continuous", "differentiable",
    "integrable", "laplace", "fourier", "Union", "argmin", "argmax", "O", "u", "T", "H", "dx", "dy", "dt",
}


def strip_comment(raw):
    """Drop a trailing # comment that is outside double quotes."""
    in_q = False
    for i, ch in enumerate(raw):
        if ch == '"':
            in_q = not in_q
        elif ch == "#" and not in_q and (i == 0 or raw[i - 1].isspace()):
            return raw[:i].rstrip()
    return raw.rstrip()


def lint(path):
    errors, warnings = [], []
    try:
        lines = open(path, encoding="utf-8").read().splitlines()
    except OSError as ex:
        return [f"{path}: {ex}"], []

    def err(n, msg):
        errors.append(f"{path}:{n}: error: {msg}")

    def warn(n, msg):
        warnings.append(f"{path}:{n}: warning: {msg}")

    prefix = None
    entries = []
    cur = None
    last_key = None
    for n, raw in enumerate(lines, 1):
        line = strip_comment(raw)
        if raw.lstrip().startswith("#") or not line.strip():
            continue
        if not raw.startswith((" ", "\t")):
            m = re.match(r'^domain\s+(\S+)\s+"[^"]*"\s*$', line)
            if m:
                if prefix:
                    err(n, "more than one domain header")
                prefix = m.group(1)
                if not DOTTED.match(prefix):
                    err(n, f"domain prefix '{prefix}' is not lowercase dotted kebab-case")
                elif prefix.split(".")[0] not in PREFIXES:
                    err(n, f"unknown domain prefix '{prefix.split('.')[0]}' (expected one of {sorted(PREFIXES)})")
                continue
            if re.match(r"^uses\s+\S+\s*$", line):
                continue
            m = re.match(r'^(\w+)\s+(\S+)\s+"([^"]*)"\s*$', line)
            if not m:
                err(n, f"expected 'KIND name \"Title\"', got: {line.strip()}")
                cur = None
                continue
            kind, name, title = m.groups()
            if kind not in KINDS:
                err(n, f"unknown kind '{kind}'")
            if not KEBAB.match(name):
                err(n, f"name '{name}' is not lowercase kebab-case")
            if not prefix:
                err(n, "entry before the 'domain' header")
            cur = {"kind": kind, "name": name, "line": n, "fields": {}, "order": []}
            entries.append(cur)
            last_key = None
            continue
        if cur is None:
            continue
        m = re.match(r"^\s+([a-z-]+)\s*:\s*(.*)$", line)
        if m and (m.group(1) in KEYS or last_key is None or len(raw) - len(raw.lstrip()) <= 4):
            key, val = m.group(1), m.group(2).strip()
            if key not in KEYS:
                err(n, f"unknown field '{key}'")
                continue
            if key in cur["fields"]:
                err(n, f"duplicate field '{key}' in {cur['name']}")
            cur["fields"][key] = val
            cur["order"].append((key, n))
            last_key = key
        elif last_key:
            cur["fields"][last_key] += "\n" + line.strip()
        else:
            err(n, f"continuation line with no field: {line.strip()}")

    if not prefix:
        errors.append(f"{path}:1: error: missing 'domain PREFIX \"Title\"' header")

    seen = {}
    for e in entries:
        n, kind, f = e["line"], e["kind"], e["fields"]
        full_id = f"{prefix}.{e['name']}" if prefix else e["name"]
        if full_id in seen:
            err(n, f"duplicate ID {full_id} (first at line {seen[full_id]})")
        seen[full_id] = n
        for alternatives in REQUIRED.get(kind, []):
            if not any(k in f for k in alternatives):
                err(n, f"{full_id}: {kind} needs {' or '.join(alternatives)}")
        if kind in NEEDS_LEVEL and "level" not in f:
            err(n, f"{full_id}: missing level")
        if "level" in f and f["level"] not in LEVELS:
            err(n, f"{full_id}: unknown level '{f['level']}'")
        if "course" in f:
            for c in [c.strip() for c in f["course"].split(",")]:
                if c not in COURSES:
                    err(n, f"{full_id}: unknown course '{c}'")
        if "orient" in f and f["orient"] not in {"ltr", "rtl", "both", "none"}:
            err(n, f"{full_id}: orient must be ltr, rtl, both or none")
        if kind == "law" and "orient" not in f:
            warn(n, f"{full_id}: law without orient (engines will not rewrite with it)")
        if f.get("orient") == "both" and "|" not in f.get("tags", ""):
            warn(n, f"{full_id}: orient both usually names two rule sets in tags (left | right)")
        if f.get("verify") == "none" and "rationale" not in f:
            err(n, f"{full_id}: verify: none requires rationale")
        if "verify" in f and f["verify"] not in {"numeric", "instances", "proof", "none"}:
            err(n, f"{full_id}: unknown verify '{f['verify']}'")
        if kind in {"law", "pattern"} and "explain" not in f:
            warn(n, f"{full_id}: no explain template")
        for ref_key in ("see", "aliases"):
            for ref in [r.strip() for r in f.get(ref_key, "").split(",") if r.strip()]:
                if not DOTTED.match(ref) or ref.count(".") < 1:
                    err(n, f"{full_id}: {ref_key} entry '{ref}' is not a dotted catalog ID")
        # undeclared variables
        if "vars" in f:
            declared = set()
            for part in f["vars"].split(","):
                nm = part.split(":")[0].strip()
                if nm:
                    declared.add(nm)
            text = " ".join(f.get(k, "") for k in ("statement", "given", "then", "where", "complex", "match",
                                                    "yields", "applies-to", "result", "defines", "iff"))
            text = re.sub(r'"[^"]*"', " ", text)
            text = re.sub(r"\d+[a-zA-Z]", lambda m: " " + m.group(0)[-1], text)  # 2a -> a
            used = set(re.findall(r"(?<![\w.'])([A-Za-z][A-Za-z0-9_]*)", text))
            # bound variables of binders and quantifiers
            bound = set(re.findall(r"(?:sum|product|integrate|limit|Union)\([^,]+,\s*([A-Za-z]\w*)", text))
            bound |= set(re.findall(r"(?:forall|exists!?)\s+([A-Za-z]\w*)", text))
            bound |= set(re.findall(r"([A-Za-z]\w*)\s*->", text))
            undeclared = sorted(u for u in used - declared - KNOWN - bound if len(u) <= 3 or u.islower())
            undeclared = [u for u in undeclared if u not in KNOWN]
            if undeclared:
                warn(n, f"{full_id}: symbols not in vars (declare them or ignore if they are operators): {', '.join(undeclared)}")
        elif kind in {"law", "formula", "pattern", "method", "theorem"}:
            err(n, f"{full_id}: missing vars")
    return errors, warnings


def main():
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    all_e, all_w, count = [], [], 0
    for p in sys.argv[1:]:
        e, w = lint(p)
        all_e += e
        all_w += w
    for line in all_e + all_w:
        print(line)
    print(f"{len(all_e)} error(s), {len(all_w)} warning(s)")
    sys.exit(1 if all_e else 0)


if __name__ == "__main__":
    main()
