using System;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace SqlLinq
{
    [Serializable()]
    public class SqlException : Exception
    {
        public SqlException()
        {
        }

        public SqlException(string message)
            : base(message)
        {
        }

        public SqlException(string message, Exception inner)
            : base(message, inner)
        {
        }

        public SqlException(string message, int start, int length) : base(message)
        {
            Start = start;
            Length = length;
        }

        public SqlException(string message, Exception inner, int start, int length) : base(message, inner)
        {
            Start = start;
            Length = length;
        }

        protected SqlException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        public int Start { get; } = -1;

        public int Length { get; }

        internal static void ThrowIfNestedTooDeeply()
        {
            if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
            {
                throw new SqlException("The query is nested too deeply.");
            }
        }
    }
}
