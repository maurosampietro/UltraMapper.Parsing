using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace UltraMapper.Parsing.Parameters2
{
    public sealed class SimpleParam2 : IParsedParam
    {
        private readonly string _data;

        public int Index { get; set; } = 0;

        public int NameStartIndex { get; set; }
        public int NameEndIndex { get; set; }

        public int ValueStartIndex { get; set; }
        public int ValueEndIndex { get; set; }

        public bool ContainsEscapedChars { get; set; } = false;
        public bool ContainsLiteralUnicodeChars { get; set; } = false;

        public SimpleParam2( string data )
        {
            _data = data;
        }

        public string Value
        {
            get
            {
                if(ValueStartIndex == -1) return null;

                var quotation = _data[ ValueStartIndex..ValueEndIndex ];
                if(!ContainsEscapedChars) return quotation;

                if(ContainsLiteralUnicodeChars)
                {
                    StringBuilder sb = new StringBuilder();
                    var unicodeQuotation = quotation.AsSpan();

                    int unicodeCharIndex = unicodeQuotation.IndexOf( @"\u" );
                    while(unicodeCharIndex > -1)
                    {
                        sb.Append( unicodeQuotation[ 0..unicodeCharIndex ] );

                        var unicodeLiteral = unicodeQuotation.Slice( unicodeCharIndex, 6 );
                        int symbolCode = Int32.Parse( unicodeLiteral[ 2.. ], System.Globalization.NumberStyles.HexNumber );
                        var unicodeChar = Char.ConvertFromUtf32( symbolCode );

                        sb.Append( unicodeChar );

                        unicodeQuotation = unicodeQuotation[ (unicodeCharIndex + 6).. ];
                        unicodeCharIndex = unicodeQuotation.IndexOf( @"\u" );
                    }

                    return sb.ToString()
                        .Replace( @"\b", "\b" )
                        .Replace( @"\f", "\f" )
                        .Replace( @"\n", "\n" )
                        .Replace( @"\r", "\r" )
                        .Replace( @"\t", "\t" )
                        .Replace( @"\\", "\\" )
                        .Replace( @"\""", "\"" );
                }

                return quotation.ToString()
                    .Replace( @"\b", "\b" )
                    .Replace( @"\f", "\f" )
                    .Replace( @"\n", "\n" )
                    .Replace( @"\r", "\r" )
                    .Replace( @"\t", "\t" )
                    .Replace( @"\\", "\\" )
                    .Replace( @"\""", "\"" );
            }
        }

        public string Name { get => _data[ NameStartIndex..NameEndIndex ]; set => throw new NotSupportedException(); }

        public bool GetBooleanValue()
        {
            return Boolean.Parse( _data[ ValueStartIndex..ValueEndIndex ] );
        }

        public int GetIntValue( NumberStyles style = NumberStyles.Integer, IFormatProvider provider = null )
        {
            return Int32.Parse( _data.AsSpan()[ ValueStartIndex..ValueEndIndex ] );
            //    int value = 0;

            //    for(int i = ValueStartIndex; i <= ValueEndIndex; i++)
            //        value = checked((value * 10) + ((byte)_data[ i ] - (byte)'0'));

            //    return value;
        }

        public float GetFloatValue( NumberStyles style = NumberStyles.Integer, IFormatProvider provider = null )
        {
            return Single.Parse( _data.AsSpan()[ ValueStartIndex..ValueEndIndex ], style, provider );
        }

        public bool CompareName( string otherName, StringComparison comparison )
        {
            return _data.AsSpan()[ NameStartIndex..NameEndIndex ].Equals( otherName, comparison );
        }
    }

    public sealed class ArrayParam2 : IParsedParam
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

        public int Count => Simple.Count + Complex.Count + Array.Count;
        public string Name { get => _data[ NameStartIndex..NameEndIndex ]; set => throw new NotSupportedException(); }

        public bool CompareName( string otherName, StringComparison comparison )
        {
            return _data.AsSpan()[ NameStartIndex..NameEndIndex ].Equals( otherName, comparison );
        }
    }

    public sealed class ComplexParam2 : IParsedParam
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

        public int Count => Simple.Count + Complex.Count + Array.Count;
        public string Name { get => _data[ NameStartIndex..NameEndIndex ]; set => throw new NotSupportedException(); }

        public bool CompareName( string otherName, StringComparison comparison )
        {
            return _data.AsSpan()[ NameStartIndex..NameEndIndex ].Equals( otherName, comparison );
        }
    }
}
