﻿using System.Linq;
using System.Linq.Expressions;
using UltraMapper.Internals;
using UltraMapper.MappingExpressionBuilders;
using UltraMapper.Parsing.Parameters2;

namespace UltraMapper.Parsing.Extensions
{
    public class SimpleParam2ExpressionBuilder : PrimitiveMapperBase
    {
        public override bool CanHandle( Mapping mapping )
        {
            var source = mapping.Source;
            var target = mapping.Target;

            return source.EntryType == typeof( SimpleParam2 )
                && target.EntryType.IsBuiltIn( true );
        }

        protected override Expression GetValueExpression( MapperContext context )
        {
            var getParamValue = Expression.Property( context.SourceInstance,
                nameof( SimpleParam2.Value ) );

            var conversion = context.MapperConfiguration[ typeof( SimpleParam2 ),
                context.TargetInstance.Type ].MappingExpression;

            //first param can be the ReferenceTracker
            var replaceParam = conversion.Parameters
                .First( p => p.Type == getParamValue.Type );

            var exp = conversion.Body.ReplaceParameter(
                getParamValue, replaceParam.Name );

            if(context.TargetInstance.Type.IsNullable())
            {
                var labelTarget = Expression.Label( context.TargetInstance.Type, "returnTarget" );
                exp = Expression.Block
                (
                    Expression.IfThen
                    (
                            //Expression.Or(
                            Expression.Equal( context.SourceInstance, Expression.Constant( null, context.SourceInstance.Type ) ),
                        //Expression.Equal( getParamValue, Expression.Constant( null, typeof( string ) ) )
                        //),
                        Expression.Return( labelTarget, Expression.Default( context.TargetInstance.Type ) )
                    ),

                    Expression.Label( labelTarget, exp )
                );
            }

            return exp;
        }
    }
}