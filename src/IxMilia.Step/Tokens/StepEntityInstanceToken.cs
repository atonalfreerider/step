namespace IxMilia.Step.Tokens
{
    class StepEntityInstanceToken(int id, int line, int column) : StepToken(line, column)
    {
        public override StepTokenKind Kind => StepTokenKind.EntityInstance;

        public int Id { get; } = id;

        public override string ToString()
        {
            return "#" + Id.ToString();
        }
    }
}
