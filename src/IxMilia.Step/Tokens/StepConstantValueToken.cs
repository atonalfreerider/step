namespace IxMilia.Step.Tokens
{
    class StepConstantValueToken(string name, int line, int column) : StepToken(line, column)
    {
        public override StepTokenKind Kind => StepTokenKind.ConstantValue;

        public string Name { get; } = name;

        public override string ToString()
        {
            return "@" + Name;
        }
    }
}
