using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;

namespace RoslynTest
{
    class Program
    {
        private static readonly IEnumerable<string> DefaultNamespaces =
            new[]
            {
                "System",
                "System.IO",
                "System.Net",
                "System.Linq",
                "System.Text",
                "System.Text.RegularExpressions",
                "System.Collections.Generic"
            };

        private static string runtimePath = @"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5.1\{0}.dll";

        private static readonly IEnumerable<MetadataReference> DefaultReferences =
            new[]
            {
                MetadataReference.CreateFromFile(string.Format(runtimePath, "mscorlib")),
                MetadataReference.CreateFromFile(string.Format(runtimePath, "System")),
                MetadataReference.CreateFromFile(string.Format(runtimePath, "System.Core"))
            };

        //private const string MyModelPath = @"D:\OneDrive\Documents\MMSaveEditor\Model";
        //private const string SourcePath = @"D:\OneDrive\Documents\MM_Decompiled2\Assembly-CSharp";
        private const string MyModelPath = @"D:\od\OneDrive\Documents\MMSaveEditor\Model";
        private const string SourcePath = @"D:\od\OneDrive\Documents\MM_Decompiled2\Assembly-CSharp";
        private static StreamWriter outputFile;

        private static readonly CSharpCompilationOptions DefaultCompilationOptions =
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithOverflowChecks(true).WithOptimizationLevel(OptimizationLevel.Release)
                .WithUsings(DefaultNamespaces);

        public static SyntaxTree Parse(string text, string filename = "", CSharpParseOptions options = null)
        {
            var stringText = SourceText.From(text, Encoding.UTF8);
            return SyntaxFactory.ParseSyntaxTree(stringText, options, filename);
        }

        static void Main(string[] args)
        {
            var ext = new List<string> { ".cs" };
            var myFiles = Directory.GetFiles(MyModelPath, "*.*", SearchOption.AllDirectories)
                 .Where(s => ext.Contains(Path.GetExtension(s)));

            var Mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);

            outputFile = new StreamWriter("ParseOutput.txt");

            int count = 0;
            foreach (string file in myFiles)
            {
                string fileName = Path.GetFileName(file);
                if (!File.Exists(Path.Combine(SourcePath, fileName)))
                {
                    outputFile.WriteLine(Path.Combine(SourcePath, fileName) + " does not exist");
                    continue;
                }
                // My version of the file
                var source = File.ReadAllText(file);
                var parsedSyntaxTree = Parse(source, "", CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp5));
                var compilation = CSharpCompilation.Create("MyCompilation", syntaxTrees: new[] { parsedSyntaxTree }, references: new[] { Mscorlib });
                var root = (CompilationUnitSyntax)parsedSyntaxTree.GetRoot();

                var walker = new SyntaxWalker(compilation.GetSemanticModel(parsedSyntaxTree), fileName, compilation.GetTypeByMetadataName("System.NonSerializedAttribute"));
                walker.Visit(root);

                // Original version of the file
                var original = File.ReadAllText(Path.Combine(SourcePath, fileName));
                var parsedSyntaxTree2 = Parse(original, "", CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp5));
                var compilation2 = CSharpCompilation.Create("MyCompilation", syntaxTrees: new[] { parsedSyntaxTree2 }, references: new[] { Mscorlib });
                var root2 = (CompilationUnitSyntax)parsedSyntaxTree2.GetRoot();
                var walker2 = new SyntaxWalker(compilation2.GetSemanticModel(parsedSyntaxTree2), fileName, compilation.GetTypeByMetadataName("System.NonSerializedAttribute"));
                walker2.Visit(root2);

                //Console.WriteLine(string.Format("Parsed {0} of {1}", ++count, myFiles.Count()));

                GoCompare(walker, walker2);


            }
            Console.WriteLine("Done");
            Console.Read();
        }

        private static void GoCompare(SyntaxWalker walker, SyntaxWalker walker2)
        {
            List<string> added = new List<string>();
            List<string> removed = new List<string>();
            List<string> modified = new List<string>();
            if (walker.DefinedFields.Count == walker2.DefinedFields.Count || walker.DefinedFields.Count > walker2.DefinedFields.Count)
            {
                foreach (var myField in walker.DefinedFields)
                {
                    SyntaxWalker.FieldDef match = walker2.DefinedFields.FirstOrDefault(f => f.name.Equals(myField.name) && f.type.Equals(myField.type) && f.extra.Equals(myField.extra));
                    if (match == null)
                    {
                        removed.Add(GenerateCodeVariable(myField));
                    }
                    else
                    {
                        if (!AttributesAreEqual(myField.attributes, match.attributes))
                        {
                            modified.Add(GenerateCodeVariable(myField));
                        }
                    }
                }
            }
            if (walker.DefinedFields.Count == walker2.DefinedFields.Count || walker.DefinedFields.Count < walker2.DefinedFields.Count)
            {
                foreach (var myField in walker2.DefinedFields)
                {
                    SyntaxWalker.FieldDef match = walker.DefinedFields.FirstOrDefault(f => f.name.Equals(myField.name) && f.type.Equals(myField.type) && f.extra.Equals(myField.extra));

                    if (match == null)
                    {
                        added.Add(GenerateCodeVariable(myField));
                    }
                    else
                    {
                        if (!AttributesAreEqual(myField.attributes, match.attributes))
                        {
                            modified.Add(GenerateCodeVariable(myField));
                        }
                    }
                }
            }

            if (added.Count > 0 || removed.Count > 0 || modified.Count > 0)
            {
                outputFile.WriteLine(string.Format("{0}:", walker.FileName));
                outputFile.WriteLine(string.Format("Added:"));
                foreach (string s in added)
                {
                    outputFile.WriteLine(s);
                }
                outputFile.WriteLine(string.Format("Removed:"));
                foreach (string s in removed)
                {
                    outputFile.WriteLine(s);
                }
                outputFile.WriteLine(string.Format("Modified:"));
                foreach (string s in modified)
                {
                    outputFile.WriteLine(s);
                }
            }

            outputFile.WriteLine();
        }

        private static string GenerateCodeVariable(SyntaxWalker.FieldDef myField)
        {
            string output = "";
            foreach (AttributeData attribute in myField.attributes)
            {
                output += string.Format("[{0}]\n", attribute.ToString());
            }
            output += string.Format("{0} {1} {2} {3};", myField.accessibility, myField.extra, myField.type, myField.name);
            return output;
        }

        private static bool AttributesAreEqual(ImmutableArray<AttributeData> myFieldAttributes, ImmutableArray<AttributeData> matchAttributes)
        {
            if (myFieldAttributes.Length != matchAttributes.Length)
            {
                return false;
            }
            return myFieldAttributes.Select((t, i) => i).All(i1 => matchAttributes.Any(a => a.ToString().Equals(myFieldAttributes[i1].ToString())));
        }
    }
}
