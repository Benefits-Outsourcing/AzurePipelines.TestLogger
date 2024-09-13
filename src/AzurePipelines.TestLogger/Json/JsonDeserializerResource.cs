namespace AzurePipelines.TestLogger.Json
{
    public static class JsonDeserializerResource
    {
        public static string Format_IllegalCharacter(int value)
        {
            return $"Illegal character '{(char)value}' (Unicode hexadecimal {value:X4}).";
        }

        public static string Format_IllegalTrailingCharacterAfterLiteral(int value, string literal)
        {
            return $"Illegal character '{(char)value}' (Unicode hexadecimal {value:X4}) after the literal name '{literal}'.";
        }

        public static string Format_UnrecognizedLiteral(string literal)
        {
            return $"Invalid JSON literal. Expected literal '{literal}'.";
        }

        public static string Format_DuplicateObjectMemberName(string memberName)
        {
            return Format_InvalidSyntax("JSON object", $"Duplicate member name '{memberName}'");
        }

        public static string Format_InvalidFloatNumberFormat(string raw)
        {
            return $"Invalid float number format: {raw}";
        }

        public static string Format_FloatNumberOverflow(string raw)
        {
            return $"Float number overflow: {raw}";
        }

        public static string Format_InvalidSyntax(string syntaxName, string issue)
        {
            return $"Invalid {syntaxName} syntax. {issue}.";
        }

        public static string Format_InvalidSyntaxNotExpected(string syntaxName, char unexpected)
        {
            return $"Invalid {syntaxName} syntax. Unexpected '{unexpected}'.";
        }

        public static string Format_InvalidSyntaxNotExpected(string syntaxName, string unexpected)
        {
            return $"Invalid {syntaxName} syntax. Unexpected {unexpected}.";
        }

        public static string Format_InvalidSyntaxExpectation(string syntaxName, char expectation)
        {
            return $"Invalid {syntaxName} syntax. Expected '{expectation}'.";
        }

        public static string Format_InvalidSyntaxExpectation(string syntaxName, string expectation)
        {
            return $"Invalid {syntaxName} syntax. Expected {expectation}.";
        }

        public static string Format_InvalidSyntaxExpectation(string syntaxName, char expectation1, char expectation2)
        {
            return $"Invalid {syntaxName} syntax. Expected '{expectation1}' or '{expectation2}'.";
        }

        public static string Format_InvalidTokenExpectation(string tokenValue, string expectation)
        {
            return $"Unexpected token '{tokenValue}'. Expected {expectation}.";
        }

        public static string Format_InvalidUnicode(string unicode)
        {
            return $"Invalid Unicode [{unicode}]";
        }

        public static string Format_UnfinishedJSON(string nextTokenValue)
        {
            return $"Invalid JSON end. Unprocessed token {nextTokenValue}.";
        }

        public static string JSON_OpenString
        {
            get { return Format_InvalidSyntaxExpectation("JSON string", '\"'); }
        }

        public static string JSON_InvalidEnd
        {
            get { return "Invalid JSON. Unexpected end of file."; }
        }
    }
}
