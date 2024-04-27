namespace IxMilia.Step.Tokens
{
    class StepOmittedToken(int line, int column) : StepToken(line, column)
    {
        public override StepTokenKind Kind => StepTokenKind.Omitted;

        public override string ToString()
        {
            return "$";
        }

        public static StepOmittedToken Instance { get; } = new StepOmittedToken(-1, -1);
    }
}
