namespace IxMilia.Step.Tokens
{
    abstract class StepToken(int line, int column)
    {
        public abstract StepTokenKind Kind { get; }

        public int Line { get; } = line;
        public int Column { get; } = column;

        public virtual string ToString(StepWriter writer)
        {
            return ToString();
        }
    }
}
