using System;

namespace UltraMapper.Parsing
{
    public class SimpleParam : IParsedParam
    {
        public string Name { get; set; }
        public int Index { get; set; }
        public virtual string Value { get; set; }
    }
}
