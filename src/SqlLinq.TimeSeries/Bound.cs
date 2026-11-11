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

        internal static Bound Parse(string text, string clause, int start = -1)
        {
            var m = Duration.Match(text);
            if (!m.Success)
            {
                throw new SqlException(string.Format(CultureInfo.CurrentCulture, "{0} {1} isn't a duration, e.g. {0} 30 seconds or {0} 500 ms.", clause, text), start, start < 0 ? 0 : text.Length);
            }

            var value = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            var seconds = value * (m.Groups[2].Success ? 0.001 : m.Groups[3].Success ? 60 : m.Groups[4].Success ? 3600 : 1);
            if (!(seconds < TimeSpan.MaxValue.TotalSeconds))
            {
                throw new SqlException(string.Format(CultureInfo.CurrentCulture, "{0} {1} is too long a duration.", clause, text), start, start < 0 ? 0 : text.Length);
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

            return new Bound { Offset = offset, Ago = m.Groups[5].Success };
        }
    }
}
