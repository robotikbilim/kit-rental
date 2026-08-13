using KitRental.SharedKernel;

namespace KitRental.Web.Mvc.Services;

public static class TurkeyTimeDisplayExtensions
{
    public static DateTimeOffset AsTurkeyTime(this DateTimeOffset value) => TurkeyTime.Convert(value);

    public static string ToTurkeyTimeString(this DateTimeOffset value, string format) =>
        TurkeyTime.Convert(value).ToString(format);

    public static string ToTurkeyTimeString(this DateTimeOffset? value, string format) =>
        value.HasValue ? TurkeyTime.Convert(value.Value).ToString(format) : string.Empty;
}
