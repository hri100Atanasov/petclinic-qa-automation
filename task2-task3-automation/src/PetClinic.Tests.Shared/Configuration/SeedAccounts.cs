namespace PetClinic.Tests.Shared.Configuration;

/// <summary>
/// Fixed seed accounts documented in the AUT's own README. Not environment-driven
/// like TestSettings — these are constants of the application's seed data, not
/// something a different environment would plausibly override. Nested static
/// classes with const fields, rather than tuples, so the username/password can be
/// referenced directly as NUnit [TestCase] attribute arguments, which require
/// compile-time constants.
/// </summary>
public static class SeedAccounts
{
    public static class Admin
    {
        public const string Username = "admin";
        public const string Password = "admin123";
    }

    public static class Reception
    {
        public const string Username = "reception";
        public const string Password = "desk123";
    }

    public static class Vet
    {
        public const string Username = "vet.carter";
        public const string Password = "vet123";
    }

    public static class Auditor
    {
        public const string Username = "auditor";
        public const string Password = "audit123";
    }

    public static class FormerStaff
    {
        public const string Username = "former.staff";
        public const string Password = "old123";
    }
}

