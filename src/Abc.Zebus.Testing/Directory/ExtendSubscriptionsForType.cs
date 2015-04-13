using System.Collections.Generic;
using System.Linq;
using Abc.Zebus.Directory;

namespace Abc.Zebus.Testing.Directory
{
    public static class ExtendSubscriptionsForType
    {
        public static IEnumerable<SubscriptionsForType> GroupIntoSubscriptionsForTypes(this IEnumerable<Subscription> subscriptions)
        {
            return subscriptions.GroupBy(sub => sub.MessageTypeId).Select(grp => new SubscriptionsForType(grp.Key, grp.Select(sub => sub.BindingKey).ToArray()));
        }
    }
}