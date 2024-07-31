using LacusLLVM.Frontend.Parser.AST;
using LacusLLVM.SemanticAanylyzerVisitor;
using LacusLLVM.SemanticAanylyzerVisitor.Backend;
using Lexxer;
using LLVMSharp.Interop;

namespace LacusLLVM.LLVMCodeGen.Visitors.StatementVisit;

public struct LLVMVar(LLVMValueRef valueRef, LLVMTypeRef typeRef)
{
    public LLVMValueRef Value { get; set; } = valueRef;
    public LLVMTypeRef Type { get; set; } = typeRef;
};

public struct LLVMFunction(LLVMTypeRef functionType, LLVMTypeRef returnType, LLVMValueRef valueRef)
{
    public LLVMTypeRef returnType = returnType;
    public LLVMTypeRef FunctionType = functionType;
    public LLVMValueRef FunctionValue = valueRef;

    public LLVMBasicBlockRef retVoidblock;
};

public struct LLVMType(
    LLVMTypeRef typeRef,
    List<VaraibleDeclarationNode> varaibleDeclarationNodes)
{
    public LLVMTypeRef Type { get; set; } = typeRef;

    public List<VaraibleDeclarationNode> Vars = varaibleDeclarationNodes;
};

public struct LLVMContext
{
    public CompileContext<LLVMVar> vars = new();
    public CompileContext<LLVMFunction> functions = new();
    public CompileContext<LLVMType> types = new();

    public LLVMContext()
    {
        // functions.AllocateScope();
        // types.AllocateScope();
        // vars.AllocateScope();
    }
}

