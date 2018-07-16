namespace Abc.Zebus.Persistence.Matching
{
    public enum MatcherEntryType : byte
    {
        Message,
        Ack,
        EventWaitHandle,
    }
}
