namespace ResourceManager.CommitDataRenders;

public interface IDateFormatter
{
    /// <summary>
    ///     Formats the supplied date as relative local date (e.g. 3 months ago (10/9/17 4:38:40 pm)).
    /// </summary>
    /// <param name="date">Date to format.</param>
    /// <returns>Date in relative local date format.</returns>
    string FormatDateAsRelativeLocal(DateTimeOffset date);
}

public sealed class DateFormatter(Func<DateTime> getUtcNow) : IDateFormatter
{
    public DateFormatter()
        : this(() => DateTime.UtcNow)
    {
    }

    /// <summary>
    ///     Formats the supplied date as relative local date (e.g. 3 months ago (10/9/17 4:38:40 pm)).
    /// </summary>
    /// <param name="date">Date to format.</param>
    /// <returns>Date in relative local date format.</returns>
    string IDateFormatter.FormatDateAsRelativeLocal(DateTimeOffset date)
    {
        return string.Format("{0} ({1})", LocalizationHelpers.GetRelativeDateString(getUtcNow(), date.UtcDateTime),
            LocalizationHelpers.GetFullDateString(date));
    }
}
