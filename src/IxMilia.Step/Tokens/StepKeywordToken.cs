namespace IxMilia.Step.Tokens
{
    class StepKeywordToken(string value, int line, int column) : StepToken(line, column)
    {
        public override StepTokenKind Kind => StepTokenKind.Keyword;

        public string Value { get; } = value;

        public override string ToString()
        {
            return Value;
        }
    }
}
