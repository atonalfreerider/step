using System;
using System.Collections.Generic;
using System.Linq;
using IxMilia.Step.Tokens;

namespace IxMilia.Step.Syntax
{
    class StepHeaderSectionSyntax(int line, int column, IEnumerable<StepHeaderMacroSyntax> macros)
        : StepSyntax(line, column)
    {
        public override StepSyntaxType SyntaxType => StepSyntaxType.HeaderSection;

        public List<StepHeaderMacroSyntax> Macros { get; } = macros.ToList();

        public override IEnumerable<StepToken> GetTokens()
        {
            throw new NotSupportedException();
        }
    }
}
