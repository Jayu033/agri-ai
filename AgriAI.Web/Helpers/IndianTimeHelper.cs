using System;

namespace AgriAI.Web
{
    /// <summary>
    /// Centralized Indian Standard Time (IST - UTC+05:30) helper for Gujarat/India operations.
    /// Ensures all dates and timestamps displayed across the website strictly follow Indian Standard Time.
    /// </summary>
    public static class IndianTimeHelper
    {
        public static readonly TimeZoneInfo IndianTimeZone = ResolveIndianTimeZone();

        private static TimeZoneInfo ResolveIndianTimeZone()
        {
            // 1. Try Windows TimeZone ID
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            }
            catch { }

            // 2. Try IANA / Linux / Docker TimeZone ID
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
            }
            catch { }

            // 3. Fallback to custom fixed +05:30 offset
            return TimeZoneInfo.CreateCustomTimeZone(
                "IST",
                TimeSpan.FromHours(5.5),
                "(UTC+05:30) Chennai, Kolkata, Mumbai, New Delhi",
                "India Standard Time"
            );
        }

        /// <summary>
        /// Current DateTime in Indian Standard Time (IST).
        /// Replaces DateTime.Now and DateTime.UtcNow for display or local Indian date calculations.
        /// </summary>
        public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IndianTimeZone);

        /// <summary>
        /// Converts a UTC or stored database DateTime to Indian Standard Time (IST).
        /// </summary>
        public static DateTime ToIst(this DateTime utcDate)
        {
            if (utcDate == DateTime.MinValue || utcDate == DateTime.MaxValue)
                return utcDate;

            DateTime utc = utcDate.Kind switch
            {
                DateTimeKind.Utc => utcDate,
                DateTimeKind.Local => utcDate.ToUniversalTime(),
                _ => DateTime.SpecifyKind(utcDate, DateTimeKind.Utc)
            };

            return TimeZoneInfo.ConvertTimeFromUtc(utc, IndianTimeZone);
        }

        /// <summary>
        /// Converts a nullable UTC or stored database DateTime to Indian Standard Time (IST).
        /// </summary>
        public static DateTime? ToIst(this DateTime? utcDate)
        {
            return utcDate.HasValue ? ToIst(utcDate.Value) : null;
        }

        /// <summary>
        /// Formats a DateTime in Indian Standard Time (IST) using the provided format (default: "dd MMM yyyy, hh:mm tt").
        /// </summary>
        public static string ToIstString(this DateTime date, string format = "dd MMM yyyy, hh:mm tt")
        {
            return date.ToIst().ToString(format);
        }

        /// <summary>
        /// Formats a nullable DateTime in Indian Standard Time (IST) or returns defaultText if null.
        /// </summary>
        public static string ToIstString(this DateTime? date, string format = "dd MMM yyyy, hh:mm tt", string defaultText = "")
        {
            return date.HasValue ? date.Value.ToIst().ToString(format) : defaultText;
        }

        /// <summary>
        /// Returns the UTC equivalent of Today 00:00:00 Indian Standard Time.
        /// Use this when querying database OrderDate >= Today in Indian time.
        /// </summary>
        public static DateTime GetIstTodayStartUtc()
        {
            var istNow = Now;
            var istTodayMidnight = new DateTime(istNow.Year, istNow.Month, istNow.Day, 0, 0, 0);
            return TimeZoneInfo.ConvertTimeToUtc(istTodayMidnight, IndianTimeZone);
        }

        /// <summary>
        /// Returns the UTC equivalent of the first day of the current month 00:00:00 Indian Standard Time.
        /// </summary>
        public static DateTime GetIstThisMonthStartUtc()
        {
            var istNow = Now;
            var istMonthMidnight = new DateTime(istNow.Year, istNow.Month, 1, 0, 0, 0);
            return TimeZoneInfo.ConvertTimeToUtc(istMonthMidnight, IndianTimeZone);
        }

        /// <summary>
        /// Returns the UTC equivalent of the first day of the current year 00:00:00 Indian Standard Time.
        /// </summary>
        public static DateTime GetIstThisYearStartUtc()
        {
            var istNow = Now;
            var istYearMidnight = new DateTime(istNow.Year, 1, 1, 0, 0, 0);
            return TimeZoneInfo.ConvertTimeToUtc(istYearMidnight, IndianTimeZone);
        }

        /// <summary>
        /// Converts an IST date to its UTC equivalent for database querying.
        /// </summary>
        public static DateTime ConvertIstToUtc(DateTime istDate)
        {
            var unspecified = DateTime.SpecifyKind(istDate, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(unspecified, IndianTimeZone);
        }
    }
}
