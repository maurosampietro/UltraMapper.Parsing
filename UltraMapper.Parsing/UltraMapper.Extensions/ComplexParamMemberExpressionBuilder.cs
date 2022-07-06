using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using UltraMapper.Internals;
using UltraMapper.MappingExpressionBuilders;
using UltraMapper.MappingExpressionBuilders.MapperContexts;
using UltraMapper.ReferenceTracking;

namespace UltraMapper.Parsing.Extensions
{
    public class ComplexParamMemberExpressionBuilder : ReferenceMapper
    {
        public bool CanMapByIndex { get; set; }

        public ComplexParamMemberExpressionBuilder( Configuration configuration )
            : base( configuration ) { }

        public IEnumerable<Expression> GetMemberAssignments( ReferenceMapperContext context,
            MemberInfo[] targetMembers, ParameterExpression subParam,
            Configuration MapperConfiguration, ParameterExpression paramNameLowerCase )
        {
            for( int i = 0; i < targetMembers.Length; i++ )
            {
                var memberInfo = targetMembers[ i ];
                var assignment = GetMemberAssignment( context, subParam, memberInfo, MapperConfiguration );

                var optionAttribute = memberInfo.GetCustomAttribute<OptionAttribute>();
                string memberNameLowerCase = String.IsNullOrWhiteSpace( optionAttribute?.Name ) ?
                    memberInfo.Name.ToLower() : optionAttribute.Name.ToLower();

                if( this.CanMapByIndex )
                {
                    yield return Expression.IfThen
                    (
                        Expression.OrElse
                        (
                            //we check param name and index
                            Expression.Equal( Expression.Constant( memberNameLowerCase ), paramNameLowerCase ),

                            Expression.AndAlso
                            (
                                Expression.Equal( paramNameLowerCase, Expression.Constant( String.Empty ) ),
                                Expression.Equal( Expression.Constant( i ),
                                    Expression.Property( subParam, nameof( IParsedParam.Index ) ) )
                            )
                        ),

                        assignment
                    );
                }
                else //can map only by name
                {
                    yield return Expression.IfThen
                    (
                        //we check param name and index
                        Expression.Equal( Expression.Constant( memberNameLowerCase ), paramNameLowerCase ),
                        assignment
                    );
                }

                //if( memberInfo is PropertyInfo pi && pi.PropertyType == typeof( bool ) && implicitbool )
                //{
                //    var subParamsAccess = Expression.Property( context.SourceInstance, nameof( ParsedCommand.Param ) );
                //    var setter = memberInfo.GetSetterLambdaExpression();

                //    yield return Expression.IfThenElse
                //    (
                //        Expression.Equal( subParamsAccess, Expression.Constant( null, typeof( IParsedParam ) ) ),
                //        Expression.Invoke( setter, context.TargetInstance, Expression.Constant( true ) ),
                //        standardChecks
                //    );
                //}                    
            }
        }

        private Expression GetMemberAssignment( ReferenceMapperContext context, ParameterExpression subParam,
            MemberInfo targetMemberInfo, Configuration MapperConfiguration )
        {
            var propertyInfo = (PropertyInfo)targetMemberInfo;

            if( propertyInfo.PropertyType.IsBuiltIn( true ) )
            {
                var typeMap = new TypeMapping( MapperConfiguration, typeof( SimpleParam ), targetMemberInfo.GetType() );

                var mappingSource = new MappingSource<IParsedParam, string>( s => ((SimpleParam)s).Value );
                var memberMapping = new MemberMapping( typeMap, mappingSource, new MappingTarget( targetMemberInfo ) );

                return base.GetSimpleMemberExpression( memberMapping )
                    .ReplaceParameter( context.TargetInstance, "instance" )
                    .ReplaceParameter( subParam, "sourceInstance" );
            }
            else
            {
                TypeMapping typeMap = null;
                IMappingSource mappingSource = null;

                if( context.SourceInstance.Type == typeof( ComplexParam ) )
                {
                    typeMap = new TypeMapping( MapperConfiguration, typeof( ComplexParam ), targetMemberInfo.GetMemberType() );
                    mappingSource = new MappingSource<IParsedParam, ComplexParam>( s => (ComplexParam)s );
                }

                if( propertyInfo.PropertyType.IsEnumerable() )
                {
                    typeMap = new TypeMapping( MapperConfiguration, typeof( ArrayParam ), targetMemberInfo.GetMemberType() );
                    mappingSource = new MappingSource<IParsedParam, IReadOnlyList<IParsedParam>>( s => ((ArrayParam)s).Items );
                }

                var mappingTarget = new MappingTarget( targetMemberInfo );
                var memberMapping = new MemberMapping( typeMap, mappingSource, mappingTarget );

                return base.GetComplexMemberExpression( memberMapping )
                    .ReplaceParameter( context.TargetInstance, "instance" )
                    .ReplaceParameter( subParam, "sourceInstance" )
                    .ReplaceParameter( context.Mapper, "mapper" )
                    .ReplaceParameter( context.ReferenceTracker, "referenceTracker" );
            }
        }
    }
}
