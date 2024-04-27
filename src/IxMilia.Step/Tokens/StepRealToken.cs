namespace IxMilia.Step.Tokens
{
    class StepRealToken(double value, int line, int column) : StepToken(line, column)
    {
        public override StepTokenKind Kind => StepTokenKind.Real;

        public double Value { get; } = value;

        public override string ToString()
        {
            return Value.ToString("0.0#");
        }
    }
}
