using System;
using System.Runtime.Serialization;

namespace Abc.Zebus
{
    [Serializable]
    public class MessageProcessingException : Exception
    {
        private int _errorCode = ErrorStatus.UnknownError.Code;

        public int ErrorCode
        {
            get => _errorCode;
            init => _errorCode = value != ErrorStatus.NoError.Code
                ? value
                : ErrorStatus.UnknownError.Code;
        }

        public bool ShouldPublishError { get; init; }

        public MessageProcessingException()
        {
        }

        public MessageProcessingException(string message)
            : base(message)
        {
        }

        public MessageProcessingException(string message, Exception? inner)
            : base(message, inner)
        {
        }

        protected MessageProcessingException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
