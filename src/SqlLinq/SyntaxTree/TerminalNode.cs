using System.Diagnostics;
using System.Xml.Linq;

using GoldParser;

namespace SqlLinq.SyntaxTree
{
    public class TerminalNode : SyntaxNode
    {
        private int _start;

        private int _length;

        internal TerminalNode(Parser parser)
        {
            Debug.Assert(parser != null);

            Symbol = parser.TokenSymbol;
            Text = parser.TokenText;
            LineNumber = parser.LineNumber;
            LinePosition = parser.LinePosition;
            _start = parser.TokenCharPosition;
            _length = parser.TokenLength;
        }

        private TerminalNode(TerminalNode original)
        {
            Symbol = original.Symbol;
            Text = original.Text;
            LineNumber = original.LineNumber;
            LinePosition = original.LinePosition;
            _start = original._start;
            _length = original._length;
        }

        internal Symbol Symbol { get; private set; }

        public string Text { get; private set; }

        public int LineNumber { get; private set; }

        public int LinePosition { get; private set; }

        public override int Start => _start;

        public override int Length => _length;

        public override string ToSql()
        {
            return Text;
        }

        public override string ToString()
        {
            return Text;
        }

        public override XElement ToXml()
        {
            return new XElement("terminal", Text);
        }

        internal TerminalNode Copy()
        {
            return new TerminalNode(this);
        }

        internal void Place(int start, int length)
        {
            _start = start;
            _length = length;
        }

        internal void Locate(int line, int position)
        {
            LineNumber = line;
            LinePosition = position;
        }
    }
}
