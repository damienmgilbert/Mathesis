// Dependency policy check. Run: dotnet run eng/policy-check.cs
// Fails when a shipped project (src/) uses a package, direct or transitive, whose ID does not
// start with System., Microsoft. or CommunityToolkit., or when a package is deprecated or vulnerable.
using System.Diagnostics;
using System.Text.Json;

string[] allowedPrefixes = ["System.", "Microsoft.", "CommunityToolkit."];

var root = Directory.GetCurrentDirectory();
while (root is not null && !File.Exists(Path.Combine(root, "Mathesis.slnx")))
    root = Path.GetDirectoryName(root);
if (root is null)
{
    Console.Error.WriteLine("Run from inside the repository (Mathesis.slnx not found).");
    return 1;
}

var projects = Directory.GetFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories)
    .Order(StringComparer.Ordinal).ToArray();
var failures = new List<string>();
var packages = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal); // "id version" -> projects

(int Code, string Output) Dotnet(params string[] args)
{
    var psi = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = root };
    foreach (var a in args) psi.ArgumentList.Add(a);
    using var p = Process.Start(psi)!;
    var stdout = p.StandardOutput.ReadToEndAsync();
    var stderr = p.StandardError.ReadToEndAsync();
    p.WaitForExit();
    return (p.ExitCode, stdout.Result + stderr.Result);
}

foreach (var project in projects)
{
    var name = Path.GetFileNameWithoutExtension(project);

    // 1. Restore and collect every package, transitive included.
    var restore = Dotnet("restore", project);
    if (restore.Code != 0)
    {
        failures.Add($"{name}: restore failed\n{restore.Output}");
        continue;
    }
    var assetsPath = Path.Combine(Path.GetDirectoryName(project)!, "obj", "project.assets.json");
    using (var assets = JsonDocument.Parse(File.ReadAllText(assetsPath)))
    {
        foreach (var lib in assets.RootElement.GetProperty("libraries").EnumerateObject())
        {
            if (lib.Value.GetProperty("type").GetString() != "package") continue;
            var slash = lib.Name.IndexOf('/');
            var id = lib.Name[..slash];
            var version = lib.Name[(slash + 1)..];

            if (!packages.TryGetValue($"{id} {version}", out var users)) packages[$"{id} {version}"] = users = [];
            users.Add(name);

            // 2. Allowed IDs; runtime.* shims for System./Microsoft. packages are fine.
            var allowed = allowedPrefixes.Any(p => id.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                || (id.StartsWith("runtime.", StringComparison.OrdinalIgnoreCase)
                    && (id.Contains(".System.", StringComparison.OrdinalIgnoreCase) || id.Contains(".Microsoft.", StringComparison.OrdinalIgnoreCase)));
            if (!allowed) failures.Add($"{name}: package {id} {version} is not System.*, Microsoft.* or CommunityToolkit.*");
        }
    }

    // 3. Deprecated, then vulnerable.
    foreach (var flag in new[] { "--deprecated", "--vulnerable" })
    {
        var list = Dotnet("package", "list", "--project", project, "--include-transitive", flag, "--format", "json");
        if (list.Code != 0)
        {
            failures.Add($"{name}: dotnet package list {flag} failed\n{list.Output}");
            continue;
        }
        using var doc = JsonDocument.Parse(list.Output);
        if (doc.RootElement.TryGetProperty("problems", out var problems) && problems.GetArrayLength() > 0)
            failures.Add($"{name}: dotnet package list {flag} reported problems: {problems.GetRawText()}");
        foreach (var hit in FindHits(doc.RootElement, flag == "--deprecated" ? "deprecationReasons" : "vulnerabilities"))
            failures.Add($"{name}: {(flag == "--deprecated" ? "deprecated" : "vulnerable")} package {hit}");
    }
}

// 4. Report.
Console.WriteLine("Packages used by src/ projects:");
foreach (var (key, users) in packages) Console.WriteLine($"  {key}  ({string.Join(", ", users)})");
if (packages.Count == 0) Console.WriteLine("  (none)");

if (failures.Count > 0)
{
    Console.Error.WriteLine();
    foreach (var f in failures) Console.Error.WriteLine($"FAIL {f}");
    return 1;
}
Console.WriteLine("Policy check passed.");
return 0;

static IEnumerable<string> FindHits(JsonElement e, string property)
{
    if (e.ValueKind == JsonValueKind.Object)
    {
        if (e.TryGetProperty("id", out var id) && e.TryGetProperty(property, out _))
            yield return $"{id.GetString()} {(e.TryGetProperty("resolvedVersion", out var v) ? v.GetString() : "")}".TrimEnd();
        foreach (var p in e.EnumerateObject())
            foreach (var h in FindHits(p.Value, property)) yield return h;
    }
    else if (e.ValueKind == JsonValueKind.Array)
    {
        foreach (var item in e.EnumerateArray())
            foreach (var h in FindHits(item, property)) yield return h;
    }
}
