using LacusLLVM.LLVMCodeGen.Visitors.StatementVisit;
using Lexxer;

namespace LacusLLVM.Frontend.Parser.AST;

public class StructNode(
    List<VaraibleDeclarationNode> vars,
    Tokens name) : TopLevelStatement
{
    public List<VaraibleDeclarationNode> Vars { get; set; } = vars;
    public Tokens Name { get; set; } = name;

    // public override void Visit(StatementVisit visitor) => visitor.Visit(this);
    
    public override void Visit(TopLevelVisitor v) => v.Visit(this);
}