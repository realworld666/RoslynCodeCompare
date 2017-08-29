using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;

namespace RoslynTest
{
    class SyntaxWalker : CSharpSyntaxWalker
    {
        private readonly SemanticModel _model;

        public class FieldDef
        {
            public string type;
            public string name;
            public string accessibility;
            public ImmutableArray<AttributeData> attributes;
            public string extra;
        }
        public List<FieldDef> DefinedFields = new List<FieldDef>();
        private string _name;
        private INamedTypeSymbol _ignoreAttribute;

        public string FileName
        {
            get
            {
                return _name;
            }
        }

        public SyntaxWalker(SemanticModel model, string filename, INamedTypeSymbol ignoreAttribute)
        {
            _model = model;
            _name = filename;
            _ignoreAttribute = ignoreAttribute;
        }


        public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            base.VisitFieldDeclaration(node);

            foreach (var variable in node.Declaration.Variables)
            {
                var fieldSymbol = _model.GetDeclaredSymbol(variable);

                if (fieldSymbol.GetAttributes().Any(a => a.AttributeClass == _ignoreAttribute))
                    continue;

                string extra = "";
                if (fieldSymbol.IsStatic)
                    extra += "static ";
                DefinedFields.Add(new FieldDef()
                {
                    type = node.Declaration.Type.ToString(),
                    name = fieldSymbol.Name,
                    accessibility = fieldSymbol.DeclaredAccessibility.ToString().ToLower(),
                    extra = extra,
                    attributes = fieldSymbol.GetAttributes()
                });
            }
        }
    }
}
