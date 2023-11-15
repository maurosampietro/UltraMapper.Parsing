using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace UltraMapper.Parsing
{
    public sealed class ArrayParam : IParsedParam
    {
        public string Name { get; set; } = String.Empty;
        public int Index { get; set; } = 0;

        public int Count => Array.Count + Complex.Count + Simple.Count;

        private IList<IParsedParam> _items;
        public IEnumerable<IParsedParam> Items
        {
            get
            {
                return _items = new List<IParsedParam>()
                    .Concat( Array )
                    .Concat( Complex )
                    .Concat( Simple )
                    .OrderBy( p => p.Index )
                    .ToList();
            }

            set
            {
                Array.Clear();
                Complex.Clear();
                Simple.Clear();

                foreach(var item in value)
                {
                    switch(item)
                    {
                        case null: this.Simple.Add( null ); break;
                        case ComplexParam cp2: this.Complex.Add( cp2 ); break;
                        case SimpleParam sp: this.Simple.Add( sp ); break;
                        case ArrayParam ap: this.Array.Add( ap ); break;
                    }
                }
            }
        }

        public List<ArrayParam> Array { get; } = new List<ArrayParam>();
        public List<ComplexParam> Complex { get; } = new List<ComplexParam>();
        public List<SimpleParam> Simple { get; } = new List<SimpleParam>();

        public bool CompareName( string otherName, StringComparison comparison )
        {
            throw new NotImplementedException();
        }
    }
}
