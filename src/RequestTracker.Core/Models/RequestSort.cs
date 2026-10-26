namespace RequestTracker.Core.Models;

public record RequestSort(RequestSortField Field, SortDirection Direction)
{
    public static readonly RequestSort Default = new(RequestSortField.CreatedAt, SortDirection.Desc);

    public static bool TryParse(string? value, out RequestSort sort)
    {
        sort = Default;
        var parts = (value ?? string.Empty).Split(':');
        if (parts.Length > 2
            || !TryParseName<RequestSortField>(parts[0], out var field)
            || !TryParseDirection(parts.Length == 2 ? parts[1] : null, out var direction))
        {
            return false;
        }

        sort = new RequestSort(field, direction);
        return true;
    }

    private static bool TryParseDirection(string? value, out SortDirection direction)
    {
        direction = SortDirection.Asc;
        return value is null || TryParseName(value, out direction);
    }

    // Names only: Enum.TryParse would also accept numbers such as "2".
    private static bool TryParseName<TEnum>(string value, out TEnum result) where TEnum : struct, Enum
    {
        foreach (var candidate in Enum.GetValues<TEnum>())
        {
            if (string.Equals(candidate.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                result = candidate;
                return true;
            }
        }

        result = default;
        return false;
    }
}
