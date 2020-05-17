using System;
using System.Runtime.Serialization;

namespace NUnitDotNetCoreRunner.Models
{
    [Serializable]
    internal class IterationsExceededException : Exception
    {
        public IterationsExceededException()
        {
        }

        public IterationsExceededException(string message) : base(message)
        {
        }

        public IterationsExceededException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected IterationsExceededException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}