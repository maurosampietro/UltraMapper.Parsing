using System;
using System.Linq;
using System.Linq.Expressions;
using UltraMapper.Internals;
using UltraMapper.MappingExpressionBuilders;

namespace UltraMapper.Parsing.Extensions
{
    public class SimpleParamExpressionBuilder : PrimitiveMapperBase
    {
        public SimpleParamExpressionBuilder( Configuration configuration )
               : base( configuration ) { }

        public override bool CanHandle( Mapping mapping )
        {
            var source = mapping.Source;
            var target = mapping.Target;

            return source.EntryType == typeof( SimpleParam ) 
                && target.EntryType.IsBuiltIn( true );
        }

        protected override Expression GetValueExpression( MapperContext context )
        {
            var paramValueExp = Expression.Property( context.SourceInstance,
                nameof( SimpleParam.Value ) );

            var conversion = MapperConfiguration[ typeof( string ),
                context.TargetInstance.Type ].MappingExpression;

            //first param can be the ReferenceTracker
            var replaceParam = conversion.Parameters
                .First( p => p.Type == paramValueExp.Type );

            return conversion.Body.ReplaceParameter(
                paramValueExp, replaceParam.Name );

            //return Expression.Invoke( conversion, paramValueExp );
        }
    }
}