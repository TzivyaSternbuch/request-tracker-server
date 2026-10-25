namespace RequestTracker.Infrastructure.Persistence;

internal static class CheckConstraints
{
    // Built from the enum, so adding a member changes the constraint SQL and EF puts it in the next migration.
    public static string EnumValues<TEnum>(string column) where TEnum : struct, Enum =>
        $"[{column}] IN ({string.Join(", ", Enum.GetValues<TEnum>().Select(value => Convert.ToInt32(value)))})";
}
