using System;
using System.Collections.Generic;

namespace Abc.Zebus.Dispatch
{
    public readonly struct DispatchResult
    {
        private static readonly Dictionary<Type, Exception> _noErrors = new Dictionary<Type, Exception>();
        private readonly IDictionary<Type, Exception> _errors;

        public DispatchResult(IDictionary<Type, Exception> errorsByHandlerType)
        {
            _errors = errorsByHandlerType ?? _noErrors;
        }
        
        public ICollection<Exception> Errors => _errors.Values;

        public ICollection<Type> ErrorHandlerTypes => _errors.Keys;
    }
}
