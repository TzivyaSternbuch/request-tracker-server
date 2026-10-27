namespace RequestTracker.Core.Dtos;

public record OpenRequestAgeDto(int UnderSevenDays, int SevenToUnderThirtyDays, int ThirtyDaysOrMore);
