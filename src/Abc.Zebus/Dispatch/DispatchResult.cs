using System;
using System.Collections.Generic;

namespace Abc.Zebus.Dispatch
{
    public struct DispatchResult
    {
        private static readonly Dictionary<Type, Exception> _noErrors = new Dictionary<Type, Exception>();
        private readonly IDictionary<Type, Exception> _errors;

        public readonly bool WasHandled;

        public DispatchResult(bool wasHandled, IDictionary<Type, Exception> errorsByHandlerType)
        {
            WasHandled = wasHandled;
            _errors = errorsByHandlerType ?? _noErrors;
        }
        
        public IEnumerable<Exception> Errors
        {
            get
            {
                return _errors.Values;
            }
        }

        public IEnumerable<Type> ErrorHandlerTypes
        {
            get { return _errors.Keys; }
        }
    }
}