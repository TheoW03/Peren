using LLVMSharp.Interop;

public interface IVisitor
{
	public LLVMValueRef Visit(IntegerNode node, LLVMBuilderRef builder, LLVMModuleRef module, Context context);
	public LLVMValueRef Visit(FloatNode node, LLVMBuilderRef builder, LLVMModuleRef module, Context context);

	public LLVMValueRef Visit(OpNode node, LLVMBuilderRef builder, LLVMModuleRef module, Context context);
	public LLVMValueRef Visit(VaraibleReferenceNode node, LLVMBuilderRef builder, LLVMModuleRef module, Context context);



}