using Lexxer;

namespace LacusLLVM.Frontend.SemanticAnalysis;

public class FloatType(bool isConst, Range range = Range.Float) : PerenType(isConst, range)
{
    public override bool CanAccept(PerenType type)
    {
        if (this.IsConst && !type.IsConst)
            return false;
        return type is IntegerType || type is FloatType;
    }

    public override int size()
    {
        throw new NotImplementedException();
    }

    public override bool OpAccept(Tokens op)
    {
        return op.tokenType switch
        {
            TokenType.Addition 
                or TokenType.Subtraction 
                or TokenType.Division 
                or TokenType.Multiplication => true,
            _ => false
        };
    }
}