public class LLVMStatementVisitor(LLVMBuilderRef builderRef, LLVMModuleRef moduleRef)
    : StatementVisit
{
    private LLVMContext Context { get; } = new();
    private LLVMFunction _currentFunction;
    private bool _returns;

    public override void Visit(VaraibleDeclarationNode node)
    {
        var type = ToLLVMType(node.Type);
        // if (Context.vars.Values == 0)
        // return;
        if (node is ArrayNode n)
        {
            var size = n.Size.Visit(new LLVMExprVisitor(Context, builderRef, moduleRef));
            var value = builderRef.BuildArrayAlloca(type, size);
            Context.vars.Add(node.Name.buffer, new LLVMVar(value, type));
        }
        else
        {
            var value = builderRef.BuildAlloca(type, node.Name.buffer);
            Context.vars.Add(node.Name.buffer, new LLVMVar(value, type));
            {
                if (node.ExpressionNode != null)
                {
                    LLVMValueRef eq = node.ExpressionNode.Visit(
                        new LLVMExprVisitor(Context, builderRef, moduleRef)
                    );
                    builderRef.BuildStore(eq, value);
                }
            }
        }
    }

    public override void Visit(VaraibleReferenceStatementNode node)
    {
        LLVMVar a = Context.vars.Get(node.Name.buffer);

        if (node is ArrayRefStatementNode arr)
        {
            var loc = builderRef.BuildInBoundsGEP2(a.Type, a.Value,
                new LLVMValueRef[]
                {
                    arr.Element.Visit(new LLVMExprVisitor(Context, builderRef, moduleRef))
                });
            builderRef.BuildStore(
                node.Expression.Visit(new LLVMExprVisitor(Context, builderRef, moduleRef)),
                loc
            );
        }
        else
        {
            builderRef.BuildStore(
                node.Expression.Visit(new LLVMExprVisitor(Context, builderRef, moduleRef)),
                a.Value
            );
        }
    }

    public override void Visit(FunctionCallNode node)
    {
        LLVMFunction a = Context.functions.Get(node.Name.buffer);
        builderRef.BuildCall2(
            a.FunctionType,
            a.FunctionValue,
            node.ParamValues.Select(n =>
                    n.Visit(new LLVMExprVisitor(Context, builderRef, moduleRef))
                )
                .ToArray(),
            "funcCall"
        );
    }

    public override void Visit(FunctionNode node)
    {
        LLVMTypeRef funcType = LLVMTypeRef.CreateFunction(
            ToLLVMType(node.RetType.Name),
            node.Parameters //params
                .Select(n => ToLLVMType(n.Type)) //converts param types
                .ToArray(), //to an array
            false
        );
        // node.Parameters.ForEach(n => n.Visit(this));
        LLVMValueRef function = moduleRef.AddFunction(node.Name.buffer, funcType);
        _currentFunction = new LLVMFunction(funcType, ToLLVMType(node.RetType.Name), function);
        Context.functions.Add(node.Name.buffer, _currentFunction);
        if (node.AttributesTuple.isExtern)
        {
            function.Linkage = LLVMLinkage.LLVMExternalLinkage;
            return;
        }

        LLVMBasicBlockRef entry = function.AppendBasicBlock("entry");
        builderRef.PositionAtEnd(entry);
        // Context.vars.AllocateScope();
        foreach (var (param, index) in node.Parameters.Select((param, index) => (param, index)))
        {
            var llvmParam = function.GetParam((uint)index);
            var name = param.Name.buffer;
            llvmParam.Name = name;
            Context.vars.Add(
                param.Name.buffer,
                new LLVMVar(
                    builderRef.BuildAlloca(ToLLVMType(param.Type), name),
                    ToLLVMType(param.Type)
                )
            );
            builderRef.BuildStore(
                function.GetParam((uint)index),
                Context.vars.Get(param.Name.buffer).Value
            );
        }

        node.Statements.ForEach(n => n.Visit(this));
        if (!_returns && _currentFunction.returnType == LLVMTypeRef.Void)
            builderRef.BuildRetVoid();
        // Context.vars.DeallocateScope();
    }

    public override void Visit(ReturnNode node)
    {
        if (_currentFunction.returnType == LLVMTypeRef.Void)
        {
            builderRef.BuildRetVoid();
            _returns = true;
        }
        else
        {
            builderRef.BuildRet(
                (node.Expression ?? throw new Exception("null return ")).Visit(
                    new LLVMExprVisitor(Context, builderRef, moduleRef)
                )
            );
        }
    }


    public override void Visit(ForLoopNode node)
    {
        var loopCond = _currentFunction.FunctionValue.AppendBasicBlock("loopCOnd");

        var loopBody = _currentFunction.FunctionValue.AppendBasicBlock("loopBody");
        var loopEnd = _currentFunction.FunctionValue.AppendBasicBlock("Loopend");

        // Context.vars.AllocateScope();
        node.Iterator.Visit(this);
        builderRef.BuildBr(loopCond);
        builderRef.PositionAtEnd(loopCond);
        var v = node.Expr.Visit(new LLVMExprVisitor(Context, builderRef, moduleRef));
        builderRef.BuildCondBr(v, loopBody, loopEnd);
        builderRef.PositionAtEnd(loopBody);
        node.Statements.ForEach(n => n.Visit(this));
        node.Inc.Visit(this);
        builderRef.BuildBr(loopCond);
        // Context.vars.DeallocateScope();
        builderRef.PositionAtEnd(loopEnd);
    }

    public override void Visit(WhileLoopNode node)
    {
        var loopCond = _currentFunction.FunctionValue.AppendBasicBlock("while.cond");

        var loopBody = _currentFunction.FunctionValue.AppendBasicBlock("while.body");
        var loopEnd = _currentFunction.FunctionValue.AppendBasicBlock("while.end");
        builderRef.BuildBr(loopCond);
        builderRef.PositionAtEnd(loopCond);
        LLVMValueRef v = node.Expression.Visit(new LLVMExprVisitor(Context, builderRef, moduleRef));
        builderRef.BuildCondBr(v, loopBody, loopEnd);
        // Context.vars.AllocateScope();

        builderRef.PositionAtEnd(loopBody);
        node.StatementNodes.ForEach(n => n.Visit(this));
        builderRef.BuildBr(loopCond);

        // Context.vars.DeallocateScope();
        builderRef.PositionAtEnd(loopEnd);
    }

    public override void Visit(IfNode node)
    {
        LLVMValueRef v = node.Expression.Visit(new LLVMExprVisitor(Context, builderRef, moduleRef));

        // Context.vars.AllocateScope();
        if (node.ElseNode.StatementNodes.Count != 0)
        {
            var If = _currentFunction.FunctionValue.AppendBasicBlock("if.then");
            var Else = _currentFunction.FunctionValue.AppendBasicBlock("else");
            var after = _currentFunction.FunctionValue.AppendBasicBlock("if.after");
            // Context.vars.AllocateScope();
            builderRef.BuildCondBr(v, If, Else);
            builderRef.PositionAtEnd(If);

            node.StatementNodes.ForEach(n => n.Visit(this));
            builderRef.BuildBr(after);
            // Context.vars.DeallocateScope();
            // Context.vars.AllocateScope();
            builderRef.PositionAtEnd(Else);
            node.ElseNode.StatementNodes.ForEach(n => n.Visit(this));
            // Context.vars.DeallocateScope();
            builderRef.BuildBr(after);
            builderRef.PositionAtEnd(after);
        }
        else
        {
            LLVMBasicBlockRef If = _currentFunction.FunctionValue.AppendBasicBlock("if.then");
            LLVMBasicBlockRef After = _currentFunction.FunctionValue.AppendBasicBlock("if.after");
            builderRef.BuildCondBr(v, If, After);
            // Context.vars.AllocateScope();
            builderRef.PositionAtEnd(If);
            node.StatementNodes.ForEach(n => n.Visit(this));
            // Context.vars.DeallocateScope();
            builderRef.BuildBr(After);
            builderRef.PositionAtEnd(After);
        }
    }

    public override void Visit(StructNode node)
    {
        var llvmstruct = moduleRef.Context.CreateNamedStruct(node.Name.buffer);
        Context.types.Add(node.Name.buffer, new LLVMType(
            llvmstruct, node.Vars));
    }

    public LLVMTypeRef ToLLVMType(Tokens type)
    {
        if (type.tokenType == TokenType.WORD)
        {
            return Context.types.Get(type.buffer).Type;
        }

        return type.tokenType switch
        {
            TokenType.INT or TokenType.UINT => LLVMTypeRef.Int32,
            TokenType.INT16 or TokenType.UINT_16 => LLVMTypeRef.Int16,
            TokenType.INT64 or TokenType.ULONG => LLVMTypeRef.Int64,

            TokenType.FLOAT => LLVMTypeRef.Float,
            TokenType.BOOL => LLVMTypeRef.Int1,
            TokenType.CHAR or TokenType.BYTE or TokenType.SBYTE => LLVMTypeRef.Int8,
            TokenType.VOID => LLVMTypeRef.Void,
            TokenType.STRING => LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0),
            _ => throw new Exception($"undefined {type.ToString()} type")
        };
    }
}