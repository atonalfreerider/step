using System.Collections.Generic;
using IxMilia.Step.Syntax;
using IxMilia.Step.Tokens;

namespace IxMilia.Step
{
    class StepLexer(IEnumerable<StepToken> tokens)
    {
        readonly List<StepToken> _tokens = [..tokens];
        int _offset = 0;

        bool TokensRemain()
        {
            return _offset < _tokens.Count;
        }

        void MoveNext()
        {
            _offset++;
        }

        StepToken Current => _tokens[_offset];

        public StepFileSyntax LexFileSyntax()
        {
            _offset = 0;
            SwallowKeywordAndSemicolon(StepFile.MagicHeader);

            StepHeaderSectionSyntax header = LexHeaderSection();
            StepDataSectionSyntax data = LexDataSection();

            StepFileSyntax file = new StepFileSyntax(header, data);

            SwallowKeywordAndSemicolon(StepFile.MagicFooter);

            return file;
        }

        StepHeaderSectionSyntax LexHeaderSection()
        {
            AssertTokensRemain();
            int headerLine = Current.Line;
            int headerColumn = Current.Column;
            SwallowKeywordAndSemicolon(StepFile.HeaderText);
            List<StepHeaderMacroSyntax> macros = [];
            while (TokensRemain() && Current.Kind == StepTokenKind.Keyword && !IsCurrentEndSec())
            {
                StepHeaderMacroSyntax macro = LexHeaderMacro();
                macros.Add(macro);
            }

            SwallowKeywordAndSemicolon(StepFile.EndSectionText);
            return new StepHeaderSectionSyntax(headerLine, headerColumn, macros);
        }

        StepHeaderMacroSyntax LexHeaderMacro()
        {
            AssertNextTokenKind(StepTokenKind.Keyword);
            string name = ((StepKeywordToken)Current).Value;
            MoveNext();
            StepSyntaxList syntaxList = LexSyntaxList();
            SwallowSemicolon();
            return new StepHeaderMacroSyntax(name, syntaxList);
        }

        StepSyntax LexIndividualValue()
        {
            StepSyntax result;
            AssertTokensRemain();
            switch (Current.Kind)
            {
                case StepTokenKind.Integer:
                    result = new StepIntegerSyntax((StepIntegerToken)Current);
                    MoveNext();
                    break;
                case StepTokenKind.Real:
                    result = new StepRealSyntax((StepRealToken)Current);
                    MoveNext();
                    break;
                case StepTokenKind.String:
                    result = new StepStringSyntax((StepStringToken)Current);
                    MoveNext();
                    break;
                case StepTokenKind.Asterisk:
                    result = new StepAutoSyntax((StepAsteriskToken)Current);
                    MoveNext();
                    break;
                case StepTokenKind.Omitted:
                    result = new StepOmittedSyntax((StepOmittedToken)Current);
                    MoveNext();
                    break;
                case StepTokenKind.Enumeration:
                    result = new StepEnumerationValueSyntax((StepEnumerationToken)Current);
                    MoveNext();
                    break;
                case StepTokenKind.LeftParen:
                    result = LexSyntaxList();
                    break;
                case StepTokenKind.Keyword:
                    result = LexSimpleItem();
                    break;
                case StepTokenKind.EntityInstance:
                    result = new StepEntityInstanceReferenceSyntax((StepEntityInstanceToken)Current);
                    MoveNext();
                    break;
                default:
                    ReportError($"Unexpected syntax token '{Current.Kind}'");
                    result = null; // unreachable
                    break;
            }

            return result;
        }

        StepSyntaxList LexSyntaxList()
        {
            AssertTokensRemain();
            int listLine = Current.Line;
            int listColumn = Current.Column;
            SwallowLeftParen();
            List<StepSyntax> values = [];
            bool keepReading = true;
            bool expectingValue = true;
            while (keepReading)
            {
                AssertTokensRemain();
                if (expectingValue || values.Count == 0)
                {
                    // expect a value or a close paren
                    switch (Current.Kind)
                    {
                        case StepTokenKind.RightParen:
                            keepReading = false;
                            MoveNext();
                            break;
                        default:
                            values.Add(LexIndividualValue());
                            break;
                    }
                }
                else
                {
                    // expect a comma or close paren
                    switch (Current.Kind)
                    {
                        case StepTokenKind.RightParen:
                            keepReading = false;
                            MoveNext();
                            break;
                        case StepTokenKind.Comma:
                            MoveNext();
                            break;
                        default:
                            ReportError($"Expected right paren or comma but found '{Current.Kind}'");
                            break;
                    }
                }

                expectingValue = !expectingValue;
            }

            return new StepSyntaxList(listLine, listColumn, values);
        }

