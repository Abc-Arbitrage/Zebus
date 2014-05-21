using System;

namespace Abc.Zebus
{
    [Flags]
    public enum SubscriptionOptions
    {
        Default,
        ThereIsNoHandlerButIKnowWhatIAmDoing,
    }
}