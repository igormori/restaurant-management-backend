namespace RestaurantManagement.Modules.Identity.Services.Lockout
{
    // Stub (tester): the English account-locked email (task T2).
    public static class AccountLockedEmailTemplate
    {
        public static string Subject() => throw new NotImplementedException();

        public static string Build(string firstName, int lockoutMinutes) => throw new NotImplementedException();
    }
}
