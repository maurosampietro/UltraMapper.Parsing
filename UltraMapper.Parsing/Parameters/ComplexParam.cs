using System;
using System.Collections.Generic;
using System.Linq;

namespace UltraMapper.Parsing
{
    public sealed class ComplexParam : IParsedParam
    {
        public string Name { get; set; } = String.Empty;
        public int Index { get; set; } = 0;

        private IList<IParsedParam> _params;
        public IList<IParsedParam> SubParams
        {
            get
            {
                return _params ??= new List<IParsedParam>()
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
                    if(item is ComplexParam cp2)
                        this.Complex.Add( cp2 );
                    else if(item is SimpleParam sp)
                        this.Simple.Add( sp );
                    else if(item is ArrayParam ap)
                        this.Array.Add( ap );
                }
            }
        }

        public IParsedParam this[ int index ]
            => this.SubParams[ index ];

        //in case of mapping to multidimensional arrays this is needed
        public int Count => Array.Count + Complex.Count + Simple.Count;

        public List<ArrayParam> Array { get; } = new List<ArrayParam>();
        public List<ComplexParam> Complex { get; } = new List<ComplexParam>();
        public List<SimpleParam> Simple { get; } = new List<SimpleParam>();

        public bool CompareName( string otherName, StringComparison comparison )
        {
            throw new NotImplementedException();
        }
    }
}
