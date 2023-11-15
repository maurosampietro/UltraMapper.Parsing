using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using UltraMapper.Internals;
using UltraMapper.MappingExpressionBuilders;
using UltraMapper.Parsing.Parameters2;

namespace UltraMapper.Parsing.Extensions
{
    public class ComplexParam2ExpressionBuilder : ReferenceMapper
    {
        private static readonly MethodInfo _compareNameMethod = typeof( IParsedParam )
            .GetMethod( nameof( IParsedParam.CompareName ), BindingFlags.Public | BindingFlags.Instance,
        null, new Type[] { typeof( string ), typeof( StringComparison ) }, null );

        public bool CanMapByIndex { get; set; }

        public override bool CanHandle( Mapping mapping )
        {
            var source = mapping.Source;
            var target = mapping.Target;

            return (source.EntryType == typeof( IParsedParam ) || source.EntryType == typeof( ComplexParam2 )) &&
                target.EntryType != typeof( ComplexParam2 ) //disallow cloning
            && !target.EntryType.IsBuiltIn( true );
        }

        public override LambdaExpression GetMappingExpression( Mapping mapping )
        {
            var context = this.GetMapperContext( mapping );
            var targetMembers = this.SelectTargetMembers( mapping.Target.EntryType );

            Expression[] expressions = new[]
            {
                 LoopAllPropertiesOfType( context, targetMembers, typeof( SimpleParam2 ), nameof( ComplexParam2.Simple ) ),
                 LoopAllPropertiesOfType( context, targetMembers, typeof( ComplexParam2 ), nameof( ComplexParam2.Complex ) ),
                 LoopAllPropertiesOfType( context, targetMembers, typeof( ArrayParam2 ), nameof( ComplexParam2.Array ) )
            };

            var labelTarget = Expression.Label( context.TargetInstance.Type, "returnTarget" );
            var finalExpression = Expression.Block(

                new[] { context.Mapper },

                Expression.Assign( context.Mapper, Expression.Constant( context.MapperInstance ) ),

                Expression.IfThen(
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
            var subParamsAccess = context.SourceInstance.Type == typeof( ComplexParam2 )
                ? Expression.Property( context.SourceInstance, paramName )
                : Expression.Property( Expression.Convert( context.SourceInstance, typeof( ComplexParam2 ) ), paramName );

            var propertiesAssigns = GetMemberAssignments( context, targetMembers, subParam, paramType );

            if(propertiesAssigns.Any())
            {
                return Expression.Block
                (
                    Expression.Assign( context.Mapper, Expression.Constant( context.MapperInstance ) ),
                    ExpressionLoops.ForEach( subParamsAccess, subParam, GetIfElseChain( propertiesAssigns.ToArray() ) ),
                    context.TargetInstance
                );
            }

            return Expression.Empty();
        }

        private IEnumerable<Expression> GetMemberAssignments( ReferenceMapperContext context, MemberInfo[] targetMembers, ParameterExpression subParam, Type paramType )
        {
            if(paramType == typeof( ArrayParam2 ))
                targetMembers = targetMembers.Where( t => !t.GetMemberType().IsBuiltIn( true ) && t.GetMemberType().IsEnumerable() ).ToArray();
            else if(paramType == typeof( ComplexParam2 ))
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
                string memberNameLowerCase = String.IsNullOrWhiteSpace( optionAttribute?.Name )
                    ? targetMemberInfo.Name.ToLower()
                    : optionAttribute.Name.ToLower();

                yield return Expression.IfThen
                (
                    Expression.IsTrue( Expression.Call( subParam, _compareNameMethod, Expression.Constant( memberNameLowerCase ), Expression.Constant( StringComparison.OrdinalIgnoreCase ) ) ),
                    memberExp
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

        private ConditionalExpression GetIfElseChain( Expression[] propertiesAssigns, int i = 0 )
        {
            var item = (ConditionalExpression)propertiesAssigns[ i ];

            if(i == propertiesAssigns.Length - 1)
                return item;

            return Expression.IfThenElse( item.Test, item.IfTrue,
                GetIfElseChain( propertiesAssigns, i + 1 ) );
        }
    }
}