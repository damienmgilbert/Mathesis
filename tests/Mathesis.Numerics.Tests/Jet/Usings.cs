// The suite names the jet type only through these aliases (ADR-19; semantics in docs/design/04-type-system.md, "Jet semantics").
global using JetD = Mathesis.Numbers.Jet<double>;
global using JetF = Mathesis.Numbers.Jet<float>;
global using JetJetD = Mathesis.Numbers.Jet<Mathesis.Numbers.Jet<double>>;
global using JetDiff = Mathesis.Numerics.Differentiation.JetDifferentiation;
