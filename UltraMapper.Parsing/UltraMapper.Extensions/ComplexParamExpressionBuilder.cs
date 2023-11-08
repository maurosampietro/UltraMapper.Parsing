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
        private static readonly Expression<Func<string, string, bool>> _ordinalCmp =
            ( a, b ) => String.Equals( a, b, StringComparison.OrdinalIgnoreCase );

        private static readonly MethodInfo _ordinalCmpMi = typeof( String ).GetMethod( nameof( String.Equals ),
            BindingFlags.Public | BindingFlags.Instance,
            null, new Type[] { typeof( string ), typeof( StringComparison ) }, null );

        public bool CanMapByIndex { get; set; }

        public override bool CanHandle( Mapping mapping )
        {
            var source = mapping.Source;
            var target = mapping.Target;

            return (source.EntryType == typeof( IParsedParam ) || source.EntryType == typeof( ComplexParam )) &&
                target.EntryType != typeof( ComplexParam ) //disallow cloning
            && !target.EntryType.IsBuiltIn( true );
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

            //Expression expression = LoopAllPropertiesAnyType( context, targetMembers );
            Expression expressionSp = LoopAllPropertiesSimpleParams( context, targetMembers );
            Expression expressionCp = LoopAllPropertiesComplexParams( context, targetMembers );
            Expression expressionAr = LoopAllPropertiesArrayParams( context, targetMembers );

            var labelTarget = Expression.Label( context.TargetInstance.Type, "returnTarget" );
            var finalExpression = Expression.Block
            (
                Expression.IfThen
                (
                        //Expression.Or(
                        Expression.Equal( context.SourceInstance, Expression.Constant( null, context.SourceInstance.Type ) ),
                    //Expression.Equal( getParamValue, Expression.Constant( null, typeof( string ) ) )
                    //),
                    Expression.Return( labelTarget, Expression.Constant( null, context.TargetInstance.Type ) )
                ),

                 Expression.Block(
                /*expression,*/
                expressionSp
                , expressionCp
                , expressionAr
                ),


                Expression.Label( labelTarget, context.TargetInstance )
            );

            var delegateType = typeof( UltraMapperDelegate<,> )
                .MakeGenericType( context.SourceInstance.Type, context.TargetInstance.Type );

            return Expression.Lambda( delegateType, finalExpression,
                context.ReferenceTracker, context.SourceInstance, context.TargetInstance );
        }

        private Expression LoopAllPropertiesAnyType( ReferenceMapperContext context, MemberInfo[] targetMembers )
        {
            //all params types
            var subParam = Expression.Parameter( typeof( IParsedParam ), "paramLoopVar" );

            var subParamsAccess = context.SourceInstance.Type == typeof( ComplexParam ) ?
                Expression.Property( context.SourceInstance, nameof( ComplexParam.SubParams ) ) :
                Expression.Property( Expression.Convert( context.SourceInstance, typeof( ComplexParam ) ), nameof( ComplexParam.SubParams ) );

            var propertiesAssigns = GetMemberAssignments( context, targetMembers,
                 subParam ).ToArray();

            Expression paramNameDispatch = Expression.Empty();
            if(propertiesAssigns.Length > 0)
            {
                if(this.CanMapByIndex)
                {
                    if(propertiesAssigns.Length > 0)
                    {
                        var paramNameIfElseChain = GetIfElseChain( propertiesAssigns );
                        paramNameDispatch = paramNameIfElseChain;
                    }
                }
                else
                {
                    var paramNameSwitch = GetIfElseChain( propertiesAssigns );
                    paramNameDispatch = paramNameSwitch;
                }
            }

            var expression = !propertiesAssigns.Any() ? (Expression)Expression.Empty() : Expression.Block
            (
                new[] { context.Mapper },

                Expression.Assign( context.Mapper, Expression.Constant( context.MapperInstance ) ),
                ExpressionLoops.ForEach( subParamsAccess, subParam, paramNameDispatch ),

                context.TargetInstance
            )
            .ReplaceParameter( context.Mapper, context.Mapper.Name )
            .ReplaceParameter( context.SourceInstance, context.SourceInstance.Name )
            .ReplaceParameter( context.TargetInstance, context.TargetInstance.Name );

            return expression;
        }

        private Expression LoopAllPropertiesArrayParams( ReferenceMapperContext context, MemberInfo[] targetMembers )
        {
            //simple params
            var subParam = Expression.Parameter( typeof( ArrayParam ), "arrayParamLoopVar" );

            var subParamsAccess = context.SourceInstance.Type == typeof( ComplexParam ) ?
                Expression.Property( context.SourceInstance, nameof( ComplexParam.Arrays ) ) :
                Expression.Property( Expression.Convert( context.SourceInstance, typeof( ComplexParam ) ), nameof( ComplexParam.Arrays ) );

            targetMembers = targetMembers.Where( t => !t.GetMemberType().IsBuiltIn( true ) && t.GetMemberType().IsEnumerable() ).ToArray();
            var propertiesAssigns = GetArrayMemberAssignments( context, targetMembers, subParam ).ToArray();

            Expression paramNameDispatch = Expression.Empty();
            if(propertiesAssigns.Length > 0)
            {
                if(this.CanMapByIndex)
                {
                    if(propertiesAssigns.Length > 0)
                    {
                        var paramNameIfElseChain = GetIfElseChain( propertiesAssigns );
                        paramNameDispatch = paramNameIfElseChain;
                    }
                }
                else
                {
                    var paramNameSwitch = GetIfElseChain( propertiesAssigns );
                    paramNameDispatch = paramNameSwitch;
                }
            }

            var expression = !propertiesAssigns.Any() ? (Expression)Expression.Empty() : Expression.Block
            (
                new[] { context.Mapper },

                Expression.Assign( context.Mapper, Expression.Constant( context.MapperInstance ) ),
                ExpressionLoops.ForEach( subParamsAccess, subParam, paramNameDispatch ),

                context.TargetInstance
            )
            .ReplaceParameter( context.Mapper, context.Mapper.Name )
            .ReplaceParameter( context.SourceInstance, context.SourceInstance.Name )
            .ReplaceParameter( context.TargetInstance, context.TargetInstance.Name );

            return expression;
        }

        private Expression LoopAllPropertiesComplexParams( ReferenceMapperContext context, MemberInfo[] targetMembers )
        {
            //simple params
            var subParam = Expression.Parameter( typeof( ComplexParam ), "complexParamLoopVar" );

            var subParamsAccess = context.SourceInstance.Type == typeof( ComplexParam ) ?
                Expression.Property( context.SourceInstance, nameof( ComplexParam.Complex ) ) :
                Expression.Property( Expression.Convert( context.SourceInstance, typeof( ComplexParam ) ), nameof( ComplexParam.Complex ) );

            targetMembers = targetMembers.Where( t => !t.GetMemberType().IsBuiltIn( true ) && !t.GetMemberType().IsEnumerable() ).ToArray();
            var propertiesAssigns = GetComplexMemberAssignments( context, targetMembers, subParam ).ToArray();

            Expression paramNameDispatch = Expression.Empty();
            if(propertiesAssigns.Length > 0)
            {
                if(this.CanMapByIndex)
                {
                    if(propertiesAssigns.Length > 0)
                    {
                        var paramNameIfElseChain = GetIfElseChain( propertiesAssigns );
                        paramNameDispatch = paramNameIfElseChain;
                    }
                }
                else
                {
                    var paramNameSwitch = GetIfElseChain( propertiesAssigns );
                    paramNameDispatch = paramNameSwitch;
                }
            }

            var expression = !propertiesAssigns.Any() ? (Expression)Expression.Empty() : Expression.Block
            (
                new[] { context.Mapper },

                Expression.Assign( context.Mapper, Expression.Constant( context.MapperInstance ) ),
                ExpressionLoops.ForEach( subParamsAccess, subParam, paramNameDispatch ),

                context.TargetInstance
            )
            .ReplaceParameter( context.Mapper, context.Mapper.Name )
            .ReplaceParameter( context.SourceInstance, context.SourceInstance.Name )
            .ReplaceParameter( context.TargetInstance, context.TargetInstance.Name );

            return expression;
        }

        private Expression LoopAllPropertiesSimpleParams( ReferenceMapperContext context, MemberInfo[] targetMembers )
        {
            //simple params
            var subParam = Expression.Parameter( typeof( SimpleParam ), "simpleParamLoopVar" );

            var subParamsAccess = context.SourceInstance.Type == typeof( ComplexParam ) ?
                Expression.Property( context.SourceInstance, nameof( ComplexParam.Simples ) ) :
                Expression.Property( Expression.Convert( context.SourceInstance, typeof( ComplexParam ) ), nameof( ComplexParam.Simples ) );

            var propertiesAssigns = GetSimpleMemberAssignments( context, targetMembers.Where( t => t.GetMemberType().IsBuiltIn( true ) ).ToArray(),
                subParam ).ToArray();

            Expression paramNameDispatch = Expression.Empty();
            if(propertiesAssigns.Length > 0)
            {
                if(this.CanMapByIndex)
                {
                    var paramNameIfElseChain = GetIfElseChain( propertiesAssigns );
                    paramNameDispatch = paramNameIfElseChain;
                }
                else
                {
                    var paramNameSwitch = GetIfElseChain( propertiesAssigns );
                    paramNameDispatch = paramNameSwitch;
                }
            }

            var expression = !propertiesAssigns.Any() ? (Expression)Expression.Empty() : Expression.Block
            (
                new[] { context.Mapper },

                Expression.Assign( context.Mapper, Expression.Constant( context.MapperInstance ) ),
                ExpressionLoops.ForEach( subParamsAccess, subParam, paramNameDispatch ),

                context.TargetInstance
            )
            .ReplaceParameter( context.Mapper, context.Mapper.Name )
            .ReplaceParameter( context.SourceInstance, context.SourceInstance.Name )
            .ReplaceParameter( context.TargetInstance, context.TargetInstance.Name );

            return expression;
        }

        private IEnumerable<Expression> GetArrayMemberAssignments( ReferenceMapperContext context,
        MemberInfo[] targetMembers, ParameterExpression subParam )
        {
            //var typeMapping = MapperConfiguration[ _cpSource, context.TargetInstance.Type ];
            var typeMapping = new TypeMapping( context.MapperConfiguration,
                new MappingSource<ComplexParam, ComplexParam>( xx => xx ), new MappingTarget( context.TargetInstance.Type ) );
            context.MapperConfiguration.TypeMappingTree.Add( typeMapping );
            //typeMapping._mappingSource.Add( _blSource );
            //typeMapping._mappingSource.Add( _spSource );
            //typeMapping._mappingSource.Add( _cpSource );
            //typeMapping._mappingSource.Add( _apSource );

            for(int i = 0; i < targetMembers.Length; i++)
            {
                var targetMemberInfo = targetMembers[ i ];
                var targetMemberType = targetMemberInfo.GetMemberType();

                var mappingSource = new MappingSource<ArrayParam, ArrayParam>( sourceInstance => sourceInstance );
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

                Expression exp = memberExp;
                //Expression exp = memberMapping.Target.EntryType.IsBuiltIn( true ) ?
                //    memberExp : Expression.IfThenElse
                //(
                //    Expression.AndAlso
                //    (
                //        Expression.TypeIs( subParam, typeof( SimpleParam ) ),
                //        Expression.Equal
                //        (
                //            Expression.Property( Expression.Convert( subParam, typeof( SimpleParam ) ), nameof( SimpleParam.Value ) ),
                //            Expression.Constant( null, typeof( string ) )
                //        )
                //    ),

                //    Expression.Assign( Expression.Property( context.TargetInstance, targetProp ),
                //        Expression.Constant( null, targetProp.PropertyType ) ),

                //    memberExp
                //);
                var paramName = Expression.Property( subParam, nameof( IParsedParam.Name ) );
                if(this.CanMapByIndex)
                {
                    yield return Expression.IfThen
                    (
                        Expression.OrElse
                        (
                            //we check param name and index
                            Expression.IsTrue( Expression.Call( Expression.Constant( memberNameLowerCase ), _ordinalCmpMi, paramName, Expression.Constant( StringComparison.OrdinalIgnoreCase ) ) ),

                            Expression.AndAlso
                            (
                                Expression.Equal( paramName, Expression.Constant( String.Empty ) ),
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
                        Expression.IsTrue( Expression.Call( Expression.Constant( memberNameLowerCase ), _ordinalCmpMi, paramName, Expression.Constant( StringComparison.OrdinalIgnoreCase ) ) ),
                        exp
                    );
                }
            }
        }

        private IEnumerable<Expression> GetComplexMemberAssignments( ReferenceMapperContext context,
           MemberInfo[] targetMembers, ParameterExpression subParam )
        {
            //var typeMapping = MapperConfiguration[ _cpSource, context.TargetInstance.Type ];
            var typeMapping = new TypeMapping( context.MapperConfiguration,
                new MappingSource<ComplexParam, ComplexParam>( xx => xx ), new MappingTarget( context.TargetInstance.Type ) );
            context.MapperConfiguration.TypeMappingTree.Add( typeMapping );
            //typeMapping._mappingSource.Add( _blSource );
            //typeMapping._mappingSource.Add( _spSource );
            //typeMapping._mappingSource.Add( _cpSource );
            //typeMapping._mappingSource.Add( _apSource );

            for(int i = 0; i < targetMembers.Length; i++)
            {
                var targetMemberInfo = targetMembers[ i ];
                var targetMemberType = targetMemberInfo.GetMemberType();

                var mappingSource = new MappingSource<ComplexParam, ComplexParam>( sourceInstance => sourceInstance );
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

                Expression exp = memberExp;
                //    Expression.IfThenElse
                //(
                //    Expression.AndAlso
                //    (
                //        Expression.TypeIs( subParam, typeof( SimpleParam ) ),
                //        Expression.Equal
                //        (
                //            Expression.Property( Expression.Convert( subParam, typeof( SimpleParam ) ), nameof( SimpleParam.Value ) ),
                //            Expression.Constant( null, typeof( string ) )
                //        )
                //    ),

                //    Expression.Assign( Expression.Property( context.TargetInstance, targetProp ),
                //        Expression.Constant( null, targetProp.PropertyType ) ),

                //    memberExp
                //);
                var paramName = Expression.Property( subParam, nameof( IParsedParam.Name ) );
                if(this.CanMapByIndex)
                {
                    yield return Expression.IfThen
                    (
                        Expression.OrElse
                        (
                            //we check param name and index
                            Expression.IsTrue( Expression.Call( Expression.Constant( memberNameLowerCase ), _ordinalCmpMi, paramName, Expression.Constant( StringComparison.OrdinalIgnoreCase ) ) ),

                            Expression.AndAlso
                            (
                                Expression.Equal( paramName, Expression.Constant( String.Empty ) ),
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
                        Expression.IsTrue( Expression.Call( Expression.Constant( memberNameLowerCase ), _ordinalCmpMi, paramName, Expression.Constant( StringComparison.OrdinalIgnoreCase ) ) ),
                        exp
                    );
                }
            }
        }

        private IEnumerable<Expression> GetSimpleMemberAssignments( ReferenceMapperContext context,
            MemberInfo[] targetMembers, ParameterExpression subParam )
        {
            //var typeMapping = MapperConfiguration[ _cpSource, context.TargetInstance.Type ];
            var typeMapping = new TypeMapping( context.MapperConfiguration,
                new MappingSource<ComplexParam, ComplexParam>( xx => xx ), new MappingTarget( context.TargetInstance.Type ) );
            context.MapperConfiguration.TypeMappingTree.Add( typeMapping );
            //typeMapping._mappingSource.Add( _blSource );
            //typeMapping._mappingSource.Add( _spSource );
            //typeMapping._mappingSource.Add( _cpSource );
            //typeMapping._mappingSource.Add( _apSource );

            for(int i = 0; i < targetMembers.Length; i++)
            {
                var targetMemberInfo = targetMembers[ i ];
                var targetMemberType = targetMemberInfo.GetMemberType();

                var mappingSource = new MappingSource<SimpleParam, string>( sourceInstance => sourceInstance.Value );
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

                var paramName = Expression.Property( subParam, nameof( SimpleParam.Name ) );

                if(this.CanMapByIndex)
                {
                    yield return Expression.IfThen
                    (
                        Expression.OrElse
                        (
                            //we check param name and index
                            Expression.IsTrue( Expression.Call( Expression.Constant( memberNameLowerCase ), _ordinalCmpMi, paramName, Expression.Constant( StringComparison.OrdinalIgnoreCase ) ) ),

                            Expression.AndAlso
                            (
                                Expression.Equal( paramName, Expression.Constant( String.Empty ) ),
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
                        Expression.IsTrue( Expression.Call( Expression.Constant( memberNameLowerCase ), _ordinalCmpMi, paramName, Expression.Constant( StringComparison.OrdinalIgnoreCase ) ) ),
                        exp
                    );
                }
            }
        }

        private IEnumerable<Expression> GetMemberAssignments( ReferenceMapperContext context,
            MemberInfo[] targetMembers, ParameterExpression subParam )
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

                //Expression memberExp =Expression.Empty();
                //if(memberMapping.MemberMappingExpression.Parameters.Count == 3)
                //{
                //    memberExp = Expression.Invoke( memberMapping.MemberMappingExpression,
                //        context.ReferenceTracker, context.SourceInstance, context.TargetInstance );
                //}
                //else
                //{

                //    memberExp = Expression.Invoke( memberMapping.MemberMappingExpression,
                //        context.SourceInstance, context.TargetInstance );
                //}

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

                var paramNameExp = Expression.Property( subParam, nameof( SimpleParam.Name ) );

                if(this.CanMapByIndex)
                {
                    yield return Expression.IfThen
                    (
                        Expression.OrElse
                        (
                            //we check param name and index
                            Expression.IsTrue( Expression.Call( Expression.Constant( memberNameLowerCase ), _ordinalCmpMi, paramNameExp, Expression.Constant( StringComparison.OrdinalIgnoreCase ) ) ),


                            Expression.AndAlso
                            (
                                Expression.Equal( paramNameExp, Expression.Constant( String.Empty ) ),
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
                        Expression.IsTrue( Expression.Call( Expression.Constant( memberNameLowerCase ), _ordinalCmpMi, paramNameExp, Expression.Constant( StringComparison.OrdinalIgnoreCase ) ) ),
                        exp
                    );
                }
            }
        }

        //private static readonly IMappingSource _blSource = new MappingSource<IParsedParam, bool>( s => ((BooleanParam)s).BoolValue );
        private static readonly IMappingSource _spSource = new MappingSource<IParsedParam, string>( s => ((SimpleParam)s).Value );
        private static readonly IMappingSource _cpSource = new MappingSource<IParsedParam, ComplexParam>( s => (ComplexParam)s );
        private static readonly IMappingSource _apSource = new MappingSource<IParsedParam, ArrayParam>( s => (ArrayParam)s );

        private static IMappingSource GetMappingSource( Type targetMemberType )
        {
            //if(targetMemberType == typeof( bool )) return _blSource;
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
                    var caseTest = (UnaryExpression)item.Test;

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
