using System;

namespace IxMilia.Step
{
    public class StepReadException(string message, int line, int column) : Exception($"{message} at [{line}:{column}]")
    {
        public int Line { get; } = line;
        public int Column { get; } = column;
    }
}
