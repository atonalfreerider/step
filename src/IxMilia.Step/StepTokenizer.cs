using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using IxMilia.Step.Tokens;

namespace IxMilia.Step
{
    class StepTokenizer
    {
        readonly StreamReader _reader;
        string _currentLine;
        int _offset;
        int _currentLineNumber;
        int _currentColumn;

        public int CurrentLine => _currentLineNumber;
        public int CurrentColumn => _currentColumn;

        public StepTokenizer(Stream stream)
        {
            _reader = new StreamReader(stream);
            ReadNextLine();
        }

        void ReadNextLine()
        {
            _currentLine = _reader.ReadLine();
            _offset = 0;
            _currentLineNumber++;
            _currentColumn = 1;
        }

        char? PeekCharacter()
        {
            while (true)
            {
                if (_currentLine == null)
                {
                    return null;
                }

                while (_offset >= _currentLine.Length)
                {
                    ReadNextLine();
                    if (_currentLine == null)
                    {
                        return null;
                    }
                }

                switch (_currentLine[_offset])
                {
                    case '/':
                        if (_offset <= _currentLine.Length - 1 && _currentLine[_offset + 1] == '*')
                        {
                            // entered multiline comment
                            Advance(); // swallow '/'
                            Advance(); // swallow '*'

                            int endIndex = _currentLine.IndexOf("*/", _offset);
                            while (endIndex < 0 && _currentLine != null)
                            {
                                // end wasn't on this line
                                ReadNextLine();
                                if (_currentLine == null)
                                {
                                    break;
                                }

                                endIndex = _currentLine.IndexOf("*/", _offset);
                            }

                            if (_currentLine == null)
                            {
                                // read past the end of the file
                                return null;
                            }
                            else
                            {
                                // end was on this line
                                _offset = endIndex + 2;
                            }
                        }
                        else
                        {
                            goto default;
                        }
                        break;
                    default:
                        return _currentLine[_offset];
                }
            }
        }

        void Advance()
        {
            _offset++;
            _currentColumn++;
            if (_offset > _currentLine.Length)
            {
                ReadNextLine();
            }
        }

        public IEnumerable<StepToken> GetTokens()
        {
            char? cn;
            SwallowWhitespace();
            while ((cn = PeekCharacter()) != null)
            {
                int tokenLine = _currentLineNumber;
                int tokenColumn = _currentColumn;
                char c = cn.GetValueOrDefault();
                if (c == '$')
                {
                    Advance();
                    yield return new StepOmittedToken(tokenLine, tokenColumn);
                }
                else if (c == ';')
                {
                    Advance();
                    yield return new StepSemicolonToken(tokenLine, tokenColumn);
                }
                else if (c == '=')
                {
                    Advance();
                    yield return new StepEqualsToken(tokenLine, tokenColumn);
                }
                else if (c == '*')
                {
                    Advance();
                    yield return new StepAsteriskToken(tokenLine, tokenColumn);
                }
                else if (IsNumberStart(c))
                {
                    yield return ParseNumber();
                }
                else if (IsApostrophe(c))
                {
                    yield return ParseString();
                }
                else if (IsHash(c))
                {
                    // constant instance: #INCH
                    // entity instance: #1234
                    yield return ParseHashValue();
                }
                else if (IsAt(c))
                {
                    // constant value: @PI
                    // instance value: @12
                    yield return ParseAtValue();
                }
                else if (IsDot(c))
                {
                    yield return ParseEnumeration();
                }
                else if (IsLeftParen(c))
                {
                    Advance();
                    yield return new StepLeftParenToken(tokenLine, tokenColumn);
                }
                else if (IsRightParen(c))
                {
                    Advance();
                    yield return new StepRightParenToken(tokenLine, tokenColumn);
                }
                else if (IsComma(c))
                {
                    Advance();
                    yield return new StepCommaToken(tokenLine, tokenColumn);
                }
                else if (IsUpper(c))
                {
                    yield return ParseKeyword();
                }
                else
                {
                    throw new StepReadException($"Unexpected character '{c}'", _currentLineNumber, _currentColumn);
                }

                SwallowWhitespace();
            }

            yield break;
        }

