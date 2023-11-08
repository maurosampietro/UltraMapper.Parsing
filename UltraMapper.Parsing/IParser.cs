using UltraMapper.Parsing.Parameters2;

namespace UltraMapper.Parsing
{
    public interface IParser
    {
        IParsedParam Parse( string text );
    }

    public interface IParser2
    {
        IParsedParam2 Parse( string text );
    }
}
