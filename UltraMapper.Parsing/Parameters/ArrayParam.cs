using System;
using System.Collections.Generic;
using System.Linq;

namespace UltraMapper.Parsing
{
    public sealed class ArrayParam : IParsedParam
    {
        public string Name { get; set; } = String.Empty;
        public int Index { get; set; } = 0;

        public int Count() => Arrays.Count + Complex.Count + Simples.Count;

        private IList<IParsedParam> _items;
        public IEnumerable<IParsedParam> Items
        {
            get
            {
                return _items = new List<IParsedParam>()
                    .Concat( Arrays )
                    .Concat( Complex )
                    .Concat( Simples )
                    .OrderBy( p => p.Index )
                    .ToList();
            }

            set
            {
                Arrays.Clear();
                Complex.Clear();
                Simples.Clear();

                foreach(var item in value)
                {
                    switch(item)
                    {
                        case null: this.Simples.Add( null ); break;
                        case ComplexParam cp2: this.Complex.Add( cp2 ); break;
                        case SimpleParam sp: this.Simples.Add( sp ); break;
                        case ArrayParam ap: this.Arrays.Add( ap ); break;
                    }
                }
            }
        }

        public List<ArrayParam> Arrays { get; } = new List<ArrayParam>();
        public List<ComplexParam> Complex { get; } = new List<ComplexParam>();
        public List<SimpleParam> Simples { get; } = new List<SimpleParam>();
    }
}
