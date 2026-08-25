using System.Net.Http.Json;
using System.Text.Json;
using NBomber;
using NBomber.Contracts;
using NBomber.CSharp;
using PetClinic.PerformanceTests.Support;

namespace PetClinic.PerformanceTests.Scenarios;

/// <summary>
/// Test 1 — the original POC ("create a thousand invoices"), reworked into a
/// proper load model: ramp-up (one user added per second until all 10 are
/// active), then hold at 10 concurrent users for the rest of a 20-second
/// run, with 1s think time between each user's requests. Closed model
/// (RampingConstant + KeepConstant) because we're modeling a fixed number of
/// concurrent front-desk sessions, not an open-ended arrival rate.
/// </summary>
public static class Test1CreateInvoiceRampUp
{
    public static ScenarioProps Build(ClientPool<ReceptionistSession> receptionists, List<int> ownerPool)
    {
        int[] discountPool = [0, 10, 20, 50];

        return Scenario.Create("test1_create_invoice_rampup", async context =>
        {
            var session = receptionists.GetClient(context.ScenarioInfo);
            var ownerId = ownerPool[Random.Shared.Next(ownerPool.Count)];
            var discountPct = discountPool[Random.Shared.Next(discountPool.Length)];

            // Only the HTTP call is inside the step, so the reported latency is
            // the server's response time. NBomber derives scenario latency from
            // its steps, so the think time below is excluded from the
            // percentiles entirely while still pacing the run — it shows up in
            // throughput (RPS), which is where it belongs.
            var result = await Step.Run("create_invoice", context, async () =>
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

            // Think time: 1s pause between this user's requests, per the
            // agreed load model — applied regardless of pass/fail so a
            // string of failures doesn't silently turn into a tighter,
            // unintended retry loop.
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
