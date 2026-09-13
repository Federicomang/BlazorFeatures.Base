using System;

namespace BlazorFeatures.Abstractions.Tools
{
    public static class UrlEncodedValueTypes
    {
        public const string String = "string";
        public const string Boolean = "bool";
        public const string Byte = "byte";
        public const string SignedByte = "sbyte";
        public const string Int16 = "short";
        public const string UInt16 = "ushort";
        public const string Int32 = "int";
        public const string UInt32 = "uint";
        public const string Int64 = "long";
        public const string UInt64 = "ulong";
        public const string Single = "float";
        public const string Double = "double";
        public const string Decimal = "decimal";
        public const string Char = "char";
        public const string Guid = "guid";
        public const string DateTime = "datetime";
        public const string DateTimeOffset = "datetimeoffset";
        public const string DateOnly = "dateonly";
        public const string TimeOnly = "timeonly";
        public const string TimeSpan = "timespan";
        public const string Uri = "uri";
        public const string Json = "json";

        public static string ArrayOf(string elementType)
        {
            if (string.IsNullOrWhiteSpace(elementType))
                throw new ArgumentException("Il tipo dell'elemento non può essere vuoto.", nameof(elementType));

            return elementType.Trim() + "[]";
        }
    }
}
