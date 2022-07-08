using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using UltraMapper.Internals;
using UltraMapper.MappingExpressionBuilders;

namespace UltraMapper.Parsing.Extensions
{
    public class ComplexParamExpressionBuilder : ReferenceMapper
    {
        public bool CanMapByIndex { get; set; }

        public ComplexParamExpressionBuilder( Configuration configuration )
            : base( configuration ) { }

        public override bool CanHandle( Type source, Type target )
        {
            return source == typeof( ComplexParam ) &&
                target != typeof( ComplexParam ); //disallow cloning
        }

        public override LambdaExpression GetMappingExpression( Type source, Type target, IMappingOptions options )
        {
            //IGNORING FIELDS ON PURPOSE SINCE SUPPORTING FIELDS WILL FORCE 
            //THE USER TO ALWAYS SET OPTIONATTRIBUTE.ORDER IN ORDER TO MAP BY INDEX RELIABLY
            //(REFLECTION DO NOT GUARANTEE PROPERTIES AND FIELDS TO BE RETURNED IN DECLARED ORDER
            //IN PARTICULAR IF FIELDS AND PROPERTIES DECLARATION ORDER IS MIXED 
            //(eg: property, field, property, field...)

            //if( target.IsValueType && !target.IsNullable() )
            //    throw new ArgumentException( $"Value types are not supported. {target.GetPrettifiedName()} is a value type." );

            var context = this.GetMapperContext( source, target, options );
            var targetMembers = this.SelectTargetMembers( target );

            var subParam = Expression.Parameter( typeof( IParsedParam ), "paramLoopVar" );
            var subParamsAccess = Expression.Property( context.SourceInstance, nameof( ComplexParam.SubParams ) );

            var paramNameLowerCase = Expression.Parameter( typeof( string ), "paramName" );
            var paramNameExp = Expression.Property( subParam, nameof( IParsedParam.Name ) );
            var paramNameToLower = Expression.Call( paramNameExp, nameof( String.ToLower ), null, null );

            var propertiesAssigns = GetMemberAssignments( context, targetMembers,
                subParam, paramNameLowerCase ).ToArray();

            Expression paramNameDispatch = null;
            if( this.CanMapByIndex )
            {
                var paramNameIfElseChain = GetIfElseChain( propertiesAssigns );
                paramNameDispatch = paramNameIfElseChain;
            }
            else
            {
                var paramNameSwitch = GetSwitch( propertiesAssigns, paramNameLowerCase );
                paramNameDispatch = paramNameSwitch;
            }

            var expression = !propertiesAssigns.Any() ? (Expression)Expression.Empty() : Expression.Block
            (
                new[] { context.Mapper, subParam, paramNameLowerCase },

                Expression.Assign( context.Mapper, Expression.Constant( _mapper ) ),

                ExpressionLoops.ForEach( subParamsAccess, subParam, Expression.Block
                (
                    Expression.Assign( paramNameLowerCase, paramNameToLower ),
                    paramNameDispatch
                ) )
            )
            .ReplaceParameter( context.SourceInstance, "sourceInstance" )
            .ReplaceParameter( context.TargetInstance, "targetInstance" );

            var delegateType = typeof( Action<,,> ).MakeGenericType(
                 context.ReferenceTracker.Type, context.SourceInstance.Type,
                 context.TargetInstance.Type );

            return Expression.Lambda( delegateType, expression,
                context.ReferenceTracker, context.SourceInstance, context.TargetInstance );
        }

        private IEnumerable<Expression> GetMemberAssignments( ReferenceMapperContext context,
            MemberInfo[] targetMembers, ParameterExpression subParam,
            ParameterExpression paramNameLowerCase )
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
                var mappingSource = new MappingSource<IParsedParam, string>( s => ((SimpleParam)s).Value );
                var mappingTarget = new MappingTarget( targetMemberInfo );

                var typeMapping = MapperConfiguration[ typeof( IParsedParam ), context.TargetInstance.Type ];
                typeMapping.AddMemberToMemberMapping( mappingSource, mappingTarget );

                var memberMapping = typeMapping.MemberToMemberMappings[ mappingTarget ];

                //var temp = typeMapping.MappingExpression.Body
                //    .ReplaceParameter( context.TargetInstance, "instance" )
                //    .ReplaceParameter( subParam, "sourceInstance" );

