namespace IxMilia.Step.Tokens
{
    class StepEnumerationToken(string value, int line, int column) : StepToken(line, column)
    {
        public override StepTokenKind Kind => StepTokenKind.Enumeration;

        public string Value { get; } = value;

        public override string ToString()
        {
            return "." + Value + ".";
        }
    }
}
