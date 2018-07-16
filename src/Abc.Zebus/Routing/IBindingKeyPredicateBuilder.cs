using System;

namespace Abc.Zebus.Routing
{
    public interface IBindingKeyPredicateBuilder
    {
        Func<IMessage, bool> GetPredicate(Type messageType, BindingKey bindingKey);
    }
}