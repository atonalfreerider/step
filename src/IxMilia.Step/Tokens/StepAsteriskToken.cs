namespace IxMilia.Step.Tokens
{
    class StepAsteriskToken(int line, int column) : StepToken(line, column)
    {
        public override StepTokenKind Kind => StepTokenKind.Asterisk;

        public override string ToString()
        {
            return "*";
        }

        public static StepAsteriskToken Instance { get; } = new StepAsteriskToken(-1, -1);
    }
}
