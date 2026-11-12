namespace SqlLinq.TimeSeries
{
    public sealed class TimeSeriesColumn
    {
        internal TimeSeriesColumn(string name, TimeSeriesColumnKind kind, TimeSeriesExpression item)
        {
            Name = name;
            Kind = kind;
            Item = item;
        }

        public string Name { get; }

        public TimeSeriesColumnKind Kind { get; }

        public TimeSeriesExpression Item { get; }
    }
}
