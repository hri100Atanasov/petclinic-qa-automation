using NBomber;
using NBomber.Contracts;
using NBomber.CSharp;
using PetClinic.PerformanceTests.Config;
using PetClinic.PerformanceTests.Support;

namespace PetClinic.PerformanceTests.Scenarios;

/// <summary>
/// Test 5 — ramp-to-failure against GET /api/invoices, to find where the
/// application actually starts to degrade.
///
/// **Why the other four tests can't answer this.** Tests 1, 3 and 4 use a
/// closed model with 1s think time, which self-throttles: 10 users each
/// pausing a second between requests caps throughput at ~10 req/s no matter
/// how fast the server is. That models real usage well, but it means those
/// tests can never approach a capacity limit — the load simply never gets
/// high enough. Finding a limit needs an open model, where the arrival rate
/// is imposed rather than gated by user pacing.
///
/// **Why reads.** GET /api/invoices is the only endpoint measured here with a
/// 0% error rate. Ramping writes would conflate capacity limits with the
/// invoice-number race (Defect #8, DEFECTS.md), which already fails ~10% of
/// writes at trivial load and roughly half at 50 req/s — any error curve would
/// be that defect, not saturation. Reads isolate capacity cleanly.
///
/// Ramps 0 -> RampToRps over RampDuration and watches for whichever gives
/// first: latency knee, error onset, or HikariCP connection queuing (visible
/// in the metrics CSV as hikaricp_pending leaving 0 while active sits at the
/// pool max of 10).
/// </summary>
public static class Test5ReadRampToFailure
{
    private static readonly string?[] StatusPool = [null, "DRAFT", "ISSUED", "PAID"];

    public static ScenarioProps Build(ClientPool<ReceptionistSession> receptionists)
    {
        return Scenario.Create("test5_read_ramp_to_failure", async context =>
        {
            var session = receptionists.GetClient(context.ScenarioInfo);
            var page = Random.Shared.Next(0, 5);
            var status = StatusPool[Random.Shared.Next(StatusPool.Length)];

            var url = status is null
                ? $"/api/invoices?page={page}&size=20"
                : $"/api/invoices?page={page}&size=20&status={status}";

            // No think time here by design — this test imposes an arrival rate
            // rather than modelling a user's pacing.
            return await Step.Run("list_invoices", context, async () =>
            {
                var response = await session.Http.GetAsync(url);
                var check = await Assertions.ValidateInvoiceList(response, page);
                return check.Ok
                    ? Response.Ok(statusCode: check.StatusCode)
                    : Response.Fail(statusCode: check.StatusCode, message: check.Message);
            });
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            Simulation.RampingInject(
                rate: Settings.RampToRps,
                interval: TimeSpan.FromSeconds(1),
                during: Settings.RampDuration)
        );
    }
}
