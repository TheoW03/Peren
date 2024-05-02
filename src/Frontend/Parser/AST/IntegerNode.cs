using LLVMSharp.Interop;
using Lexxer;

public class IntegerNode : INode
{
    public int n;
    public LLVMTypeRef IntType;

    // public IntegerNode(int n)
    // {
    //     this.n = n;
    // }

    public IntegerNode(int n, LLVMTypeRef type)
    {
        this.n = n;
        this.IntType = type;
    }

    public IntegerNode(Tokens n, LLVMTypeRef type)
    {
        this.n = int.Parse(n.buffer);
        this.IntType = type;
    }

    public LLVMValueRef CodeGen(
        IVisitor visitor,
        LLVMBuilderRef builder,
        LLVMModuleRef module,
        Context scope
    )
    {
        return visitor.Visit(this, builder, module, scope);

        // throw new NotImplementedException();
    }

    public override string ToString()
    {
        return n.ToString();
    }

    public void Transform(IOptimize optimizer, Context graph)
    {
        throw new NotImplementedException();
    }
}
