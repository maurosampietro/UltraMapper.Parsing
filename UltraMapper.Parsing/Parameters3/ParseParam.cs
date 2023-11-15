using System;
using System.Collections.Generic;
using UltraMapper.Parsing.Parameters2;

namespace UltraMapper.Parsing.Parameters3
{
    public sealed class ArrayParam3 : IParsedParam
    {
        private readonly ReadOnlyMemory<char> _data;

        public int Index { get; set; } = 0;

        public int NameStartIndex { get; set; }
        public int NameEndIndex { get; set; }

        public List<SimpleParam2> Simple { get; set; } = new List<SimpleParam2>();
        public List<ComplexParam3> Complex { get; set; } = new List<ComplexParam3>();
        public List<ArrayParam3> Array { get; set; } = new List<ArrayParam3>();

        public ArrayParam3( ReadOnlyMemory<char> data )
        {
            _data = data;
        }

        public int Count => Simple.Count + Complex.Count + Array.Count;
        public string Name { get => _data[ NameStartIndex..NameEndIndex ].ToString(); set => throw new NotSupportedException(); }

        public bool CompareName( string otherName, StringComparison comparison )
        {
            throw new NotImplementedException();
        }
    }

    public sealed class ComplexParam3 : IParsedParam
    {
        private static readonly ReadOnlyMemoryCharComparer _comparer = new();

        private readonly ReadOnlyMemory<char> _data;

        public int Index { get; set; } = 0;

        public int NameStartIndex { get; set; }
        public int NameEndIndex { get; set; }

        public Dictionary<ReadOnlyMemory<char>, SimpleParam2> Simple { get; set; } = new( _comparer );
        public Dictionary<ReadOnlyMemory<char>, ComplexParam3> Complex { get; set; } = new( _comparer );
        public Dictionary<ReadOnlyMemory<char>, ArrayParam3> Array { get; set; } = new( _comparer );

        public ComplexParam3( ReadOnlyMemory<char> data )
        {
            _data = data;
        }

        public int Count => Simple.Count + Complex.Count + Array.Count;
        public string Name { get => _data[ NameStartIndex..NameEndIndex ].ToString(); set => throw new NotSupportedException(); }

        public ComplexParam3 LookupComplexParam( string name )
        {
            Complex.TryGetValue( name.AsMemory(), out var item );
            return item;
        }

        public ArrayParam3 LookupArrayParam( string name )
        {
            Array.TryGetValue( name.AsMemory(), out var item );
            return item;
        }

        public SimpleParam2 LookupSimpleParam( string name )
        {
            Simple.TryGetValue( name.AsMemory(), out var item );
            return item;
        }

        public bool CompareName( string otherName, StringComparison comparison )
        {
            throw new NotImplementedException();
        }
    }

    public sealed class ReadOnlyMemoryCharComparer : IEqualityComparer<ReadOnlyMemory<char>>
    {
        public bool Equals( ReadOnlyMemory<char> x, ReadOnlyMemory<char> y )
        {
            return x.Span.Equals( y.Span, StringComparison.OrdinalIgnoreCase );
        }

        public int GetHashCode( ReadOnlyMemory<char> obj )
        {
            return obj.Span.Length ^ obj.Span[0];
            //// Simple hash code calculation based on the characters in the ReadOnlyMemory<char>
            ////only length and first 3 chars hashed
            //unchecked
            //{
            //    int hash = 17 ^ obj.Span.Length.GetHashCode();
            //    for(int i = 0; i < Math.Min( obj.Span.Length, 3 ); i++)
            //        hash ^= 31 + obj.Span[ i ].GetHashCode();

            //    return hash;
            //}
        }
    }
}
