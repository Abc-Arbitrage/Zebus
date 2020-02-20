namespace Abc.Zebus.Routing
{
    public readonly struct BindingKeyPart
    {
        internal const string StarToken = "*";
        internal const string SharpToken = "#";

        public static BindingKeyPart Star { get; } = new BindingKeyPart(null, true);
        public static BindingKeyPart Null { get; } = default;

        private BindingKeyPart(string? value, bool matchesAllValues)
        {
            Value = value;
            MatchesAllValues = matchesAllValues;
        }

        public string? Value { get; }
        public bool MatchesAllValues { get; }

        public bool Matches(string s)
            => MatchesAllValues || Value == s;

        public override string ToString()
        {
            return MatchesAllValues
                ? StarToken
                : Value ?? "null";
        }

        public static BindingKeyPart Parse(string token)
        {
            return token == SharpToken || token == StarToken
                ? Star
                : new BindingKeyPart(token, false);
        }
    }
}
