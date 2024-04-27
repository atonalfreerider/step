namespace IxMilia.Step.Tokens
{
    class StepLeftParenToken(int line, int column) : StepToken(line, column)
    {
        public override StepTokenKind Kind => StepTokenKind.LeftParen;

        public override string ToString()
        {
            return "(";
        }

        public static StepLeftParenToken Instance { get; } = new StepLeftParenToken(-1, -1);
    }
}
