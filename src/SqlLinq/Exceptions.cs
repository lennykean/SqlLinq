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

        public SqlException(string message, SqlErrorKind kind) : base(message)
        {
            Kind = kind;
        }

        public SqlException(string message, Exception inner, SqlErrorKind kind) : base(message, inner)
        {
            Kind = kind;
        }

        public SqlException(string message, SqlErrorKind kind, int start, int length) : this(message, start, length)
        {
            Kind = kind;
        }

        public SqlException(string message, Exception inner, SqlErrorKind kind, int start, int length) : this(message, inner, start, length)
        {
            Kind = kind;
        }

        protected SqlException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        public int Start { get; } = -1;

        public int Length { get; }

        public SqlErrorKind Kind { get; }

        internal static void ThrowIfNestedTooDeeply()
        {
            if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
            {
                throw new SqlException("The query can't be translated because it's nested too deeply.", SqlErrorKind.Semantic);
            }
        }
    }
}
