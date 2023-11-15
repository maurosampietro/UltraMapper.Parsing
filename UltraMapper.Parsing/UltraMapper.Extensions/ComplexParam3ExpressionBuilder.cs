using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using UltraMapper.Internals;
using UltraMapper.MappingExpressionBuilders;
using UltraMapper.Parsing.Parameters2;
using UltraMapper.Parsing.Parameters3;

namespace UltraMapper.Parsing.Extensions
{
    public class ComplexParam3ExpressionBuilder : ReferenceMapper
    {
        public bool CanMapByIndex { get; set; }

        public override bool CanHandle( Mapping mapping )
        {
            var source = mapping.Source;
            var target = mapping.Target;

            return (source.EntryType == typeof( IParsedParam ) || source.EntryType == typeof( ComplexParam3 )) &&
                target.EntryType != typeof( ComplexParam3 ) //disallow cloning
            && !target.EntryType.IsBuiltIn( true );
        }

        public override LambdaExpression GetMappingExpression( Mapping mapping )
        {
            var context = this.GetMapperContext( mapping );
            var targetMembers = this.SelectTargetMembers( mapping.Target.EntryType );

            Expression[] expressions = new[]
            {
                 LoopAllPropertiesOfType( context, targetMembers, typeof( SimpleParam2 ), nameof( ComplexParam3.Simple ) ),
                 LoopAllPropertiesOfType( context, targetMembers, typeof( ComplexParam3 ), nameof( ComplexParam3.Complex ) ),
                 LoopAllPropertiesOfType( context, targetMembers, typeof( ArrayParam3 ), nameof( ComplexParam3.Array ) )
            };

            var labelTarget = Expression.Label( context.TargetInstance.Type, "returnTarget" );
            var finalExpression = Expression.Block
            (
                new[] { context.Mapper },

                Expression.Assign( context.Mapper, Expression.Constant( context.MapperInstance ) ),

                Expression.IfThen
                (
                    Expression.Equal( context.SourceInstance, Expression.Constant( null, context.SourceInstance.Type ) ),
                    Expression.Return( labelTarget, Expression.Constant( null, context.TargetInstance.Type ) )
                ),

                Expression.Block( expressions ),

                Expression.Label( labelTarget, context.TargetInstance )
            );

            var delegateType = typeof( UltraMapperDelegate<,> )
                .MakeGenericType( context.SourceInstance.Type, context.TargetInstance.Type );

            return Expression.Lambda( delegateType, finalExpression,
                context.ReferenceTracker, context.SourceInstance, context.TargetInstance );
        }

        private Expression LoopAllPropertiesOfType( ReferenceMapperContext context, MemberInfo[] targetMembers, Type paramType, string paramName )
        {
            var subParam = Expression.Parameter( paramType, "loopVar" );
            var propertiesAssigns = GetMemberAssignments( context, targetMembers, subParam, paramType );

            if(propertiesAssigns.Any())
            {
                return Expression.Block
                (
                    new[] { subParam },                    
                    Expression.Block( propertiesAssigns.ToArray() ),
                    context.TargetInstance
                );
            }

            return Expression.Empty();
        }

        private IEnumerable<Expression> GetMemberAssignments( ReferenceMapperContext context, MemberInfo[] targetMembers,
            ParameterExpression subParam, Type paramType )
        {
            if(paramType == typeof( ArrayParam3 ))
                targetMembers = targetMembers.Where( t => !t.GetMemberType().IsBuiltIn( true ) && t.GetMemberType().IsEnumerable() ).ToArray();
            else if(paramType == typeof( ComplexParam3 ))
                targetMembers = targetMembers.Where( t => !t.GetMemberType().IsBuiltIn( true ) && !t.GetMemberType().IsEnumerable() ).ToArray();
            else
                targetMembers = targetMembers.Where( t => t.GetMemberType().IsBuiltIn( true ) ).ToArray();

            foreach(var targetMemberInfo in targetMembers)
            {
                var targetMemberType = targetMemberInfo.GetMemberType();
                var mc = context.MapperConfiguration[ paramType, targetMemberType ];

                var mappingSource = mc.Source;
                var mappingTarget = new MappingTarget( targetMemberInfo );
                var memberMapping = mc.AddMemberToMemberMapping( mappingSource, mappingTarget );

                var memberExp = memberMapping.MemberMappingExpression.Body
                    .ReplaceParameter( context.TargetInstance, "instance" )
                    .ReplaceParameter( context.TargetInstance, "targetInstance" )
                    .ReplaceParameter( subParam, "sourceInstance" )
                    .ReplaceParameter( context.Mapper, "mapper" )
                    .ReplaceParameter( context.ReferenceTracker, "referenceTracker" );

                var optionAttribute = targetMemberInfo.GetCustomAttribute<OptionAttribute>();
                string memberName = String.IsNullOrWhiteSpace( optionAttribute?.Name )
                    ? targetMemberInfo.Name : optionAttribute.Name;

                string methodName = "LookupSimpleParam";
                if(paramType == typeof( ArrayParam3 ))
                    methodName = "LookupArrayParam";
                else if(paramType == typeof( ComplexParam3 ))
                    methodName = "LookupComplexParam";

                var mi = typeof( ComplexParam3 ).GetMethod( methodName, new Type[] { typeof( string ) } );

                yield return Expression.Block
                (
                    Expression.Assign( subParam, Expression.Call( context.SourceInstance, mi, Expression.Constant( memberName ) ) ),
                    Expression.IfThen
                    ( 
                        Expression.IsFalse( Expression.Equal( subParam, Expression.Constant(null, subParam.Type) ) ), 
                        memberExp 
                    )
                );
            }
        }

        protected MemberInfo[] SelectTargetMembers( Type targetType )
        {
            return targetType.GetProperties()
                .Where( m => m.GetSetMethod() != null && m.GetIndexParameters().Length == 0 )
                .Select( ( m, index ) => new
                {
                    Member = m,
                    Options = m.GetCustomAttribute<OptionAttribute>() ?? new OptionAttribute()
                } )
                .Where( m => !m.Options.IsIgnored )
                .OrderByDescending( info => info.Options.IsRequired )
                .ThenBy( info => info.Options.Order )
                .Select( m => m.Member )
                .ToArray();
        }
    }
}