using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IxMilia.Step.Schemas.ExplicitDraughting;
using IxMilia.Step.Syntax;

namespace IxMilia.Step
{
    public class StepFile
    {
        internal const string MagicHeader = "ISO-10303-21";
        internal const string MagicFooter = "END-" + MagicHeader;
        internal const string HeaderText = "HEADER";
        internal const string EndSectionText = "ENDSEC";
        internal const string DataText = "DATA";

        internal const string FileDescriptionText = "FILE_DESCRIPTION";
        internal const string FileNameText = "FILE_NAME";
        internal const string FileSchemaText = "FILE_SCHEMA";

        // FILE_DESCRIPTION values
        public string Description { get; set; }
        public string ImplementationLevel { get; set; }

        // FILE_NAME values
        public string Name { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Author { get; set; }
        public string Organization { get; set; }
        public string PreprocessorVersion { get; set; }
        public string OriginatingSystem { get; set; }
        public string Authorization { get; set; }

        // FILE_SCHEMA values
        public HashSet<StepSchemaTypes> Schemas { get; } = [];
        public List<string> UnsupportedSchemas { get; } = [];

        public List<StepItem> Items { get; } = [];

        public static StepFile Load(string path)
        {
            using FileStream stream = new FileStream(path, FileMode.Open);
            return Load(stream);
        }

        public static StepFile Load(Stream stream)
        {
            return new StepReader(stream).ReadFile();
        }

        public static StepFile Parse(string data)
        {
            using MemoryStream stream = new MemoryStream();
            using StreamWriter writer = new StreamWriter(stream);
            writer.Write(data);
            writer.Flush();
            stream.Seek(0, SeekOrigin.Begin);
            return Load(stream);
        }

        public string GetContentsAsString(bool inlineReferences = false)
        {
            StepWriter writer = new StepWriter(this, inlineReferences);
            return writer.GetContents();
        }

        public void Save(string path, bool inlineReferences = false)
        {
            using FileStream stream = new FileStream(path, FileMode.Create);
            Save(stream, inlineReferences);
        }

        public void Save(Stream stream, bool inlineReferences = false)
        {
            using StreamWriter streamWriter = new StreamWriter(stream);
            streamWriter.Write(GetContentsAsString(inlineReferences));
            streamWriter.Flush();
        }

        /// <summary>
        /// Gets all top-level items (i.e., not referenced by any other item) in the file.
        /// </summary>
        public IEnumerable<StepItem> GetTopLevelItems()
        {
            HashSet<StepItem> visitedItems = [];
            HashSet<StepItem> referencedItems = [];
            foreach (StepItem item in Items)
            {
                MarkReferencedItems(item, visitedItems, referencedItems);
            }

            return Items.Where(item => !referencedItems.Contains(item));
        }

        static void MarkReferencedItems(StepItem item, HashSet<StepItem> visitedItems, HashSet<StepItem> referencedItems)
        {
            if (visitedItems.Add(item))
            {
                foreach (StepItem referenced in item.GetReferencedItems())
                {
                    referencedItems.Add(referenced);
                    MarkReferencedItems(referenced, visitedItems, referencedItems);
                }
            }
        }

        internal StepHeaderSectionSyntax GetHeaderSyntax()
        {
            List<StepHeaderMacroSyntax> macros =
            [
                new StepHeaderMacroSyntax(
                    FileDescriptionText,
                    new StepSyntaxList(
                        new StepSyntaxList(StepWriter.SplitStringIntoParts(Description)
                            .Select(s => new StepStringSyntax(s))),
                        new StepStringSyntax(ImplementationLevel))),

                new StepHeaderMacroSyntax(
                    FileNameText,
                    new StepSyntaxList(
                        new StepStringSyntax(Name),
                        new StepStringSyntax(Timestamp.ToString("O")),
                        new StepSyntaxList(StepWriter.SplitStringIntoParts(Author)
                            .Select(s => new StepStringSyntax(s))),
                        new StepSyntaxList(StepWriter.SplitStringIntoParts(Organization)
                            .Select(s => new StepStringSyntax(s))),
                        new StepStringSyntax(PreprocessorVersion),
                        new StepStringSyntax(OriginatingSystem),
                        new StepStringSyntax(Authorization))),

                new StepHeaderMacroSyntax(
                    FileSchemaText,
                    new StepSyntaxList(
                        new StepSyntaxList(
                            Schemas
                                .Select(s => s.ToSchemaName())
                                .Concat(UnsupportedSchemas)
                                .Select(s => new StepStringSyntax(s)))))
            ];

            return new StepHeaderSectionSyntax(-1, -1, macros);
        }
    }
}
