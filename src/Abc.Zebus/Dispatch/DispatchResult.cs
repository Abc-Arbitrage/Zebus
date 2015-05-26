using System;
using System.Collections.Generic;

namespace Abc.Zebus.Dispatch
{
    public struct DispatchResult
    {
        private static readonly Dictionary<Type, Exception> _noErrors = new Dictionary<Type, Exception>();
        private readonly IDictionary<Type, Exception> _errors;

        public DispatchResult(IDictionary<Type, Exception> errorsByHandlerType)
        {
            _errors = errorsByHandlerType ?? _noErrors;
        }
        
        public ICollection<Exception> Errors
        {
            get { return _errors.Values; }
        }

        public ICollection<Type> ErrorHandlerTypes
        {
            get { return _errors.Keys; }
        }
    }
}