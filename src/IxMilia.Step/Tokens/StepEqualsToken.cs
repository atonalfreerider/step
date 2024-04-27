namespace IxMilia.Step.Tokens
{
    class StepEqualsToken(int line, int column) : StepToken(line, column)
    {
        public override StepTokenKind Kind => StepTokenKind.Equals;

        public override string ToString()
        {
            return "=";
        }

        public static StepEqualsToken Instance { get; } = new StepEqualsToken(-1, -1);
    }
}
