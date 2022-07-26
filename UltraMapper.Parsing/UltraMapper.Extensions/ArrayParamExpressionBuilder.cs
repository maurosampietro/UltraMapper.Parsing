using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using UltraMapper.Internals;
using UltraMapper.MappingExpressionBuilders;

namespace UltraMapper.Parsing.Extensions
{
    public class ArrayParamExpressionBuilder : ReferenceMapper
    {
        private static Expression<Func<IReadOnlyList<IParsedParam>, IEnumerable<SimpleParam>>>
            CastToSpList => ( source ) => source.Cast<SimpleParam>();

        private static Expression<Func<IReadOnlyList<IParsedParam>, IEnumerable<ComplexParam>>>
            CastToCpList => ( source ) => source.Cast<ComplexParam>();

        public override bool CanHandle( Mapping mapping )
        {
            var source = mapping.Source;
            var target = mapping.Target;

            return (source.ReturnType == typeof( ArrayParam ) &&
                target.ReturnType != typeof( ArrayParam ) &&
                target.ReturnType.IsEnumerable() //icollection would be better
               )
               ||

                (source.EntryType == typeof( IParsedParam ) &&
                target.EntryType.IsEnumerable());
        }

        public override LambdaExpression GetMappingExpression( Mapping mapping )
        {
            var target = mapping.Target;
            var context = this.GetMapperContext( mapping );

            Expression items =
                Expression.Property( context.SourceInstance, nameof( ArrayParam.Items ) );


            Type targetType = target.EntryType;
            //if( target.EntryType.IsInterface || target.EntryType.IsAbstract )
            //    targetType = typeof( List<> ).MakeGenericType( context.TargetCollectionElementType );

            Type sourceType;
            if( context.TargetInstance.Type.GetCollectionGenericType().IsBuiltIn( true ) )
            {
                sourceType = typeof( IEnumerable<SimpleParam> );
                items = Expression.Invoke( CastToSpList, items );
            }
            else
            {
                sourceType = typeof( IEnumerable<ComplexParam> );
                items = Expression.Invoke( CastToCpList, items );
            }

            var mappingExpression = context.MapperConfiguration[ sourceType, targetType ].MappingExpression;

            var body = Expression.IfThenElse
            (
                Expression.TypeIs( context.SourceInstance, typeof( SimpleParam ) ),

                Expression.Assign( context.TargetInstance, Expression.Constant( null, target.EntryType ) ),

                Expression.Invoke( mappingExpression, context.ReferenceTracker, items,
                    Expression.Convert( context.TargetInstance, targetType ) )
            );

            var delegateType = typeof( Action<,,> ).MakeGenericType(
                 context.ReferenceTracker.Type, context.SourceInstance.Type,
                 context.TargetInstance.Type );

            return Expression.Lambda( delegateType, body,
                context.ReferenceTracker, context.SourceInstance, context.TargetInstance );
        }
    }
}
