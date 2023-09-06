namespace UltraMapper.Parsing
{
    public class SimpleParamSlice : SimpleParam
    {
        public int ValueStartIndex { get; set; }
        public int ValueEndIndex { get; set; }
        public readonly string Text;

        public SimpleParamSlice( string text )
        {
            Text = text;
        }

#if NET5_0_OR_GREATER
        public override string Value => Text[ ValueStartIndex..ValueEndIndex ];
#else
        public override string Value => Text.Substring( ValueStartIndex, ValueEndIndex - ValueStartIndex );
#endif
    }
}
