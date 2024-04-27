using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IxMilia.Step.Schemas.ExplicitDraughting;
using IxMilia.Step.Syntax;
using IxMilia.Step.Tokens;

namespace IxMilia.Step
{
    class StepWriter(StepFile stepFile, bool inlineReferences)
    {
        int _currentLineLength;
        bool _honorLineLength = true;
        readonly Dictionary<StepItem, int> _itemMap = new();
        int _nextId;

        const int MaxLineLength = 80;

        public string GetContents()
        {
            StringBuilder builder = new StringBuilder();

            _honorLineLength = false;
            WriteDelimitedLine(StepFile.MagicHeader, builder);

            // output header
            WriteDelimitedLine(StepFile.HeaderText, builder);
            StepHeaderSectionSyntax headerSyntax = stepFile.GetHeaderSyntax();
            foreach (StepHeaderMacroSyntax macro in headerSyntax.Macros)
            {
                WriteHeaderMacro(macro, builder);
            }

            WriteDelimitedLine(StepFile.EndSectionText, builder);

            _honorLineLength = true;

            // data section
            WriteDelimitedLine(StepFile.DataText, builder);
            foreach (StepItem item in stepFile.Items)
            {
                WriteItem(item, builder);
            }

            WriteDelimitedLine(StepFile.EndSectionText, builder);
            WriteDelimitedLine(StepFile.MagicFooter, builder);

            return builder.ToString();
        }

        void WriteHeaderMacro(StepHeaderMacroSyntax macro, StringBuilder builder)
        {
            WriteText(macro.Name, builder);
            WriteTokens(macro.Values.GetTokens(), builder);
            WriteToken(StepSemicolonToken.Instance, builder);
            WriteNewLine(builder);
        }

        int WriteItem(StepItem item, StringBuilder builder)
        {
            if (!inlineReferences)
            {
                // not inlining references, need to write out entities as we see them
                foreach (StepItem referencedItem in item.GetReferencedItems())
                {
                    if (!_itemMap.ContainsKey(referencedItem))
                    {
                        int refid = WriteItem(referencedItem, builder);
                    }
                }
            }

            int id = ++_nextId;
            StepSyntax syntax = GetItemSyntax(item, id);
            WriteToken(new StepEntityInstanceToken(id, -1, -1), builder);
            WriteToken(StepEqualsToken.Instance, builder);
            WriteTokens(syntax.GetTokens(), builder);
            WriteToken(StepSemicolonToken.Instance, builder);
            WriteNewLine(builder);
            return id;
        }

        /// <summary>
        /// Internal for testing.
        /// </summary>
        internal void WriteTokens(IEnumerable<StepToken> tokens, StringBuilder builder)
        {
            foreach (StepToken token in tokens)
            {
                WriteToken(token, builder);
            }
        }

        void WriteToken(StepToken token, StringBuilder builder)
        {
            WriteText(token.ToString(this), builder);
        }

        void WriteDelimitedLine(string text, StringBuilder builder)
        {
            WriteText(text, builder);
            WriteToken(StepSemicolonToken.Instance, builder);
            WriteNewLine(builder);
        }

        void WriteText(string text, StringBuilder builder)
        {
            if (_honorLineLength && _currentLineLength + text.Length > MaxLineLength)
            {
                WriteNewLine(builder);
            }

            builder.Append(text);
            _currentLineLength += text.Length;
        }

        void WriteNewLine(StringBuilder builder)
        {
            builder.Append("\r\n");
            _currentLineLength = 0;
        }

        StepSyntax GetItemSyntax(StepItem item, int expectedId)
        {
            if (!_itemMap.ContainsKey(item))
            {
                StepSyntaxList parameters = new StepSyntaxList(-1, -1, item.GetParameters(this));
                StepSimpleItemSyntax syntax = new StepSimpleItemSyntax(item.ItemTypeString, parameters);
                _itemMap.Add(item, expectedId);
                return syntax;
            }
            else
            {
                return GetItemSyntax(item);
            }
        }

        public StepSyntax GetItemSyntax(StepItem item)
        {
            if (inlineReferences)
            {
                StepSyntaxList parameters = new StepSyntaxList(-1, -1, item.GetParameters(this));
                return new StepSimpleItemSyntax(item.ItemTypeString, parameters);
            }
            else
            {
                return new StepEntityInstanceReferenceSyntax(_itemMap[item]);
            }
        }

        public StepSyntax GetItemSyntax(IList<double> list)
        {
            IEnumerable<StepSyntax> itemSyntaxes = list.Select(value => GetItemSyntax(value));
            return new StepSyntaxList(itemSyntaxes);
        }

        public StepSyntax GetItemSyntax(double value)
        {
            return new StepRealSyntax(value);
        }

        public StepSyntax GetItemSyntax(string value)
        {
            return new StepStringSyntax(value);
        }

        public StepSyntax GetItemSyntaxOrAuto(StepItem item)
        {
            return item == null
                ? new StepAutoSyntax()
                : GetItemSyntax(item);
        }

        public static StepEnumerationValueSyntax GetBooleanSyntax(bool value)
        {
            string text = value ? "T" : "F";
            return new StepEnumerationValueSyntax(text);
        }

        internal static IEnumerable<string> SplitStringIntoParts(string str, int maxLength = 256)
        {
            List<string> parts = [];
            if (str != null)
            {
                int offset = 0;
                while (offset < str.Length)
                {
                    int length = Math.Min(maxLength, str.Length - offset);
                    parts.Add(str.Substring(offset, length));
                    offset += length;
                }
            }
            else
            {
                parts.Add(string.Empty);
            }

            return parts;
        }
    }
}
