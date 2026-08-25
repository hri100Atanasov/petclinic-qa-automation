using System.Net.Http.Json;
using NBomber;
using NBomber.Contracts;
using NBomber.CSharp;
using PetClinic.PerformanceTests.Support;

namespace PetClinic.PerformanceTests.Scenarios;

/// <summary>
/// Test 4 — mixed read/write, same load shape as Test 3 (ramp to 10 users
/// over 10s, hold for 10s, 1s think time), but each iteration is a coin
/// flip: 50% GET /api/invoices, 50% POST /api/invoices.
///
/// Design choice worth flagging: NBomber has a first-class weighted
/// multi-scenario feature (Scenario.WithWeight) that would let read and
/// write latency/percentiles be reported as two separate distributions
/// instead of one blended one. It wasn't used here because its exact
/// concurrency semantics with two RampingConstant/KeepConstant simulations
/// registered side by side weren't verified live before this was built —
/// consistent with this project's own rule of not shipping an assumption
/// about a tool's behavior without checking it. This single-scenario
/// branch is fully within what was already confirmed working for Tests 1
/// and 3. If separated read vs. write percentiles turn out to matter, the
/// weighted-scenario version is the next thing to verify and switch to.
/// </summary>
public static class Test4MixedReadWrite
{
    private static readonly string?[] StatusPool = [null, "DRAFT", "ISSUED", "PAID"];

    public static ScenarioProps Build(ClientPool<ReceptionistSession> receptionists, List<int> ownerPool)
    {
        int[] discountPool = [0, 10, 20, 50];

        return Scenario.Create("test4_mixed_read_write", async context =>
        {
            var session = receptionists.GetClient(context.ScenarioInfo);
            var isWrite = Random.Shared.NextDouble() < 0.5;

            // Two separately named steps rather than one shared code path:
            // NBomber reports per-step stats, so read and write latency land
            // in their own distributions instead of one blended figure. Only
            // one of the two runs per iteration; the other simply records no
            // data point that iteration.
            IResponse result;
            if (isWrite)
            {
                var ownerId = ownerPool[Random.Shared.Next(ownerPool.Count)];
                var discountPct = discountPool[Random.Shared.Next(discountPool.Length)];
                result = await Step.Run("write_create_invoice", context, async () =>
                {
                    var response = await session.Http.PostAsJsonAsync("/api/invoices", new
                    {
                        ownerId,
                        taxRate = 0.10m,
                        discountPct
                    });
                    var check = await Assertions.ValidateCreatedInvoice(response);
                    return check.Ok
                        ? Response.Ok(statusCode: check.StatusCode)
                        : Response.Fail(statusCode: check.StatusCode, message: check.Message);
                });
            }
            else
            {
                var page = Random.Shared.Next(0, 5);
                var status = StatusPool[Random.Shared.Next(StatusPool.Length)];
                var url = status is null
                    ? $"/api/invoices?page={page}&size=20"
                    : $"/api/invoices?page={page}&size=20&status={status}";
                result = await Step.Run("read_invoice_list", context, async () =>
                {
                    var response = await session.Http.GetAsync(url);
                    var check = await Assertions.ValidateInvoiceList(response, page);
                    return check.Ok
                        ? Response.Ok(statusCode: check.StatusCode)
                        : Response.Fail(statusCode: check.StatusCode, message: check.Message);
                });
            }

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
