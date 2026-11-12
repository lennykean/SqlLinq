using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SqlLinq.TimeSeries
{
    public sealed class Bound
    {
        private static readonly Regex Duration = new Regex(@"^(\d+(?:\.\d+)?)\s*(?:(ms|milliseconds?)|(m|mins?|minutes?)|(h|hours?)|s|secs?|seconds?)?\s*(ago)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public TimeSpan Offset { get; private set; }

        public bool Ago { get; private set; }

        public TimeSpan Resolve(TimeSpan now)
        {
            return Ago ? now - Offset : Offset;
        }

        public static bool TryParse(string text, string clause, out Bound bound, out SqlException error)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (clause == null)
            {
                throw new ArgumentNullException(nameof(clause));
            }

            try
            {
                bound = Parse(text.Trim(), clause, text.Length - text.TrimStart().Length);
                error = null;
            }
            catch (SqlException e)
            {
                bound = null;
                error = e;
            }

            return error == null;
        }

        internal static Bound Parse(string text, string clause, int start = -1)
        {
            var m = Duration.Match(text);
            if (!m.Success)
            {
                throw new SqlException(string.Format(CultureInfo.CurrentCulture, "{0} {1} isn't a duration, e.g. {0} 30 seconds or {0} 500 ms.", clause, text), SqlErrorKind.Syntax, start, start < 0 ? 0 : text.Length);
            }

            var value = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            var seconds = value * (m.Groups[2].Success ? 0.001 : m.Groups[3].Success ? 60 : m.Groups[4].Success ? 3600 : 1);
            if (!(seconds < TimeSpan.MaxValue.TotalSeconds))
            {
                throw new SqlException(string.Format(CultureInfo.CurrentCulture, "{0} {1} is too long a duration.", clause, text), SqlErrorKind.Semantic, start, start < 0 ? 0 : text.Length);
            }

            TimeSpan offset;

            if (m.Groups[2].Success)
            {
                offset = TimeSpan.FromMilliseconds(value);
            }
            else if (m.Groups[3].Success)
            {
                offset = TimeSpan.FromMinutes(value);
            }
            else if (m.Groups[4].Success)
            {
                offset = TimeSpan.FromHours(value);
            }
            else
            {
                offset = TimeSpan.FromSeconds(value);
            }

            var bound = new Bound { Offset = offset, Ago = m.Groups[5].Success };
            if ((bound.Ago || offset <= TimeSpan.Zero) && clause.Equals("TIMESERIES", StringComparison.OrdinalIgnoreCase))
            {
                throw new SqlException("TIMESERIES needs a positive duration, e.g. TIMESERIES 1 second.", SqlErrorKind.Semantic, start, start < 0 ? 0 : text.Length);
            }

            if ((bound.Ago || offset <= TimeSpan.Zero) && clause.Equals("SESSION", StringComparison.OrdinalIgnoreCase))
            {
                throw new SqlException("SESSION needs a positive gap, e.g. SESSION 1 second.", SqlErrorKind.Semantic, start, start < 0 ? 0 : text.Length);
            }

            return bound;
        }
    }
}
