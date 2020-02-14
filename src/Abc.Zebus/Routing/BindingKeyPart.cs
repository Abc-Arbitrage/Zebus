namespace Abc.Zebus.Routing
{
    public readonly struct BindingKeyPart
    {
        internal const string StarToken = "*";
        internal const string SharpToken = "#";

        public static readonly BindingKeyPart Star = new BindingKeyPart(null, true);
        public static readonly BindingKeyPart Null;

        private BindingKeyPart(string value, bool matchesAllValues)
        {
            Value = value;
            MatchesAllValues = matchesAllValues;
        }

        public readonly string Value;
        public readonly bool MatchesAllValues;

        public bool Matches(string s) => MatchesAllValues || Value == s;

        public override string ToString()
        {
            return MatchesAllValues ? StarToken : Value;
        }

        public static BindingKeyPart Parse(string token)
        {
            return token == SharpToken || token == StarToken ? Star : new BindingKeyPart(token, false);
        }
    }
}
