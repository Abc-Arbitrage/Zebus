using System;
using System.Collections.Generic;
using Abc.Zebus.Routing;

namespace Abc.Zebus;

/// <summary>
/// Creates the startup subscriptions for a message handler.
/// Must be configured using <see cref="SubscriptionModeAttribute"/>.
/// </summary>
public interface IStartupSubscriber
{
    IEnumerable<BindingKey> GetStartupSubscriptionBindingKeys(Type messageType);
}
