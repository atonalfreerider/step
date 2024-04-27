namespace IxMilia.Step.Tokens
{
    class StepSemicolonToken(int line, int column) : StepToken(line, column)
    {
        public override StepTokenKind Kind => StepTokenKind.Semicolon;

        public override string ToString()
        {
            return ";";
        }

        public static StepSemicolonToken Instance { get; } = new StepSemicolonToken(-1, -1);
    }
}
