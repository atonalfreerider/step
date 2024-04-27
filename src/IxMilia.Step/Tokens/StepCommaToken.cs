namespace IxMilia.Step.Tokens
{
    class StepCommaToken(int line, int column) : StepToken(line, column)
    {
        public override StepTokenKind Kind => StepTokenKind.Comma;

        public override string ToString()
        {
            return ",";
        }

        public static StepCommaToken Instance { get; } = new StepCommaToken(-1, -1);
    }
}
