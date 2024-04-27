using IxMilia.Step.SchemaParser;
using Microsoft.FSharp.Collections;

namespace IxMilia.Step.Generator.Console
{
    public class Program
    {
        public static void Main(string[] args)
        {
            string? assemblyDir = Path.GetDirectoryName(typeof(Program).Assembly.Location);
            string repoRoot = Path.Combine(assemblyDir, "..", "..", "..", "..", "..");
            string outputDir = Path.Combine(repoRoot, "src", "IxMilia.Step", "Schemas", "ExplicitDraughting", "Generated");
            string schemaContent = File.ReadAllText(Path.Combine(repoRoot, "src", "IxMilia.Step.SchemaParser.Test", "Schemas", "minimal_201.exp"));
            IEnumerable<(string name, string contents)> entityDefinitions = GenerateSource(schemaContent);
            foreach ((string entityName, string entityDefinition) in entityDefinitions)
            {
                string outputPath = Path.Combine(outputDir, entityName);
                File.WriteAllText(outputPath, entityDefinition);
            }
        }

        static IEnumerable<(string name, string contents)> GenerateSource(string schemaContent)
        {
            Schema? schema = SchemaParser.SchemaParser.RunParser(schemaContent);
            FSharpList<Tuple<string, string>>? entityDefinitions = CSharpSourceGenerator.getAllFileDefinitions(
                schema,
                generatedNamespace: "IxMilia.Step.Schemas.ExplicitDraughting",
                usingNamespaces: new[] { "System", "System.Collections.Generic", "System.Linq", "IxMilia.Step.Collections", "IxMilia.Step.Syntax" },
                typeNamePrefix: "Step",
                defaultBaseClassName: "StepItem");
            foreach ((string? entityName, string? entityDefinition) in entityDefinitions)
            {
                yield return ($"{entityName}.Generated.cs", entityDefinition: entityDefinition);
            }
        }
    }
}
