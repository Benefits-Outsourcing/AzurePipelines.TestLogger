namespace AzurePipelines.TestLogger.Json
{
    public struct JsonToken
    {
        public JsonTokenType Type;
        public string Value;
        public int Line;
        public int Column;
    }
}
