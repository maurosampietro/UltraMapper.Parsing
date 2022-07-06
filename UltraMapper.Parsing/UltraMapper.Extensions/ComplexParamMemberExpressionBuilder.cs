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
                //{
                //    PropertyInfo sourcemappingtype = null;
                //    Expression sourceValueExp = null;
                //    if( context.SourceInstance.Type == typeof( ComplexParam ) )
                //    {
                //        sourcemappingtype = typeof( ComplexParam ).GetProperty( nameof( ComplexParam.SubParams ) );
                //        sourceValueExp = Expression.Property( Expression.Convert( subParam, typeof( ComplexParam ) ), nameof( ComplexParam.SubParams ) );
                //    }

                //    if( propertyInfo.PropertyType.IsEnumerable() )
                //    {
                //        sourcemappingtype = typeof( ArrayParam ).GetProperty( nameof( ArrayParam.Items ) );
                //        sourceValueExp = Expression.Property( Expression.Convert( subParam, typeof( ArrayParam ) ), nameof( ArrayParam.Items ) );
                //    }

                //    var mappingSource2 = new MappingSource( sourcemappingtype );
                //    var mappingTarget2 = new MappingTarget( targetMemberInfo );

                //    var targetProperty = Expression.Property( context.TargetInstance, targetMemberInfo.Name );

                //    var guessedSourceType = typeof( ComplexParam );
                //    if( targetProperty.Type.IsBuiltIn( true ) )
                //        guessedSourceType = typeof( SimpleParam );
                //    else if( targetProperty.Type.IsEnumerable() )
                //        guessedSourceType = typeof( ArrayParam );

                //    var targetType = targetProperty.Type;
                //    if( targetProperty.Type.IsInterface || targetProperty.Type.IsAbstract )
                //        targetType = typeof( List<> ).MakeGenericType( targetType.GetGenericArguments() );


                //    var typeMapping = new TypeMapping( MapperConfiguration, guessedSourceType, propertyInfo.PropertyType );
                //    var membermapping = new MemberMapping( typeMapping, mappingSource2, mappingTarget2 );
                //    var membermappingcontext = new MemberMappingContext( membermapping );

                //    var mapping2 = MapperConfiguration[ guessedSourceType, targetProperty.Type ];

                //    var memberAssignment = ((ReferenceMapper)mapping2.Mapper)
                //        .GetMemberAssignment( membermappingcontext, out bool needtrackingorrecursion )
                //        .ReplaceParameter( membermappingcontext.SourceMember, "sourceValue" )
                //        .ReplaceParameter( targetProperty, "targetValue" )
                //        .ReplaceParameter( context.TargetInstance, "instance" );


                //    if( MapperConfiguration.IsReferenceTrackingEnabled )
                //    {
                //        var tempReturn = ReferenceTrackingExpression.GetMappingExpression(
                //            context.ReferenceTracker,
                //            subParam, targetProperty,
                //            memberAssignment, context.Mapper, _mapper,
                //             Expression.Constant( mapping2, typeof( IMapping ) ) );

                //        return tempReturn;
                //    }
                //    else
                //    {
                //        var mapMethod = ReferenceMapperContext.RecursiveMapMethodInfo
                //            .MakeGenericMethod( subParam.Type, targetProperty.Type );

                //        var tempReturn = Expression.Block
                //        (
                //            new[] { membermappingcontext.SourceMember},

                //            Expression.Assign( membermappingcontext.SourceMember, sourceValueExp ),

                //            memberAssignment,

                //            //Expression.Invoke(mapping2.MappingExpression, context.ReferenceTracker,
                //            //    Expression.Convert(subParam,guessedSourceType),targetProperty)

                //            //slower but more reliable resolving abstract types/interfaces etc..
                //            Expression.Call( context.Mapper, mapMethod, subParam,
                //                targetProperty, context.ReferenceTracker,
                //                Expression.Constant( mapping2, typeof( IMapping ) ) )
                //        );

                //        return tempReturn;
                //    }
                //}

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