        StepDataSectionSyntax LexDataSection()
        {
            AssertTokensRemain();
            int dataLine = Current.Line;
            int dataColumn = Current.Column;
            SwallowKeywordAndSemicolon(StepFile.DataText);
            List<StepEntityInstanceSyntax> itemInstsances = [];
            while (TokensRemain() && Current.Kind == StepTokenKind.EntityInstance)
            {
                StepEntityInstanceSyntax itemInstance = LexItemInstance();
                itemInstsances.Add(itemInstance);
            }

            SwallowKeywordAndSemicolon(StepFile.EndSectionText);
            return new StepDataSectionSyntax(dataLine, dataColumn, itemInstsances);
        }

        StepEntityInstanceSyntax LexItemInstance()
        {
            int line = Current.Line;
            int column = Current.Column;

            AssertNextTokenKind(StepTokenKind.EntityInstance);
            StepEntityInstanceToken reference = (StepEntityInstanceToken)Current;
            MoveNext();

            SwallowEquals();

            AssertTokensRemain();
            StepItemSyntax item = null;
            switch (Current.Kind)
            {
                case StepTokenKind.Keyword:
                    item = LexSimpleItem();
                    break;
                case StepTokenKind.LeftParen:
                    item = LexComplexItem();
                    break;
                default:
                    ReportError($"Expected left paren but found {Current.Kind}");
                    break; // unreachable
            }

            SwallowSemicolon();

            return new StepEntityInstanceSyntax(reference, item);
        }

        StepSimpleItemSyntax LexSimpleItem()
        {
            AssertNextTokenKind(StepTokenKind.Keyword);
            StepKeywordToken keyword = (StepKeywordToken)Current;
            MoveNext();

            StepSyntaxList parameters = LexSyntaxList();
            return new StepSimpleItemSyntax(keyword, parameters);
        }

        StepComplexItemSyntax LexComplexItem()
        {
            List<StepSimpleItemSyntax> entities = [];
            int itemLine = Current.Line;
            int itemColumn = Current.Column;
            SwallowLeftParen();
            entities.Add(LexSimpleItem()); // there's always at least one

            bool keepReading = true;
            while (keepReading)
            {
                AssertTokensRemain();
                switch (Current.Kind)
                {
                    case StepTokenKind.RightParen:
                        SwallowRightParen();
                        keepReading = false;
                        break;
                    case StepTokenKind.Keyword:
                        entities.Add(LexSimpleItem());
                        break;
                    default:
                        ReportError($"Expected right paren or keyword but found {Current.Kind}");
                        break; // unreachable
                }
            }

            return new StepComplexItemSyntax(itemLine, itemColumn, entities);
        }

        bool IsCurrentEndSec()
        {
            return Current.Kind == StepTokenKind.Keyword && ((StepKeywordToken)Current).Value == StepFile.EndSectionText;
        }

        void SwallowKeyword(string keyword)
        {
            AssertNextTokenKind(StepTokenKind.Keyword);
            if (((StepKeywordToken)Current).Value != keyword)
            {
                ReportError($"Expected keyword '{keyword}' but found '{((StepKeywordToken)Current).Value}'");
            }

            MoveNext();
        }

        void SwallowKeywordAndSemicolon(string keyword)
        {
            SwallowKeyword(keyword);
            SwallowSemicolon();
        }

        void SwallowSemicolon()
        {
            SwallowToken(StepTokenKind.Semicolon);
        }

        void SwallowLeftParen()
        {
            SwallowToken(StepTokenKind.LeftParen);
        }

        void SwallowRightParen()
        {
            SwallowToken(StepTokenKind.RightParen);
        }

        void SwallowEquals()
        {
            SwallowToken(StepTokenKind.Equals);
        }

        void SwallowToken(StepTokenKind kind)
        {
            AssertNextTokenKind(kind);
            MoveNext();
        }

        void AssertNextTokenKind(StepTokenKind kind)
        {
            AssertTokensRemain();
            if (Current.Kind != kind)
            {
                ReportError($"Expected '{kind}' token but found '{Current.Kind}'");
            }
        }

        void AssertTokensRemain()
        {
            if (!TokensRemain())
            {
                ReportError("Unexpected end of token stream", 0, 0);
            }
        }

        void ReportError(string message, int? line = null, int? column = null)
        {
            throw new StepReadException(message, line ?? Current.Line, column ?? Current.Column);
        }
    }
}
