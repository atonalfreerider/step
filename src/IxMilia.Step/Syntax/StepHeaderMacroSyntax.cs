using System;
using System.Collections.Generic;
using IxMilia.Step.Tokens;

namespace IxMilia.Step.Syntax
{
    class StepHeaderMacroSyntax(string name, StepSyntaxList values) : StepSyntax(values.Line, values.Column)
    {
        public override StepSyntaxType SyntaxType => StepSyntaxType.HeaderMacro;

        public string Name { get; } = name;
        public StepSyntaxList Values { get; } = values;

        public override IEnumerable<StepToken> GetTokens()
        {
            throw new NotSupportedException();
        }
    }
}
