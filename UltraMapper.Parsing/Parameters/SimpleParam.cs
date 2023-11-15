using System;

namespace UltraMapper.Parsing
{
    public sealed class SimpleParam : IParsedParam
    {
        public static readonly SimpleParam ANONYMOUS_NULL = new SimpleParam() { Name = String.Empty, Value = null };

        public string Name { get; set; } = String.Empty;
        public int Index { get; set; } = 0;
        public string Value { get; set; }

        public bool CompareName( string otherName, StringComparison comparison )
        {
            throw new NotImplementedException();
        }
    }
}
