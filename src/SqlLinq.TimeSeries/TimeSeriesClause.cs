namespace SqlLinq.TimeSeries
{
    public sealed class TimeSeriesClause
    {
        internal TimeSeriesClause(string keyword, string text, int start)
        {
            Keyword = keyword;
            Text = text;
            Start = start;
        }

        public string Keyword { get; }

        public string Text { get; }

        public int Start { get; }
    }
}
