using System;

namespace UltraMapper.Parsing
{
    public class OptionAttribute : Attribute
    {
        public string Name { get; set; } = String.Empty;
        public int Order { get; set; } = -1;
        public bool IsRequired { get; set; } = true;
        public bool IsIgnored { get; set; } = false;
    }
}
