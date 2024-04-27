namespace IxMilia.Step.Tokens
{
    class StepConstantInstanceToken(string name, int line, int column) : StepToken(line, column)
    {
        public override StepTokenKind Kind => StepTokenKind.ConstantInstance;

        public string Name { get; } = name;

        public override string ToString()
        {
            return "#" + Name;
        }
    }
}
