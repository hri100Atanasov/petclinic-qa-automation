using System.Diagnostics;

// Local convenience runner: runs the requested suite(s) — either directly via
// `dotnet test` on this machine, or via `docker compose run` when --docker is
// passed — then opens the resulting HTML report(s) in the default browser.
// The container itself has no display to open a browser against, so this is
// what makes "run in Docker" + "see the report" a single command; the report
// still gets opened from here, on the host, after the container exits and its
// bind-mounted ./testresults is populated. docker/entrypoint.sh (used when a
// human isn't driving, e.g. `docker compose up`) never opens anything itself.

var mode = args.FirstOrDefault(a => !a.StartsWith('-')) ?? "all";
var openReports = !args.Contains("--no-open") && Environment.GetEnvironmentVariable("CI") is null;
var useDocker = args.Contains("--docker");

const string resultsDir = "testresults";
Directory.CreateDirectory(resultsDir);

var suites = new Dictionary<string, string>
{
    ["ui"] = "src/PetClinic.Tests.Ui/PetClinic.Tests.Ui.csproj",
    ["api"] = "src/PetClinic.Tests.Api/PetClinic.Tests.Api.csproj",
};

var toRun = mode switch
{
    "ui" => new[] { "ui" },
    "api" => new[] { "api" },
    "all" => new[] { "ui", "api" },
    _ => null,
};

if (toRun is null)
{
    Console.Error.WriteLine($"Unknown mode '{mode}'. Usage: dotnet run --project src/PetClinic.Tests.Runner -- {{ui|api|all}} [--docker] [--no-open]");
    return 2;
}

var results = new Dictionary<string, int>();

foreach (var suite in toRun)
{
    Console.WriteLine();
    Console.WriteLine($"=== Running {suite.ToUpperInvariant()} tests{(useDocker ? " (docker compose)" : "")} ===");

    var psi = useDocker
        ? DockerRunFor(suite)
        : DotnetTestFor(suite);

    using var process = Process.Start(psi)!;
    process.WaitForExit();
    results[suite] = process.ExitCode;
}

Console.WriteLine();
Console.WriteLine("=== Summary ===");
foreach (var suite in toRun)
{
    Console.WriteLine($"{suite.ToUpperInvariant(),-4} suite: {(results[suite] == 0 ? "PASSED" : "FAILED")}");
}
Console.WriteLine($"TRX + HTML reports written to ./{resultsDir}");

if (openReports)
{
    foreach (var suite in toRun)
    {
        var reportPath = Path.GetFullPath(Path.Combine(resultsDir, $"{suite}-report.html"));
        if (!File.Exists(reportPath))
        {
            continue;
        }

        try
        {
            Process.Start(new ProcessStartInfo(reportPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"(Could not auto-open {reportPath}: {ex.Message})");
        }
    }
}

return results.Values.Any(code => code != 0) ? 1 : 0;

ProcessStartInfo DotnetTestFor(string suite)
{
    var psi = new ProcessStartInfo("dotnet") { UseShellExecute = false };
    psi.ArgumentList.Add("test");
    psi.ArgumentList.Add(suites[suite]);
    psi.ArgumentList.Add("--logger");
    psi.ArgumentList.Add($"trx;LogFileName={suite}-results.trx");
    psi.ArgumentList.Add("--logger");
    psi.ArgumentList.Add($"html;LogFileName={suite}-report.html");
    psi.ArgumentList.Add("--results-directory");
    psi.ArgumentList.Add(resultsDir);
    return psi;
}

ProcessStartInfo DockerRunFor(string suite)
{
    // Same image/entrypoint as `docker compose run --rm tests <suite>` — the
    // container writes into ./testresults via the existing bind mount in
    // docker-compose.yml, so the files are on the host the moment it exits.
    var psi = new ProcessStartInfo("docker") { UseShellExecute = false };
    psi.ArgumentList.Add("compose");
    psi.ArgumentList.Add("run");
    psi.ArgumentList.Add("--rm");
    psi.ArgumentList.Add("tests");
    psi.ArgumentList.Add(suite);
    return psi;
}
