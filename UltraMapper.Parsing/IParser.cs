using UltraMapper.Parsing.Parameters2;

namespace UltraMapper.Parsing
{
    public interface IParser
    {
        IParsedParam Parse( string text );
    }
}
