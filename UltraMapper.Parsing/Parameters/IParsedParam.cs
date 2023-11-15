using System;

namespace UltraMapper.Parsing
{
    public interface IParsedParam
    {
        string Name { get; set; }
        int Index { get; set; }

        bool CompareName( string otherName, StringComparison comparison );
    }
}