                var exp2 = base.GetSimpleMemberExpression( memberMapping )
                    .ReplaceParameter( context.TargetInstance, "instance" )
                    .ReplaceParameter( subParam, "sourceInstance" );

                return exp2;
            }
            else
            {
                var typeMapping = MapperConfiguration[ typeof( IParsedParam ), context.TargetInstance.Type ];


                IMappingSource mappingSource = null;

                if( context.SourceInstance.Type == typeof( ComplexParam ) )
                {
                    mappingSource = new MappingSource<IParsedParam, ComplexParam>( s => (ComplexParam)s );
                }

                if( propertyInfo.PropertyType.IsEnumerable() )
                {
                    mappingSource = new MappingSource<IParsedParam, IReadOnlyList<IParsedParam>>( s => ((ArrayParam)s).Items );

                    //var temp = MapperConfiguration[ typeof( ArrayParam ), targetMemberInfo.GetMemberType() ];
                    //var exp2 =
                    //    temp.MappingExpression;

                    //exp2.ReplaceParameter( context.TargetInstance, "instance" )
                    //        .ReplaceParameter( subParam, "sourceInstance" )
                    //        .ReplaceParameter( context.Mapper, "mapper" )
                    //        .ReplaceParameter( context.ReferenceTracker, "referenceTracker" );

                    //return exp2;
                }

                var mappingTarget = new MappingTarget( targetMemberInfo );
                var memberMapping = typeMapping.AddMemberToMemberMapping( mappingSource, mappingTarget );

                memberMapping.Decoration = memberExpression => Expression.IfThenElse
                (
                    Expression.TypeIs( subParam, typeof( SimpleParam ) ),
                    Expression.Assign( Expression.Property( context.TargetInstance, propertyInfo ), Expression.Constant( null, propertyInfo.PropertyType ) ),
                    memberExpression
                );

                var exp = base.GetComplexMemberExpression( memberMapping )
                    .ReplaceParameter( context.TargetInstance, "instance" )
                    .ReplaceParameter( subParam, "sourceInstance" )
                    .ReplaceParameter( context.Mapper, "mapper" )
                    .ReplaceParameter( context.ReferenceTracker, "referenceTracker" );

                return Expression.IfThenElse
                (
                    Expression.TypeIs( subParam, typeof( SimpleParam ) ),

                    Expression.Assign( Expression.Property( context.TargetInstance, propertyInfo ), Expression.Constant( null, propertyInfo.PropertyType ) ),

                    exp
                );
            }
        }

        protected MemberInfo[] SelectTargetMembers( Type targetType )
        {
            return targetType.GetProperties() //methods only supported at top level (in ParsedCommand)
                .Where( m => m.GetSetMethod() != null ) //must be assignable
                .Where( m => m.GetIndexParameters().Length == 0 )//indexer not supported
                .Select( ( m, index ) => new
                {
                    Member = m,
                    Options = m.GetCustomAttribute<OptionAttribute>() ??
                        new OptionAttribute() {/*Order = index*/ }
                } )
                .Where( m => !m.Options.IsIgnored )
                .OrderByDescending( info => info.Options.IsRequired )
                .ThenBy( info => info.Options.Order )
                .Select( m => m.Member )
                .ToArray();
        }

        private Expression GetSwitch( Expression[] propertiesAssigns, ParameterExpression paramNameLowerCase )
        {
            IEnumerable<SwitchCase> getSwitchCases()
            {
                foreach( ConditionalExpression item in propertiesAssigns )
                {
                    var caseTest = ((BinaryExpression)item.Test).Left;

                    var caseBody = item.IfTrue;
                    if( caseBody.Type != typeof( void ) )
                        caseBody = Expression.Block( typeof( void ), caseBody );

                    yield return Expression.SwitchCase( caseBody, caseTest );
                }
            }

            var switches = getSwitchCases().ToArray();
            return Expression.Switch( paramNameLowerCase, switches );
        }

        private ConditionalExpression GetIfElseChain( Expression[] propertiesAssigns, int i = 0 )
        {
            var item = (ConditionalExpression)propertiesAssigns[ i ];

            if( i == propertiesAssigns.Length - 1 )
                return item;

            return Expression.IfThenElse( item.Test, item.IfTrue,
                GetIfElseChain( propertiesAssigns, i + 1 ) );
        }
    }
}
