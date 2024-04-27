namespace IxMilia.Step.Tokens
{
    class StepStringToken(string value, int line, int column) : StepToken(line, column)
    {
        public override StepTokenKind Kind => StepTokenKind.String;

        public string Value { get; } = value;

        public override string ToString()
        {
            // TODO: escaping
            return "'" + Value + "'";
        }
    }
}
