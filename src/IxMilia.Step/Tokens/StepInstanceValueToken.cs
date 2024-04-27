namespace IxMilia.Step.Tokens
{
    class StepInstanceValueToken(int id, int line, int column) : StepToken(line, column)
    {
        public override StepTokenKind Kind => StepTokenKind.InstanceValue;

        public int Id { get; } = id;

        public override string ToString()
        {
            return "@" + Id.ToString();
        }
    }
}
