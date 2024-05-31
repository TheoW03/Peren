public enum TypeEnum
{
    INTEGER,
    FLOAT,
    CHAR,
    STRUCT,
    POINTER,
    VOID
}

public class LacusType
{
    public string Typename { get; set; }
    public TypeEnum Type { get; set; }

    public bool isNative;

    public LacusType(string _typename, TypeEnum _type)
    {
        this.Typename = _typename;
        this.Type = _type;
        isNative = false;
    }

    public LacusType(TypeEnum _type)
    {
        this.Typename = "";
        this.Type = _type;
        isNative = true;
    }

    public override bool Equals(object? obj)
    {
        LacusType l = (LacusType)obj;
        if (isNative)
            return l.Type == Type;
        return l.Type == Type && Typename == l.Typename;
    }
}
