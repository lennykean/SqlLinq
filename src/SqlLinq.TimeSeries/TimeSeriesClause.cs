namespace SqlLinq.TimeSeries
{
    public sealed class TimeSeriesClause
    {
        internal TimeSeriesClause(string keyword, string text, int start, int keywordStart, int length)
        {
            Keyword = keyword;
            Text = text;
            Start = start;
            KeywordStart = keywordStart;
            Length = length;
        }

        public string Keyword { get; }

        public string Text { get; }

        public int Start { get; }

        public int KeywordStart { get; }

        public int Length { get; }
    }
}
