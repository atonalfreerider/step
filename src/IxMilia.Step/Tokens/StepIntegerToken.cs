namespace IxMilia.Step.Tokens
{
    class StepIntegerToken(int value, int line, int column) : StepToken(line, column)
    {
        public override StepTokenKind Kind => StepTokenKind.Integer;

        public int Value { get; } = value;

        public override string ToString()
        {
            return Value.ToString();
        }
    }
}
