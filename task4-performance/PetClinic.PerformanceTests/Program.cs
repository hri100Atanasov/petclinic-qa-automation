using NBomber;
using NBomber.Contracts.Stats;
using NBomber.CSharp;
using PetClinic.PerformanceTests.Config;
using PetClinic.PerformanceTests.Metrics;
using PetClinic.PerformanceTests.Scenarios;
using PetClinic.PerformanceTests.Support;

var apiBaseUrl = Settings.ApiBaseUrl;
var reportsDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "reports");
Directory.CreateDirectory(reportsDir);

var mode = args.FirstOrDefault(a => !a.StartsWith('-')) ?? "all";
var allTests = new[] { "test1", "test2", "test3", "test4", "test5", "test6" };
var toRun = mode switch
{
    "test1" or "test2" or "test3" or "test4" or "test5" or "test6" => [mode],
    "all" => allTests,
    _ => null
};

if (toRun is null)
{
    Console.Error.WriteLine($"Unknown mode '{mode}'. Usage: dotnet run -- {{test1|test2|test3|test4|test5|test6|all}}");
    return 2;
}

using var setupHttp = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };
var setupClient = new PetClinicClient(setupHttp);

Console.WriteLine("=== Task 4 performance tests ===");
Console.WriteLine($"AUT API: {apiBaseUrl}");
Console.WriteLine($"Running: {string.Join(", ", toRun)}");

Console.WriteLine("Checking readiness (/actuator/health)...");
if (!await setupClient.IsHealthyAsync())
{
    Console.Error.WriteLine($"""

        ============================================================
         PetClinic Pro does not appear to be running or healthy.
         Checked: {apiBaseUrl}/actuator/health

         Start it first, then re-run this test:
           cd qa-test-automation-task
           docker compose up
        ============================================================
        """);
    return 1;
}

var runId = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

Console.WriteLine("Logging in as admin...");
var adminToken = await setupClient.LoginAsync(Settings.AdminUsername, Settings.AdminPassword);

Console.WriteLine($"Creating owner pool ({Settings.OwnerPoolSize} owners)...");
var ownerPool = new List<int>();
for (var i = 0; i < Settings.OwnerPoolSize; i++)
{
    ownerPool.Add(await setupClient.CreateOwnerAsync(adminToken, $"{runId}-{i}"));
}

Console.WriteLine($"Seeding {Settings.ReceptionistPoolSize} RECEPTIONIST accounts...");
using var receptionistPool = new ClientPool<ReceptionistSession>();
for (var i = 0; i < Settings.ReceptionistPoolSize; i++)
{
    var (username, token) = await setupClient.CreateReceptionistAsync(adminToken, $"{runId}-{i}");
    var http = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };
    http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
    receptionistPool.AddClient(new ReceptionistSession(username, http));
    Console.WriteLine($"  - {username}");
}

var exitCode = 0;

foreach (var test in toRun)
{
    Console.WriteLine();
    Console.WriteLine($"=== Running {test} ===");

    var metricsCsvPath = Path.Combine(reportsDir, $"{test}-metrics-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
    using var metricsHttp = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };
    var poller = new MetricsPoller(metricsHttp, adminToken, metricsCsvPath);
    using var metricsCts = new CancellationTokenSource();
    var pollerTask = poller.RunAsync(TimeSpan.FromSeconds(1), metricsCts.Token);
    Console.WriteLine($"Metrics poller writing to: {metricsCsvPath}");

    int? test2InvoiceId = null;

    var scenario = test switch
    {
        "test1" => Test1CreateInvoiceRampUp.Build(receptionistPool, ownerPool),
        "test2" => await BuildTest2Async(),
        "test3" => Test3ReadHeavyList.Build(receptionistPool),
        "test4" => Test4MixedReadWrite.Build(receptionistPool, ownerPool),
        "test5" => Test5ReadRampToFailure.Build(receptionistPool),
        "test6" => Test6WriteScalability.Build(receptionistPool, ownerPool),
        _ => throw new InvalidOperationException($"Unknown test '{test}'")
    };

    var stats = NBomberRunner
        .RegisterScenarios(scenario)
        .WithReportFolder(reportsDir)
        .WithReportFileName($"{test}-{DateTime.UtcNow:yyyyMMdd-HHmmss}")
        .WithReportFormats(ReportFormat.Html, ReportFormat.Csv, ReportFormat.Md)
        .Run();

    await metricsCts.CancelAsync();
    try { await pollerTask; } catch (OperationCanceledException) { }
    Console.WriteLine($"Metrics CSV written to: {metricsCsvPath}");

    if (stats.ScenarioStats.Any(s => s.Fail.Request.Count > 0))
    {
        exitCode = 1;
    }

    if (test == "test2" && test2InvoiceId is { } invoiceId)
    {
        await VerifyTest2Async(invoiceId);
    }

    continue;

    async Task<NBomber.Contracts.ScenarioProps> BuildTest2Async()
    {
        Console.WriteLine($"Creating the shared ${Settings.Test2InvoiceTotal} invoice for Test 2...");
        var ownerId = ownerPool[0];
        var invoiceId = await setupClient.CreateInvoiceAsync(adminToken, ownerId, taxRate: 0m, discountPct: 0m);
        await setupClient.AddItemAsync(adminToken, invoiceId, "Consultation", "SERVICE",
            quantity: 1, unitPrice: Settings.Test2InvoiceTotal);
        await setupClient.IssueInvoiceAsync(adminToken, invoiceId);
        Console.WriteLine($"  Invoice #{invoiceId}, balance {Settings.Test2InvoiceTotal:0.00}, ISSUED.");
        test2InvoiceId = invoiceId;
        return Test2ConcurrentPayments.Build(receptionistPool, invoiceId);
    }
}

async Task VerifyTest2Async(int invoiceId)
{
    Console.WriteLine();
    Console.WriteLine("=== Test 2 post-run integrity check ===");
    var invoice = await setupClient.GetInvoiceAsync(adminToken, invoiceId);

    var issues = new List<string>();
    if (invoice.Payments.Count != Settings.PaymentCount)
    {
        issues.Add($"Expected {Settings.PaymentCount} payment records, found {invoice.Payments.Count}.");
    }
    if (invoice.Totals.AmountPaid != Settings.Test2InvoiceTotal)
    {
        issues.Add($"Expected amountPaid {Settings.Test2InvoiceTotal:0.00}, got {invoice.Totals.AmountPaid}.");
    }
    if (invoice.Totals.Balance != 0.00m)
    {
        issues.Add($"Expected balance 0.00, got {invoice.Totals.Balance}.");
    }
    if (invoice.Status != "PAID")
    {
        issues.Add($"Expected status PAID, got '{invoice.Status}'.");
    }

    if (issues.Count == 0)
    {
        Console.WriteLine($"PASS — invoice #{invoiceId}: {Settings.PaymentCount}/{Settings.PaymentCount} payments recorded, " +
                          $"amountPaid {Settings.Test2InvoiceTotal:0.00}, balance 0.00, status PAID.");
    }
    else
    {
        Console.WriteLine($"FAIL — invoice #{invoiceId} did not reach a consistent paid state after " +
                          $"{Settings.PaymentCount} concurrent ${Settings.PaymentAmount:0.00} payments:");
        foreach (var issue in issues)
        {
            Console.WriteLine($"  - {issue}");
        }
        exitCode = 1;
    }
}

Console.WriteLine();
Console.WriteLine("=== Done ===");
return exitCode;
