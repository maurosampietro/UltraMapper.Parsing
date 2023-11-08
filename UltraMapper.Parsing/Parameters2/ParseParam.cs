using System;
using System.Collections.Generic;

namespace UltraMapper.Parsing.Parameters2
{
    public interface IParsedParam2 { }

    public class SimpleParam2 : IParsedParam2
    {
        private readonly string _data;

        public int Index { get; set; } = 0;

        public int NameStartIndex { get; set; }
        public int NameEndIndex { get; set; }

        public int ValueStartIndex { get; set; }
        public int ValueEndIndex { get; set; }

        public SimpleParam2( string data )
        {
            _data = data;
        }

        public string Value => _data[ ValueStartIndex..ValueEndIndex ];

        //public int GetInt( NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null )
        //{
        //    return Int32.Parse( _data.AsSpan()[ ValueStartIndex..ValueEndIndex ], style, provider );
        //}
    }

    public class ArrayParam2 : IParsedParam2
    {
        private readonly string _data;

        public int Index { get; set; } = 0;

        public int NameStartIndex { get; set; }
        public int NameEndIndex { get; set; }

        public List<SimpleParam2> Simple { get; set; } = new List<SimpleParam2>();
        public List<ComplexParam2> Complex { get; set; } = new List<ComplexParam2>();
        public List<ArrayParam2> Array { get; set; } = new List<ArrayParam2>();

        public ArrayParam2( string data )
        {
            _data = data;
        }
    }

    public class ComplexParam2 : IParsedParam2
    {
        private readonly string _data;

        public int Index { get; set; } = 0;

        public int NameStartIndex { get; set; }
        public int NameEndIndex { get; set; }

        public List<SimpleParam2> Simple { get; set; } = new List<SimpleParam2>();
        public List<ComplexParam2> Complex { get; set; } = new List<ComplexParam2>();
        public List<ArrayParam2> Array { get; set; } = new List<ArrayParam2>();

        public ComplexParam2( string data )
        {
            _data = data;
        }
    }
}
