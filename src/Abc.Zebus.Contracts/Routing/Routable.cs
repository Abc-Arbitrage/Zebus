using System;

namespace Abc.Zebus.Routing;

/// <summary>
/// Indicates that the target message is routable.
/// </summary>
/// <remarks>
/// <para>
/// A routable message must have routable members. The routable members must be explicitly specified using <see cref="RoutingPositionAttribute"/>.
/// </para>
/// <para>
/// The default subscription mode of routable message is <c>Manual</c>, i.e.: subscriptions must be manually created with <c>IBus.Subscribe</c>.
/// If you update an existing message to make it routable, consider specifying <c>AutoSubscribe = true</c> to keep previous subscription mode.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class)]
public sealed class Routable : Attribute
{
    /// <summary>
    /// Indicates whether the default subscription mode for the target message is Auto (<c>AutoSubscribe == true</c>) or Manual.
    /// </summary>
    /// <remarks>
    /// <see cref="AutoSubscribe"/> configures the default subscription mode for the target message. The subscription mode
    /// can still be overriden per message handler using the <c>SubscriptionMode</c> attribute.
    /// </remarks>
    public bool AutoSubscribe { get; set; }
}