        void SwallowWhitespace()
        {
            char? cn;
            bool keepSwallowing = true;
            while (keepSwallowing && (cn = PeekCharacter()) != null)
            {
                switch (cn.GetValueOrDefault())
                {
                    case ' ':
                    case '\r':
                    case '\n':
                    case '\t':
                    case '\f':
                    case '\v':
                        Advance();
                        break;
                    default:
                        keepSwallowing = false;
                        break;
                }
            }
        }

        bool IsDigit(char c)
        {
            return c >= '0' && c <= '9';
        }

        bool IsDot(char c)
        {
            return c == '.';
        }

        bool IsE(char c)
        {
            return c == 'e' || c == 'E';
        }

        bool IsPlus(char c)
        {
            return c == '+';
        }

        bool IsMinus(char c)
        {
            return c == '-';
        }

        bool IsUnderscore(char c)
        {
            return c == '_';
        }

        bool IsNumberStart(char c)
        {
            return IsDigit(c)
                || c == '-'
                || c == '+';
        }

        bool IsApostrophe(char c)
        {
            return c == '\'';
        }

        bool IsBackslash(char c)
        {
            return c == '\\';
        }

        bool IsHash(char c)
        {
            return c == '#';
        }

        bool IsAt(char c)
        {
            return c == '@';
        }

        bool IsUpper(char c)
        {
            return c >= 'A' && c <= 'Z';
        }

        bool IsUpperOrDigit(char c)
        {
            return IsUpper(c) || IsDigit(c);
        }

        bool IsEnumCharacter(char c)
        {
            return IsUpperOrDigit(c) || IsUnderscore(c);
        }

        bool IsLeftParen(char c)
        {
            return c == '(';
        }

        bool IsRightParen(char c)
        {
            return c == ')';
        }

        bool IsComma(char c)
        {
            return c == ',';
        }

        bool IsKeywordCharacter(char c)
        {
            return IsUpperOrDigit(c)
                || IsUnderscore(c)
                || IsMinus(c);
        }

        StepToken ParseNumber()
        {
            int tokenLine = _currentLineNumber;
            int tokenColumn = _currentColumn;
            StringBuilder sb = new StringBuilder();
            sb.Append(PeekCharacter());
            Advance();

            bool seenDecimal = false;
            bool seenE = false;
            char? cn;
            while ((cn = PeekCharacter()) != null)
            {
                char c = cn.GetValueOrDefault();
                if (IsDigit(c))
                {
                    sb.Append(c);
                    Advance();
                }
                else if (IsDot(c) && !seenDecimal && !seenE)
                {
                    sb.Append(c);
                    seenDecimal = true;
                    Advance();
                }
                else if (IsE(c) && !seenE)
                {
                    sb.Append(c);
                    seenE = true;
                    Advance();
                }
                else if ((IsPlus(c) || IsMinus(c)) && seenE)
                {
                    // TODO: this will fail on "1.0E+-+-+-+-1"
                    sb.Append(c);
                    Advance();
                }
                else
                {
                    break;
                }
            }

            string str = sb.ToString();
            return seenDecimal || seenE
                ? (StepToken)new StepRealToken(double.Parse(str, CultureInfo.InvariantCulture), tokenLine, tokenColumn)
                : new StepIntegerToken(int.Parse(str, CultureInfo.InvariantCulture), tokenLine, tokenColumn);
        }

