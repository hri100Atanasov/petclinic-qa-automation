namespace PetClinic.PerformanceTests.Support;

/// <summary>
/// One seeded RECEPTIONIST account's own authenticated HttpClient. Pooled via
/// NBomber's ClientPool and looked up per iteration with
/// context.ScenarioInfo.InstanceNumber, so a given virtual user in a
/// scenario consistently acts as the same receptionist for its whole
/// lifetime in that run — not a different, randomly-picked account every
/// request.
/// </summary>
public sealed class ReceptionistSession(string username, HttpClient http) : IDisposable
{
    public string Username { get; } = username;
    public HttpClient Http { get; } = http;

    public void Dispose() => Http.Dispose();
}
