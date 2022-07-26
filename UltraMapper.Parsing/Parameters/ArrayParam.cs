using System.Collections.Generic;
using System.Linq;

namespace UltraMapper.Parsing
{
    public class ArrayParam : IParsedParam
    {
        public string Name { get; set; }
        public int Index { get; set; }

        public ArrayParam() :
            this( new List<IParsedParam>( 16 ) )
        { }

        public ArrayParam( IEnumerable<IParsedParam> items )
        {
            _items = items.ToList();
        }

        private readonly List<IParsedParam> _items;
        public IReadOnlyList<IParsedParam> Items => _items;

        public IParsedParam this[ int index ] => this.Items[ index ];
        public void Add( IParsedParam item ) => _items.Add( item );
        public void Clear() => _items.Clear();
        public int Count() => _items.Count();
    }
}
