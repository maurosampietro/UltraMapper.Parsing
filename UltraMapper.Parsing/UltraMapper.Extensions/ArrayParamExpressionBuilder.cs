using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using UltraMapper.Internals;
using UltraMapper.MappingExpressionBuilders;

namespace UltraMapper.Parsing.Extensions
{
    public class ArrayParamExpressionBuilder : CollectionMapper
    {
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

            Expression iParsedParamToArrayParam = Expression.Convert( context.SourceInstance, typeof( ArrayParam ) );
            Expression items;
            
            Type targetType = target.EntryType;
            if(target.EntryType.IsInterface || target.EntryType.IsAbstract)
                targetType = typeof( List<> ).MakeGenericType( context.TargetInstance.Type.GetCollectionGenericType() );

            Type sourceType;
            if(context.TargetInstance.Type.GetCollectionGenericType().IsBuiltIn( true ))
            {
                sourceType = typeof( List<SimpleParam> );
                items = Expression.Property( iParsedParamToArrayParam, nameof( ArrayParam.Simples ) );
            }
            else
            {
                sourceType = typeof( List<ComplexParam> );
                items = Expression.Property( iParsedParamToArrayParam, nameof( ArrayParam.Complex ) );
            }

            var mapCfg = context.MapperConfiguration[ sourceType, targetType ];
            var mappingExpression = mapCfg.MappingExpression;

            var body = Expression.Block
            (
                Expression.IfThenElse
                (
                    Expression.TypeIs( context.SourceInstance, typeof( SimpleParam ) ),

                    Expression.Assign( context.TargetInstance, Expression.Constant( null, target.EntryType ) ),

                    Expression.Assign( context.TargetInstance,
                        Expression.Invoke( mappingExpression, context.ReferenceTracker, items,
                            Expression.Convert( context.TargetInstance, targetType ) ) )
                ),

                context.TargetInstance
            );

            var delegateType = typeof( UltraMapperDelegate<,> )
                .MakeGenericType( context.SourceInstance.Type, context.TargetInstance.Type );

            return Expression.Lambda( delegateType, body,
                context.ReferenceTracker, context.SourceInstance, context.TargetInstance );
        }
    }
}
