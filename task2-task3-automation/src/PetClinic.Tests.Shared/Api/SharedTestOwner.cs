namespace PetClinic.Tests.Shared.Api;

/// <summary>
/// The single owner created once per test-assembly run (see each project's
/// AssemblySetup.EnsureAppIsRunning), reused by every test that needs an owner
/// instead of each one creating its own.
///
/// The invoice-creation UI's owner dropdown requests only
/// GET /api/owners?size=100, with no further pagination or search
/// (Defect #6, test-plan.md §8) — creating a fresh owner per test was silently
/// exceeding that cap after enough repeated runs, until the newest owner no
/// longer sorted into the dropdown's reachable first 100 at all, which is what
/// first surfaced this: the UI's S1 lifecycle test started intermittently
/// failing to find its own just-created owner in the dropdown.
/// </summary>
public static class SharedTestOwner
{
    public static OwnerResponse Owner { get; set; } = null!;
}
