using System;

namespace UltraMapper.Parsing
{
    public class BooleanParam : SimpleParam
    {
        private const StringComparison _comparison = StringComparison.InvariantCultureIgnoreCase;

        public bool BoolValue => Boolean.TrueString.Equals( Value, _comparison );
    }
}
