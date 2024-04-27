using System;
using System.Collections.Generic;
using IxMilia.Step.Tokens;

namespace IxMilia.Step.Syntax
{
    class StepFileSyntax(StepHeaderSectionSyntax header, StepDataSectionSyntax data)
        : StepSyntax(header.Line, header.Column)
    {
        public override StepSyntaxType SyntaxType => StepSyntaxType.File;

        public StepHeaderSectionSyntax Header { get; } = header;
        public StepDataSectionSyntax Data { get; } = data;

        public override IEnumerable<StepToken> GetTokens()
        {
            throw new NotSupportedException();
        }
    }
}
