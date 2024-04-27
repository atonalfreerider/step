namespace IxMilia.Step.Tokens
{
    class StepRightParenToken(int line, int column) : StepToken(line, column)
    {
        public override StepTokenKind Kind => StepTokenKind.RightParen;

        public override string ToString()
        {
            return ")";
        }

        public static StepRightParenToken Instance { get; } = new StepRightParenToken(-1, -1);
    }
}
