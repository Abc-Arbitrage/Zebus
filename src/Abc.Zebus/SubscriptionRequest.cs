using System.Collections.Generic;
using Abc.Zebus.Util.Extensions;

namespace Abc.Zebus
{
    public class SubscriptionRequest
    {
        public ICollection<Subscription> Subscriptions { get; } = new HashSet<Subscription>();

        public bool ThereIsNoHandlerButIKnowWhatIAmDoing { get; set; }

        public SubscriptionRequest(Subscription subscription)
        {
            Subscriptions.Add(subscription);
        }

        public SubscriptionRequest(IEnumerable<Subscription> subscriptions)
        {
            Subscriptions.AddRange(subscriptions);
        }
    }
}
