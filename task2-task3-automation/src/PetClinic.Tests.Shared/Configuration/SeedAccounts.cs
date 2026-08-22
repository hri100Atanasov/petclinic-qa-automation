namespace PetClinic.Tests.Shared.Configuration;

/// <summary>
/// Fixed seed accounts documented in the AUT's own README. Not environment-driven
/// like TestSettings — these are constants of the application's seed data, not
/// something a different environment would plausibly override.
/// </summary>
public static class SeedAccounts
{
    public static readonly (string Username, string Password) Reception = ("reception", "desk123");
    public static readonly (string Username, string Password) Vet = ("vet.carter", "vet123");
    public static readonly (string Username, string Password) Auditor = ("auditor", "audit123");
    public static readonly (string Username, string Password) FormerStaff = ("former.staff", "old123");
}
