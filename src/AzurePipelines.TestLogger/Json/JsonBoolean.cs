using System;

namespace AzurePipelines.TestLogger.Json
{
    public class JsonBoolean : JsonValue
    {
        public JsonBoolean(JsonToken token)
            : base(token.Line, token.Column)
        {
            if (token.Type == JsonTokenType.True)
            {
                Value = true;
            }
            else if (token.Type == JsonTokenType.False)
            {
                Value = false;
            }
            else
            {
                throw new ArgumentException("Token value should be either True or False.", nameof(token));
            }
        }

        public bool Value { get; }

        public static implicit operator bool(JsonBoolean jsonBoolean)
        {
            return jsonBoolean.Value;
        }
    }
}