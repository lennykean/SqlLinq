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

        protected SqlException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        internal static void ThrowIfNestedTooDeeply()
        {
            if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
            {
                throw new SqlException("The query is nested too deeply.");
            }
        }
    }
}
