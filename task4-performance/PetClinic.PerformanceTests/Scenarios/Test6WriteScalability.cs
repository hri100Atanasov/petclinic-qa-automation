using System.Net.Http.Json;
using NBomber;
using NBomber.Contracts;
using NBomber.CSharp;
using PetClinic.PerformanceTests.Config;
using PetClinic.PerformanceTests.Support;

namespace PetClinic.PerformanceTests.Scenarios;

/// <summary>
/// Test 6 — write-path scalability. Injects invoice creations at a fixed rate
/// (WRITE_RATE_RPS, default 10) for 20s, so the same test run at several rates
/// produces a scalability curve: offered rate vs. rate actually completing
/// successfully.
///
/// **Why writes, and why this is the more informative ramp.** Test 5 ramps
/// reads and finds a capacity limit — connection-pool saturation — which is a
/// generic Spring Boot configuration ceiling, not a property of this
/// application's design. Ramping writes instead exercises the path where this
/// application is actually architecturally weak: invoice-number allocation is
/// not concurrency-safe (Performance Defect #1), so added concurrency does not
/// buy added throughput, it buys collisions.
///
/// **Every failure here is self-attributing**, which is the other reason to
/// prefer this over a read ramp. A failed write returns HTTP 500 carrying
/// `duplicate key value violates unique constraint "invoices_invoice_no_key"` —
/// unambiguously the application rejecting the request. Test 5's failures at
/// high rates were client-side transport exceptions, which required a separate
/// investigation to prove were host artifacts rather than server behaviour.
/// Because the write path collides at a concurrency of 2, the whole curve is
/// measurable at rates far too low to stress the load generator's host, so that
/// ambiguity cannot arise here.
/// </summary>
public static class Test6WriteScalability
{
    public static ScenarioProps Build(ClientPool<ReceptionistSession> receptionists, List<int> ownerPool)
    {
        int[] discountPool = [0, 10, 20, 50];

        return Scenario.Create("test6_write_scalability", async context =>
        {
            var session = receptionists.GetClient(context.ScenarioInfo);
            var ownerId = ownerPool[Random.Shared.Next(ownerPool.Count)];
            var discountPct = discountPool[Random.Shared.Next(discountPool.Length)];

            return await Step.Run("create_invoice", context, async () =>
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
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            // Fixed rate, not ramping: each run measures one point on the curve
            // cleanly, rather than blending every rate into one aggregate.
            Simulation.Inject(
                rate: Settings.WriteRateRps,
                interval: TimeSpan.FromSeconds(1),
                during: Settings.WriteRateDuration)
        );
    }
}
