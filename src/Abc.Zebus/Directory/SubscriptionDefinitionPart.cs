namespace Abc.Zebus.Directory
{
    public readonly struct SubscriptionDefinitionPart
    {
        public SubscriptionDefinitionPart(string propertyName, string matchValue, bool matchesAll)
        {
            PropertyName = propertyName;
            Match = new SubscriptionDefinitionMatch(matchValue, matchesAll);
        }

        public string PropertyName { get; }
        public SubscriptionDefinitionMatch Match { get; }
    }

    public readonly struct SubscriptionDefinitionMatch
    {
        public SubscriptionDefinitionMatch(string value, bool forAll)
        {
            Value = forAll ? null : value;
            ForAll = forAll;
        }

        /// <summary> Value is null if <see cref="ForAll"/> is true </summary>
        public string Value { get; }

        /// <summary> Whether this match is valid for all values </summary>
        public bool ForAll { get; }
    }
}
