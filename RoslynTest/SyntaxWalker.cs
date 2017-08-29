using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        }
        public List<FieldDef> DefinedFields = new List<FieldDef>();
        private string _name;

        public string FileName
        {
            get
            {
                return _name;
            }
        }

        public SyntaxWalker(SemanticModel model, string filename)
        {
            _model = model;
            _name = filename;
        }


        public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            base.VisitFieldDeclaration(node);

            foreach (var variable in node.Declaration.Variables)
            {
                var fieldSymbol = _model.GetDeclaredSymbol(variable);

                DefinedFields.Add(new FieldDef()
                {
                    type = node.Declaration.Type.ToString(),
                    name = fieldSymbol.Name,
                    accessibility = fieldSymbol.DeclaredAccessibility.ToString().ToLower()
                });
            }
        }
    }
}
