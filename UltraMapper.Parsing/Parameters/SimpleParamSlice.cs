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

        public override string Value => Text[ ValueStartIndex..ValueEndIndex ];
    }
}
