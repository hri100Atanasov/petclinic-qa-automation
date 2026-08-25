using System.Globalization;
using System.Net.Http.Json;
using NBomber;
using NBomber.Contracts;
using NBomber.CSharp;
using PetClinic.PerformanceTests.Config;
using PetClinic.PerformanceTests.Support;

namespace PetClinic.PerformanceTests.Scenarios;

/// <summary>
/// Test 2 — all 10 payments of $10 fired simultaneously at the same $100
/// invoice. No ramp-up, no think time: this isn't a model of realistic usage,
/// it's a targeted probe for a write-write race on one row.
///
/// The interesting assertion isn't per-request — a single $10 payment against
/// a $100 balance looks individually valid whether or not the concurrent
/// updates interfered. The real check is the post-run GET in Program.cs
/// (untimed, excluded from the percentile stats): 10 payment records,
/// amountPaid 100.00, balance 0.00, status PAID.
///
/// **Why the barrier.** NBomber starts 10 virtual users concurrently, but not
/// at the same instant — its own scheduling spreads their first requests over
/// a few milliseconds, which is enough for some runs to serialize cleanly and
/// reach PAID. Measured directly: without the barrier this scenario reproduced
/// the defect in 2 of 5 runs; holding all 10 requests and releasing them
/// together, 9 of 10. The barrier doesn't manufacture the defect — it removes
/// the scheduling jitter that was hiding it, so the test is a dependable
/// regression guard rather than a coin flip. Real-world concurrency doesn't
/// need this precision to hit the same bug; it just hits it less often than a
/// test should.
///
/// It remains a race, so ~1 run in 10 still passes. A single PASS is therefore
/// not evidence the defect is fixed — re-run before concluding that.
/// </summary>
public static class Test2ConcurrentPayments
{
    /// <summary>Safety valve: if fewer than PaymentCount users ever arrive (an
    /// NBomber behaviour change, a failed login), release anyway rather than
    /// hanging the run forever.</summary>
    private static readonly TimeSpan BarrierTimeout = TimeSpan.FromSeconds(10);

    public static ScenarioProps Build(ClientPool<ReceptionistSession> receptionists, int invoiceId)
    {
        var arrived = 0;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        return Scenario.Create("test2_concurrent_payments", async context =>
        {
            var session = receptionists.GetClient(context.ScenarioInfo);

            // Warm this user's connection *before* the barrier. Each virtual user
            // has its own HttpClient, so an un-warmed one would spend its first
            // few milliseconds after release on TCP setup instead of the payment —
            // reintroducing exactly the stagger the barrier exists to remove.
            // Measured across runs: no barrier, 2 of 5; barrier alone, 4 of 5;
            // barrier plus this warm-up, 9 of 10.
            await session.Http.GetAsync($"/api/invoices/{invoiceId}");

            // Barrier: every user waits here until all PaymentCount of them have
            // arrived, so the payments hit the API together rather than smeared
            // across NBomber's start-up window.
            if (Interlocked.Increment(ref arrived) == Settings.PaymentCount)
            {
                release.TrySetResult();
            }
            await Task.WhenAny(release.Task, Task.Delay(BarrierTimeout));

            // Step wraps only the payment call. Without it, the scenario's
            // latency would also include the warm-up GET and however long this
            // user sat at the barrier waiting for the others — neither of which
            // is the server's response time.
            return await Step.Run("pay_invoice", context, async () =>
            {
                var response = await session.Http.PostAsJsonAsync($"/api/invoices/{invoiceId}/payments", new
                {
                    amount = Settings.PaymentAmount,
                    method = "CASH"
                });

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    return Response.Fail(statusCode: ((int)response.StatusCode).ToString(),
                        message: $"POST /payments returned {(int)response.StatusCode}: {Assertions.Truncate(body)}");
                }

                var invoice = await response.Content.ReadFromJsonAsync<InvoiceResponse>();
                if (invoice is null || invoice.Status is not ("PARTIALLY_PAID" or "PAID"))
                {
                    var amount = Settings.PaymentAmount.ToString(CultureInfo.InvariantCulture);
                    return Response.Fail(message: $"Unexpected status after a ${amount} payment: '{invoice?.Status}'");
                }

                return Response.Ok(statusCode: ((int)response.StatusCode).ToString());
            });
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            Simulation.IterationsForConstant(copies: Settings.PaymentCount, iterations: Settings.PaymentCount)
        );
    }
}
