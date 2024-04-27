using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using IxMilia.Step.Schemas.ExplicitDraughting;
using IxMilia.Step.Syntax;

namespace IxMilia.Step
{
    class StepReader
    {
        readonly StepLexer _lexer;
        readonly StepFile _file;

        public StepReader(Stream stream)
        {
            _file = new StepFile();
            StepTokenizer tokenizer = new StepTokenizer(stream);
            _lexer = new StepLexer(tokenizer.GetTokens());
        }

        public StepFile ReadFile()
        {
            StepFileSyntax fileSyntax = _lexer.LexFileSyntax();
            foreach (StepHeaderMacroSyntax headerMacro in fileSyntax.Header.Macros)
            {
                ApplyHeaderMacro(headerMacro);
            }

            Dictionary<int, StepItem> itemMap = new Dictionary<int, StepItem>();
            StepBinder binder = new StepBinder(itemMap);
            foreach (StepEntityInstanceSyntax itemInstance in fileSyntax.Data.ItemInstances)
            {
                if (itemMap.ContainsKey(itemInstance.Id))
                {
                    throw new StepReadException("Duplicate item instance", itemInstance.Line, itemInstance.Column);
                }

                StepItem item = StepItemBuilder.FromTypedParameter(binder, itemInstance.SimpleItemInstance);
                if (item != null)
                {
                    itemMap.Add(itemInstance.Id, item);
                    _file.Items.Add(item);
                }
            }

            binder.BindRemainingValues();

            foreach (StepItem item in _file.Items)
            {
                item.ValidateDomainRules();
            }

            return _file;
        }

        void ApplyHeaderMacro(StepHeaderMacroSyntax macro)
        {
            switch (macro.Name)
            {
                case StepFile.FileDescriptionText:
                    ApplyFileDescription(macro.Values);
                    break;
                case StepFile.FileNameText:
                    ApplyFileName(macro.Values);
                    break;
                case StepFile.FileSchemaText:
                    ApplyFileSchema(macro.Values);
                    break;
                default:
                    Debug.WriteLine($"Unsupported header macro '{macro.Name}' at {macro.Line}, {macro.Column}");
                    break;
            }
        }

        void ApplyFileDescription(StepSyntaxList valueList)
        {
            valueList.AssertListCount(2);
            _file.Description = valueList.Values[0].GetConcatenatedStringValue();
            _file.ImplementationLevel = valueList.Values[1].GetStringValue(); // TODO: handle appropriate values
        }

        void ApplyFileName(StepSyntaxList valueList)
        {
            valueList.AssertListCount(7);
            _file.Name = valueList.Values[0].GetStringValue();
            _file.Timestamp = valueList.Values[1].GetDateTimeValue();
            _file.Author = valueList.Values[2].GetConcatenatedStringValue();
            _file.Organization = valueList.Values[3].GetConcatenatedStringValue();
            _file.PreprocessorVersion = valueList.Values[4].GetStringValue();
            _file.OriginatingSystem = valueList.Values[5].GetStringValue();
            _file.Authorization = valueList.Values[6].GetStringValue();
        }

        void ApplyFileSchema(StepSyntaxList valueList)
        {
            valueList.AssertListCount(1);
            foreach (string schemaName in valueList.Values[0].GetValueList().Values.Select(v => v.GetStringValue()))
            {
                StepSchemaTypes schemaType;
                if (StepSchemaTypeExtensions.TryGetSchemaTypeFromName(schemaName, out schemaType))
                {
                    _file.Schemas.Add(schemaType);
                }
                else
                {
                    _file.UnsupportedSchemas.Add(schemaName);
                }
            }
        }
    }
}
