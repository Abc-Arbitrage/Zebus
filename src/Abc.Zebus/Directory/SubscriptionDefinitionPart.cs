namespace Abc.Zebus.Directory
{
    public readonly struct SubscriptionDefinitionPart
    {
        public SubscriptionDefinitionPart(string propertyName, string matchValue, bool matchesAll)
        {
            PropertyName = propertyName;
            MatchValue = matchValue;
            MatchesAll = matchesAll;
        }

        public string PropertyName { get; }
        public string MatchValue { get; }
        public bool MatchesAll { get; }
    }
}