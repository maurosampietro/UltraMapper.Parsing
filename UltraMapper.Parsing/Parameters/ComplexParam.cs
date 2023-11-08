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
                    if(item is ComplexParam cp2)
                        this.Complex.Add( cp2 );
                    else if(item is SimpleParam sp)
                        this.Simples.Add( sp );
                    else if(item is ArrayParam ap)
                        this.Arrays.Add( ap );
                }
            }
        }

        public IParsedParam this[ int index ]
            => this.SubParams[ index ];

        //in case of mapping to multidimensional arrays this is needed
        public int Count => Arrays.Count + Complex.Count + Simples.Count;

        public List<ArrayParam> Arrays { get; } = new List<ArrayParam>();
        public List<ComplexParam> Complex { get; } = new List<ComplexParam>();
        public List<SimpleParam> Simples { get; } = new List<SimpleParam>();
    }
}
