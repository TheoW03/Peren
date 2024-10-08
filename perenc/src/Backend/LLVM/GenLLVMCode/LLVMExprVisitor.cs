using LacusLLVM.Frontend.Parser.AST;
using LacusLLVM.SemanticAanylyzerVisitor;
using Lexxer;
using LLVMSharp.Interop;

namespace LacusLLVM.LLVMCodeGen.Visitors.StatementVisit;

public class LLVMExprVisitor(
    LLVMContext context,
    LLVMBuilderRef builderRef,
    LLVMModuleRef moduleRef
) : ExpressionVisit<LLVMValueRef>
{
    public override LLVMValueRef Visit(IntegerNode node)
    {
        return LLVMValueRef.CreateConstInt(node.Range switch
        {
            Range.OneBit => LLVMTypeRef.Int1,
            Range.EightBit => LLVMTypeRef.Int8,
            Range.SixteenBit => LLVMTypeRef.Int16,
            Range.ThirtyTwoBit => LLVMTypeRef.Int32,
            Range.SixtyFourBit => LLVMTypeRef.Int64,
            _ => throw new Exception("error out of range")
        }, (ulong)node.Value);
    }

    public override LLVMValueRef Visit(FloatNode node)
    {
        return LLVMValueRef.CreateConstReal(LLVMTypeRef.Float, node.Value);
    }

    public override LLVMValueRef Visit(BoolNode node)
    {
        return LLVMValueRef.CreateConstInt(LLVMTypeRef.Int1, (ulong)((node.Value) ? 1 : 0));
    }

    public override LLVMValueRef Visit(FunctionCallNode node)
    {
        LLVMFunction function = context.GetFunction(node.Name.buffer);
        return builderRef.BuildCall2(
            function.FunctionType,
            function.FunctionValue,
            node.ParamValues.Select(n =>
                    n.Visit(new LLVMExprVisitor(context, builderRef, moduleRef))
                ) //oprams
                .ToArray(), //params
            "funcCall"
        );
    }

    public override LLVMValueRef Visit(OpNode node)
    {
        LLVMValueRef L = node.Left.Visit(this);
        LLVMValueRef R = node.Right.Visit(this);
        if (node.FloatExpr)
        {
            return node.Token.tokenType switch
            {
                TokenType.Addition => builderRef.BuildFAdd(L, R, "addtmp"),
                TokenType.Subtraction => builderRef.BuildFSub(L, R, "subtmp"),
                TokenType.Multiplication => builderRef.BuildFMul(L, R, "multmp"),
                TokenType.Division => builderRef.BuildFDiv(L, R, "divtmp"),
                _ => throw new Exception($"not accepted float math op {node.Token}")
            };
        }

        return node.Token.tokenType switch
        {
            TokenType.Addition => builderRef.BuildAdd(L, R, "addtmp"),
            TokenType.Subtraction => builderRef.BuildSub(L, R, "subtmp"),
            TokenType.Multiplication => builderRef.BuildMul(L, R, "multmp"),
            TokenType.Division => (node.IsUnsignedExpr)
                ? builderRef.BuildUDiv(L, R, "divtmp")
                : builderRef.BuildSDiv(L, R, "udivtmp"),
            TokenType.Modulas => builderRef.BuildSRem(L, R, "modtmp"),
            TokenType.Or => builderRef.BuildOr(L, R, "or"),
            TokenType.Xor => builderRef.BuildXor(L, R, "xor"),
            TokenType.And => builderRef.BuildAnd(L, R, "and"),
            TokenType.Not => builderRef.BuildNot(L, "not"),
            TokenType.RShift => (node.IsUnsignedExpr)
                ? builderRef.BuildLShr(L, R, "r_usigned_bitshift")
                : builderRef.BuildAShr(L, R, "r_signed_bitshift"),
            TokenType.LShift => builderRef.BuildShl(L, R, "L_bitshift"),
            _ => throw new Exception($"not accepted int math op {node.Token}")
        };
    }

    public override LLVMValueRef Visit(VaraibleReferenceNode node)
    {
        LLVMVar a = context.GetVar(node.Name.buffer);
        if (node is ArrayRefNode arr)
        {
            var loc = builderRef.BuildInBoundsGEP2(a.Type, a.Value,
            [
                arr.Elem.Visit(new LLVMExprVisitor(context, builderRef, moduleRef))
            ]);
            return builderRef.BuildLoad2(a.Type, loc, node.Name.buffer);
        }

        return builderRef.BuildLoad2(a.Type, a.Value, node.Name.buffer);
    } //:3

    public override LLVMValueRef Visit(BooleanExprNode node)
    {
        LLVMValueRef L = node.Left.Visit(this);
        LLVMValueRef R = node.Right.Visit(this);
        if (!node.IsFloat)
            return node.Op.tokenType switch
            {
                TokenType.BoolEq => builderRef.BuildICmp(LLVMIntPredicate.LLVMIntEQ, L, R, "cmp"),
                TokenType.Lt => (node.IsUnsigned)
                    ? builderRef.BuildICmp(LLVMIntPredicate.LLVMIntULT, L, R, "cmp")
                    : builderRef.BuildICmp(LLVMIntPredicate.LLVMIntSLT, L, R, "cmp"),
                TokenType.Lte => (node.IsUnsigned)
                    ? builderRef.BuildICmp(LLVMIntPredicate.LLVMIntULE, L, R, "cmp")
                    : builderRef.BuildICmp(LLVMIntPredicate.LLVMIntSLE, L, R, "cmp"),
                TokenType.Gt => (node.IsUnsigned)
                    ? builderRef.BuildICmp(LLVMIntPredicate.LLVMIntUGT, L, R, "cmp")
                    : builderRef.BuildICmp(LLVMIntPredicate.LLVMIntSGT, L, R, "cmp"),
                TokenType.Gte => (node.IsUnsigned)
                    ? builderRef.BuildICmp(LLVMIntPredicate.LLVMIntUGE, L, R, "cmp")
                    : builderRef.BuildICmp(LLVMIntPredicate.LLVMIntULT, L, R, "cmp"),
                TokenType.NotEquals => builderRef.BuildICmp(LLVMIntPredicate.LLVMIntNE, L, R, "cmp"),
                _ => throw new Exception($"not accepted float bool op {node.Op}")
            };
        return node.Op.tokenType switch
        {
            TokenType.BoolEq => builderRef.BuildFCmp(LLVMRealPredicate.LLVMRealOEQ, L, R, "cmp"),
            TokenType.Lt => builderRef.BuildFCmp(LLVMRealPredicate.LLVMRealOLT, L, R, "cmp"),
            TokenType.Lte => builderRef.BuildFCmp(LLVMRealPredicate.LLVMRealOLE, L, R, "cmp"),
            TokenType.Gt => builderRef.BuildFCmp(LLVMRealPredicate.LLVMRealOGT, L, R, "cmp"),
            TokenType.Gte => builderRef.BuildFCmp(LLVMRealPredicate.LLVMRealOGE, L, R, "cmp"),
            TokenType.NotEquals => builderRef.BuildFCmp(LLVMRealPredicate.LLVMRealONE, L, R, "cmp"),
            _ => throw new Exception($"not accepted Int bool op {node.Op}")
        };
    }

    public override LLVMValueRef Visit(CharNode node)
    {
        return LLVMValueRef.CreateConstInt(LLVMTypeRef.Int8, node.Value);
    }

    public override LLVMValueRef Visit(CastNode node)
    {
        var v = node.Expr.Visit(this);
        var targetType = Compile.ToLLVMType(node.type, context);
        return node.inferredtype switch
        {
            CastType.FLOAT => builderRef.BuildFPToSI(v, targetType),
            CastType.INT => builderRef.BuildSIToFP(v, targetType),
            CastType.SEXT => builderRef.BuildSExt(v, targetType),
            CastType.TRUNCATE => builderRef.BuildTrunc(v, targetType),
            _ => builderRef.BuildTrunc(v, targetType)
        };
    }

    public override LLVMValueRef Visit(StringNode node)
    {
        var c = builderRef.BuildGlobalString(node.Value);
        return c;
    }
}