using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using UltraMapper.Internals;
using UltraMapper.MappingExpressionBuilders;

namespace UltraMapper.Parsing.Extensions
{
    public class ArrayParamExpressionBuilder : ArrayMapper
    {
        public ArrayParamExpressionBuilder( Configuration configuration )
            : base( configuration ) { }

        public override bool CanHandle( Type source, Type target )
        {
            return source == typeof( ArrayParam ) && 
                target != typeof( ArrayParam );
        }

        public override LambdaExpression GetMappingExpression( Type source, Type target, IMappingOptions options )
        {
            var context = (CollectionMapperContext)this.GetMapperContext( source, target, options );

            Expression items = context.SourceInstance;
            if( source == typeof( ArrayParam ) )
                items = Expression.Property( context.SourceInstance, nameof( ArrayParam.Items ) );

            Type targetType = target;
            if( target.IsInterface || target.IsAbstract )
                targetType = typeof( List<> ).MakeGenericType( context.TargetCollectionElementType );

            LambdaExpression mappingExpression = null;
            Expression body = null;

            //if( context.TargetCollectionElementType.IsBuiltIn( true ) )
            //{
            //    mappingExpression = MapperConfiguration[ typeof( IEnumerable<SimpleParam> ), targetType ].MappingExpression;
            //    body = Expression.Invoke( mappingExpression, context.ReferenceTracker, Expression.Invoke( extractValues, items ),
            //            Expression.Convert( context.TargetInstance, targetType ) );
            //}
            //else
            {
                mappingExpression = MapperConfiguration[ typeof( IEnumerable<IParsedParam> ), targetType ].MappingExpression;
                body = Expression.Invoke( mappingExpression, context.ReferenceTracker, items,
                       Expression.Convert( context.TargetInstance, targetType ) );
            }

            body = Expression.IfThenElse
            (
                Expression.TypeIs( context.SourceInstance, typeof( SimpleParam ) ),

                Expression.Assign( context.TargetInstance, Expression.Constant( null, target ) ),

                body
            );

            var delegateType = typeof( Action<,,> ).MakeGenericType(
                 context.ReferenceTracker.Type, context.SourceInstance.Type,
                 context.TargetInstance.Type );

            return Expression.Lambda( delegateType, body,
                context.ReferenceTracker, context.SourceInstance, context.TargetInstance );
        }
    }
}
