using System.Collections.Generic;
using IxMilia.Step.Tokens;

namespace IxMilia.Step.Syntax
{
    class StepAutoSyntax(StepAsteriskToken token) : StepSyntax(token.Line, token.Column)
    {
        public override StepSyntaxType SyntaxType => StepSyntaxType.Auto;

        public StepAsteriskToken Token { get; private set; } = token;

        public StepAutoSyntax()
            : this(StepAsteriskToken.Instance)
        {
        }

        public override IEnumerable<StepToken> GetTokens()
        {
            yield return Token;
        }
    }
}
