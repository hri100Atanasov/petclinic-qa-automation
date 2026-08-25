using NBomber;
using NBomber.Contracts;
using NBomber.CSharp;
using PetClinic.PerformanceTests.Support;

namespace PetClinic.PerformanceTests.Scenarios;

/// <summary>
/// Test 3 — read-heavy load against GET /api/invoices. Same shape as Test 1
/// (ramp one user in per second up to 10, hold for the rest of a 20s run,
/// 1s think time) so the two are directly comparable: same concurrency
/// profile, opposite read/write mix.
/// </summary>
public static class Test3ReadHeavyList
{
    // Small spread of page/status combinations so this isn't "GET the same
    // URL a thousand times" — status includes null (no filter) as one of
    // the options, matching the unfiltered worklist view being the most
    // common real case.
    private static readonly string?[] StatusPool = [null, "DRAFT", "ISSUED", "PAID"];

    public static ScenarioProps Build(ClientPool<ReceptionistSession> receptionists)
    {
        return Scenario.Create("test3_read_heavy_list", async context =>
        {
            var session = receptionists.GetClient(context.ScenarioInfo);
            var page = Random.Shared.Next(0, 5);
            var status = StatusPool[Random.Shared.Next(StatusPool.Length)];

            var url = status is null
                ? $"/api/invoices?page={page}&size=20"
                : $"/api/invoices?page={page}&size=20&status={status}";

            // Step wraps only the HTTP call — its latency stats are server
            // response time, excluding the think time below.
            var result = await Step.Run("list_invoices", context, async () =>
            {
                var response = await session.Http.GetAsync(url);
                var check = await Assertions.ValidateInvoiceList(response, page);
                return check.Ok
                    ? Response.Ok(statusCode: check.StatusCode)
                    : Response.Fail(statusCode: check.StatusCode, message: check.Message);
            });

            await Task.Delay(TimeSpan.FromSeconds(1));

            return result;
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            Simulation.RampingConstant(copies: 10, during: TimeSpan.FromSeconds(10)),
            Simulation.KeepConstant(copies: 10, during: TimeSpan.FromSeconds(10))
        );
    }
}
