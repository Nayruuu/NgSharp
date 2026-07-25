using System;

using BenchmarkDotNet.Running;

using NgSharp.Benchmark;

if (args.Length > 0 && args[0] == "smoke") { ProgramModes.Smoke(); return; }
if (args.Length > 0 && args[0] == "realistic-verify") { ProgramModes.RealisticVerify(); return; }
if (args.Length > 0 && args[0] == "realistic-time") { ProgramModes.RealisticTime(args); return; }
if (args.Length > 0 && args[0] == "realistic-render") { ProgramModes.RealisticRender(args); return; }
if (args.Length > 0 && args[0] == "enginecmp") { ProgramModes.EngineCmp(); return; }
if (args.Length > 0 && args[0] == "alloc") { ProgramModes.Alloc(); return; }
if (args.Length > 0 && args[0] == "parse-alloc") { ProgramModes.ParseAlloc(); return; }
if (args.Length > 0 && args[0] == "showsmoke") { ProgramModes.ShowSmoke(); return; }
if (args.Length > 0 && args[0] == "showcmp") { ProgramModes.ShowCmp(); return; }
if (args.Length > 0 && args[0] == "textcmp") { ProgramModes.TextCmp(); return; }

// Both benchmark suites are first-class: `dotnet run` runs all; target one with e.g.
// `dotnet run -c Release -- --filter *Showcase*` or `--filter *Engine*`.
BenchmarkSwitcher
    .FromTypes(new[] { typeof(EngineComparisonBenchmark), typeof(FeatureShowcaseBenchmark), typeof(RealisticDocumentBenchmark), typeof(TextDocumentBenchmark) })
    .Run(args.Length == 0 ? new[] { "--filter", "*" } : args);
