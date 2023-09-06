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

        public override bool CanHandle( Mapping mapping )
        {
            var source = mapping.Source;
            var target = mapping.Target;

            return source.EntryType == typeof( ComplexParam ) &&
                target.EntryType != typeof( ComplexParam ); //disallow cloning
        }

        public override LambdaExpression GetMappingExpression( Mapping mapping )
        {
            var source = mapping.Source;
            var target = mapping.Target;

            //IGNORING FIELDS ON PURPOSE SINCE SUPPORTING FIELDS WILL FORCE 
            //THE USER TO ALWAYS SET OPTIONATTRIBUTE.ORDER IN ORDER TO MAP BY INDEX RELIABLY
            //(REFLECTION DO NOT GUARANTEE PROPERTIES AND FIELDS TO BE RETURNED IN DECLARED ORDER
            //IN PARTICULAR IF FIELDS AND PROPERTIES DECLARATION ORDER IS MIXED 
            //(eg: property, field, property, field...)

            //if( target.IsValueType && !target.IsNullable() )
            //    throw new ArgumentException( $"Value types are not supported. {target.GetPrettifiedName()} is a value type." );

            var context = this.GetMapperContext( mapping );
            var targetMembers = this.SelectTargetMembers( target.EntryType );

            var subParam = Expression.Parameter( typeof( IParsedParam ), "paramLoopVar" );
            var subParamsAccess = Expression.Property( context.SourceInstance, nameof( ComplexParam.SubParams ) );

            var paramNameLowerCase = Expression.Parameter( typeof( string ), "paramName" );
            var paramNameExp = Expression.Property( subParam, nameof( IParsedParam.Name ) );
            var paramNameToLower = Expression.Call( paramNameExp, nameof( String.ToLower ), null, null );

            var propertiesAssigns = GetMemberAssignments( context, targetMembers,
                subParam, paramNameLowerCase ).ToArray();

            Expression paramNameDispatch = null;
            if(this.CanMapByIndex)
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

                Expression.Assign( context.Mapper, Expression.Constant( context.MapperInstance ) ),

                ExpressionLoops.ForEach( subParamsAccess, subParam, Expression.Block
                (
                    Expression.Assign( paramNameLowerCase, paramNameToLower ),
                    paramNameDispatch
                ) ),

                context.TargetInstance
            )
            .ReplaceParameter( context.Mapper, context.Mapper.Name )
            .ReplaceParameter( context.SourceInstance, context.SourceInstance.Name )
            .ReplaceParameter( context.TargetInstance, context.TargetInstance.Name );

            var labelTarget = Expression.Label( context.TargetInstance.Type, "returnTarget" );
            expression = Expression.Block
            (
                Expression.IfThen
                (
                        //Expression.Or(
                        Expression.Equal( context.SourceInstance, Expression.Constant( null, context.SourceInstance.Type ) ),
                    //Expression.Equal( getParamValue, Expression.Constant( null, typeof( string ) ) )
                    //),
                    Expression.Return( labelTarget, Expression.Constant( null, context.TargetInstance.Type ) )
                ),

                Expression.Label( labelTarget, expression )
            );

            var delegateType = typeof( UltraMapperDelegate<,> )
                .MakeGenericType( context.SourceInstance.Type, context.TargetInstance.Type );

            return Expression.Lambda( delegateType, expression,
                context.ReferenceTracker, context.SourceInstance, context.TargetInstance );
        }

        private IEnumerable<Expression> GetMemberAssignments( ReferenceMapperContext context,
            MemberInfo[] targetMembers, ParameterExpression subParam,
            ParameterExpression paramNameLowerCase )
        {
            //var typeMapping = MapperConfiguration[ _cpSource, context.TargetInstance.Type ];
            var typeMapping = new TypeMapping( context.MapperConfiguration, _cpSource, new MappingTarget( context.TargetInstance.Type ) );
            context.MapperConfiguration.TypeMappingTree.Add( typeMapping );
            //typeMapping._mappingSource.Add( _blSource );
            //typeMapping._mappingSource.Add( _spSource );
            //typeMapping._mappingSource.Add( _cpSource );
            //typeMapping._mappingSource.Add( _apSource );

            for(int i = 0; i < targetMembers.Length; i++)
            {
                var targetMemberInfo = targetMembers[ i ];
                var targetMemberType = targetMemberInfo.GetMemberType();

                var mappingSource = GetMappingSource( targetMemberType );
                var mappingTarget = new MappingTarget( targetMemberInfo );
                var memberMapping = typeMapping.AddMemberToMemberMapping( mappingSource, mappingTarget );

                var targetProp = (PropertyInfo)memberMapping.TargetMember.MemberAccessPath.Last();

                var optionAttribute = targetMemberInfo.GetCustomAttribute<OptionAttribute>();
                string memberNameLowerCase = String.IsNullOrWhiteSpace( optionAttribute?.Name ) ?
                    targetProp.Name.ToLower() : optionAttribute.Name.ToLower();

                var memberExp = memberMapping.MemberMappingExpression.Body
                    .ReplaceParameter( context.TargetInstance, "instance" )
                    .ReplaceParameter( subParam, "sourceInstance" )
                    .ReplaceParameter( context.Mapper, "mapper" )
                    .ReplaceParameter( context.ReferenceTracker, "referenceTracker" );

                Expression exp = memberMapping.Target.EntryType.IsBuiltIn( true ) ?
                    memberExp : Expression.IfThenElse
                (
                    Expression.AndAlso
                    (
                        Expression.TypeIs( subParam, typeof( SimpleParam ) ),
                        Expression.Equal
                        (
                            Expression.Property( Expression.Convert( subParam, typeof( SimpleParam ) ), nameof( SimpleParam.Value ) ),
                            Expression.Constant( null, typeof( string ) )
                        )
                    ),

                    Expression.Assign( Expression.Property( context.TargetInstance, targetProp ),
                        Expression.Constant( null, targetProp.PropertyType ) ),

                    memberExp
                );

                if(this.CanMapByIndex)
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

                        exp
                    );
                }
                else //can map only by name
                {
                    yield return Expression.IfThen
                    (
                        //we check param name and index
                        Expression.Equal( Expression.Constant( memberNameLowerCase ), paramNameLowerCase ),
                        exp
                    );
                }
            }
        }

        private static readonly IMappingSource _blSource = new MappingSource<IParsedParam, bool>( s => ((BooleanParam)s).BoolValue );
        private static readonly IMappingSource _spSource = new MappingSource<IParsedParam, string>( s => ((SimpleParam)s).Value );
        private static readonly IMappingSource _cpSource = new MappingSource<IParsedParam, ComplexParam>( s => (ComplexParam)s );
        private static readonly IMappingSource _apSource = new MappingSource<IParsedParam, ArrayParam>( s => (ArrayParam)s );

        private static IMappingSource GetMappingSource( Type targetMemberType )
        {
            if(targetMemberType == typeof( bool )) return _blSource;
            if(targetMemberType.IsBuiltIn( true )) return _spSource;
            if(targetMemberType.IsEnumerable()) return _apSource;
            return _cpSource;
        }

        protected MemberInfo[] SelectTargetMembers( Type targetType )
        {
            return targetType.GetProperties()
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
                foreach(ConditionalExpression item in propertiesAssigns)
                {
                    var caseTest = ((BinaryExpression)item.Test).Left;

                    var caseBody = item.IfTrue;
                    if(caseBody.Type != typeof( void ))
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

            if(i == propertiesAssigns.Length - 1)
                return item;

            return Expression.IfThenElse( item.Test, item.IfTrue,
                GetIfElseChain( propertiesAssigns, i + 1 ) );
        }
    }
}
