namespace AzurePipelines.TestLogger.Json
{
    public enum JsonTokenType
    {
        LeftCurlyBracket,   // [
        LeftSquareBracket,  // {
        RightCurlyBracket,  // ]
        RightSquareBracket, // }
        Colon,              // :
        Comma,              // ,
        Null,
        True,
        False,
        Number,
        String,
        EOF
    }
}
