using System.Runtime.CompilerServices;
using Lexxer;

public enum TypeEnum
{
    INTEGER,
    FLOAT,
    CHAR,
    STRUCT,
    POINTER,
    VOID,
    BOOL
}

public abstract class LacusType
{
    public LacusType? simplerType { get; set; }
    public Dictionary<string, LacusType> VarainceOfTypes { get; set; }
    public string? name { get; set; }
    public bool IsConst { get; set; } = false;

    public bool IsUnsigned { get; set; } = false;

    // public Tokens Op { get; set; } ;

    public LacusType(bool isConst, bool isUnsigned =false)
    {
        IsConst = isConst;
        IsUnsigned = isUnsigned;

    }

    public LacusType(string _name, Dictionary<string, LacusType> varainceOfTypes, bool isConst)
    {
        name = _name;
        VarainceOfTypes = varainceOfTypes;
        IsConst = isConst;
    }

    public LacusType(LacusType simplerType, bool isConst, bool isUnsigned = false)
    {
        this.simplerType = simplerType;
        IsConst = isConst;
        IsUnsigned = isUnsigned;

    }

    public abstract bool CanAccept(LacusType type);

    public abstract int size();

    public abstract bool OpAccept(Tokens op);
}