        StepStringToken ParseString()
        {
            int tokenLine = _currentLineNumber;
            int tokenColumn = _currentColumn;
            StringBuilder sb = new StringBuilder();
            Advance();

            char? cn;
            bool wasApostropheLast = false;
            bool wasBackslashLast = false;
            while ((cn = PeekCharacter()) != null)
            {
                char c = cn.GetValueOrDefault();
                if (IsApostrophe(c) && wasApostropheLast)
                {
                    // escaped
                    sb.Append(c);
                    Advance();
                }
                else if (IsApostrophe(c) && !wasApostropheLast)
                {
                    // maybe the end
                    wasApostropheLast = true;
                    Advance();
                }
                else if (!IsApostrophe(c) && wasApostropheLast)
                {
                    // end of string
                    break;
                }
                else if (IsBackslash(c) && !wasBackslashLast)
                {
                    // start escaping
                    wasBackslashLast = true;
                    Advance();
                }
                else if (wasBackslashLast)
                {
                    // TODO: handle real escaping
                    sb.Append(c);
                    Advance();
                }
                else
                {
                    // just a normal string
                    sb.Append(c);
                    Advance();
                }
            }

            string str = sb.ToString();
            return new StepStringToken(str, tokenLine, tokenColumn);
        }

        StepToken ParseHashValue()
        {
            int tokenLine = _currentLineNumber;
            int tokenColumn = _currentColumn;
            Advance(); // swallow '#'
            char? next = PeekCharacter();
            if (next == null)
            {
                throw new StepReadException("Expected constant instance or entity instance", tokenLine, tokenColumn);
            }

            if (IsDigit(next.GetValueOrDefault()))
            {
                // entity instance: #1234
                return new StepEntityInstanceToken(int.Parse(TakeWhile(IsDigit), CultureInfo.InvariantCulture), tokenLine, tokenColumn);
            }
            else if (IsUpper(next.GetValueOrDefault()))
            {
                // constant instance: #INCH
                return new StepConstantInstanceToken(TakeWhile(IsUpperOrDigit), tokenLine, tokenColumn);
            }
            else
            {
                throw new StepReadException("Expected constant instance or entity instance", tokenLine, tokenColumn);
            }
        }

        StepToken ParseAtValue()
        {
            int tokenLine = _currentLineNumber;
            int tokenColumn = _currentColumn;
            Advance(); // swallow '@'
            char? next = PeekCharacter();
            if (next == null)
            {
                throw new StepReadException("Expected constant value or instance value", tokenLine, tokenColumn);
            }

            if (IsDigit(next.GetValueOrDefault()))
            {
                // constant value: @PI
                return new StepConstantValueToken(TakeWhile(IsDigit), tokenLine, tokenColumn);
            }
            else if (IsUpper(next.GetValueOrDefault()))
            {
                // instance value: @12
                return new StepInstanceValueToken(int.Parse(TakeWhile(IsUpperOrDigit), CultureInfo.InvariantCulture), tokenLine, tokenColumn);
            }
            else
            {
                throw new StepReadException("Expected constant value or instance value", tokenLine, tokenColumn);
            }
        }

        StepEnumerationToken ParseEnumeration()
        {
            int tokenLine = _currentLineNumber;
            int tokenColumn = _currentColumn;
            StringBuilder sb = new StringBuilder();
            Advance(); // swallow leading '.'
            string value = TakeWhile(IsEnumCharacter);
            if (string.IsNullOrEmpty(value))
            {
                throw new StepReadException("Expected enumeration value", tokenLine, tokenColumn);
            }

            char? next = PeekCharacter();
            if (next.HasValue && IsDot(next.GetValueOrDefault()))
            {
                Advance();
                return new StepEnumerationToken(value, tokenLine, tokenColumn);
            }
            else
            {
                throw new StepReadException("Expected enumeration ending dot", _currentLineNumber, _currentColumn);
            }
        }

        StepKeywordToken ParseKeyword()
        {
            int tokenLine = _currentLineNumber;
            int tokenColumn = _currentColumn;
            string value = TakeWhile(IsKeywordCharacter);
            return new StepKeywordToken(value, tokenLine, tokenColumn);
        }

        string TakeWhile(Func<char, bool> predicate)
        {
            StringBuilder sb = new StringBuilder();
            char? c;
            while ((c = PeekCharacter()) != null && predicate(c.GetValueOrDefault()))
            {
                sb.Append(c);
                Advance();
            }

            return sb.ToString();
        }
    }
}
