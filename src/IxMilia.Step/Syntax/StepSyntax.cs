using System.Collections.Generic;
using IxMilia.Step.Tokens;

namespace IxMilia.Step.Syntax
{
    abstract class StepSyntax(int line, int column)
    {
        public abstract StepSyntaxType SyntaxType { get; }

        public int Line { get; } = line;
        public int Column { get; } = column;

        public abstract IEnumerable<StepToken> GetTokens();
    }
}
