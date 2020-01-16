using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Irony.Parsing;

namespace Pql.ExpressionEngine.Interfaces
{
    /// <summary>
    /// Shared utilities to handle evaluation of constant expressions. 
    /// </summary>
    public static class ConstantHelper
    {
        private static readonly ConcurrentDictionary<Tuple<ExpressionType, Type>, Tuple<object, MethodInfo>> UnaryOperators = new ConcurrentDictionary<Tuple<ExpressionType, Type>, Tuple<object, MethodInfo>>();
        private static readonly ConcurrentDictionary<Tuple<ExpressionType, Type, Type>, Tuple<object, MethodInfo>> BinaryOperators = new ConcurrentDictionary<Tuple<ExpressionType, Type, Type>, Tuple<object, MethodInfo>>();

        /// <summary>
        /// If all arguments are constant expressions, evaluates <paramref name="methodInfo"/> and returns result wrapped in <see cref="ConstantExpression"/>.
        /// Otherwise, returns an expression that will evaluate given method at run-time.
        /// </summary>
        public static Expression TryEvalConst(ParseTreeNode root, MethodInfo methodInfo, params Expression[] args)
        {
            if (methodInfo == null)
            {
                throw new ArgumentNullException("methodInfo");
            }

            if (args == null)
            {
                throw new ArgumentNullException("args");
            }

            try
            {
                foreach (var arg in args)
                {
                    if (!(arg is ConstantExpression))
                    {
                        return methodInfo.IsStatic ? Expression.Call(methodInfo, args) : Expression.Call(args[0], methodInfo, args.Skip(1));
                    }
                }

                var instance = methodInfo.IsStatic ? null : ((ConstantExpression) args[0]).Value;
                var skip = instance == null ? 0 : 1;
                object value;
                try
                {
                    value = methodInfo.Invoke(instance, args.Skip(skip).Select(x => ((ConstantExpression) x).Value).ToArray());
                }
                catch (TargetInvocationException e)
                {
                    if (e.InnerException == null) throw;
                    throw e.InnerException;
                }
                return Expression.Constant(value, methodInfo.ReturnType);
            }
            catch (InvalidOperationException e)
            {
                throw new CompilationException(e.Message, root);
            }
        }

        /// <summary>
        /// If <paramref name="instance"/> is a constant expression of type Nullable(T), evaluates <paramref name="propertyInfo"/> and returns result wrapped in <see cref="ConstantExpression"/>.
        /// Otherwise, returns an expression that will evaluate given method at run-time.
        /// </summary>
        public static Expression TryEvalConst(ParseTreeNode root, PropertyInfo propertyInfo, Expression instance)
        {
            if (propertyInfo == null)
            {
                throw new ArgumentNullException("propertyInfo");
            }

            if (instance == null)
            {
                throw new ArgumentNullException("instance");
            }

            try
            {
                if (!(instance is ConstantExpression))
                {
                    return Expression.Property(instance, propertyInfo);
                }

                var value = propertyInfo.GetValue(((ConstantExpression)instance).Value);
                return Expression.Constant(value, propertyInfo.PropertyType);
            }
            catch (InvalidOperationException e)
            {
                throw new CompilationException(e.Message, root);
            }
        }

        /// <summary>
        /// If <paramref name="instance"/> is a constant expression of type Nullable(T), evaluates <paramref name="fieldInfo"/> and returns result wrapped in <see cref="ConstantExpression"/>.
        /// Otherwise, returns an expression that will evaluate given method at run-time.
        /// </summary>
        public static Expression TryEvalConst(ParseTreeNode root, FieldInfo fieldInfo, Expression instance)
        {
            if (fieldInfo == null)
            {
                throw new ArgumentNullException("fieldInfo");
            }

            if (instance == null)
            {
                throw new ArgumentNullException("instance");
            }

            try
            {
                if (!(instance is ConstantExpression))
                {
                    return Expression.Field(instance, fieldInfo);
                }

                var value = fieldInfo.GetValue(((ConstantExpression)instance).Value);
                return Expression.Constant(value, fieldInfo.FieldType);
            }
            catch (InvalidOperationException e)
            {
                throw new CompilationException(e.Message, root);
            }
        }

        /// <summary>
        /// If all arguments are constant expressions, evaluates <paramref name="constructorInfo"/> and returns created object wrapped in <see cref="ConstantExpression"/>.
        /// Otherwise, returns an expression that will run this constructor at run-time.
        /// </summary>
        public static Expression TryEvalConst(ParseTreeNode root, ConstructorInfo constructorInfo, params Expression[] args)
        {
            if (constructorInfo == null)
            {
                throw new ArgumentNullException("constructorInfo");
            }

            if (args == null)
            {
                throw new ArgumentNullException("args");
            }

            try
            {
                foreach (var arg in args)
                {
                    if (!(arg is ConstantExpression))
                    {
                        return Expression.New(constructorInfo, args);
                    }
                }

                object value;
                try
                {
                    value = constructorInfo.Invoke(args.Select(x => ((ConstantExpression) x).Value).ToArray());
                }
                catch (TargetInvocationException e)
                {
                    if (e.InnerException == null) throw;
                    throw e.InnerException;
                }
                return Expression.Constant(value);
            }
            catch (InvalidOperationException e)
            {
                throw new CompilationException(e.Message, root);
            }
        }

        /// <summary>
        /// If <paramref name="left"/> and <paramref name="right"/> are constant expressions, evaluates operator <paramref name="op"/> and returns result wrapped in <see cref="ConstantExpression"/>.
        /// Otherwise, returns an expression that will evaluate given method at run-time.
        /// </summary>
        public static Expression TryEvalConst(ParseTreeNode root, Expression left, Expression right, ExpressionType op, Type returnType = null)
        {
            if (left == null)
            {
                throw new ArgumentNullException("left");
            }

            if (right == null)
            {
                throw new ArgumentNullException("right");
            }
            try
            {
                return !(left is ConstantExpression x) || !(right is ConstantExpression y)
                           ? (Expression)Expression.MakeBinary(op, left, right)
                           : EvalConst(root, x, y, op, returnType);
            }
            catch (InvalidOperationException e)
            {
                throw new CompilationException(e.Message, root);
            }
        }

        /// <summary>
        /// Evaluates operator <paramref name="op"/> and returns result wrapped in <see cref="ConstantExpression"/>.
        /// </summary>
        public static ConstantExpression EvalConst(ParseTreeNode root, ConstantExpression left, ConstantExpression right, ExpressionType op, Type returnType)
        {
            if (left == null)
            {
                throw new ArgumentNullException("left");
            }

            if (right == null)
            {
                throw new ArgumentNullException("right");
            }

            try
            {
                var method = BinaryOperators.GetOrAdd(
                    new Tuple<ExpressionType, Type, Type>(op, left.Type, 
                        (op == ExpressionType.Convert || op == ExpressionType.ConvertChecked) ? (Type) right.Value : right.Type), tuple =>
                        {
                            Delegate func;
                            if (op == ExpressionType.Convert || op == ExpressionType.ConvertChecked)
                            {
                                var arg1 = Expression.Parameter(tuple.Item2);
                                var type = tuple.Item3;
                                var expr = Expression.MakeUnary(tuple.Item1, arg1, type);
                                func = Expression.Lambda(Expression.GetFuncType(arg1.Type, type), expr, arg1).Compile();
                            }
                            else
                            {
                                var arg1 = Expression.Parameter(tuple.Item2);
                                var arg2 = Expression.Parameter(tuple.Item3);
                                var expr = Expression.MakeBinary(tuple.Item1, arg1, arg2);
                                func = Expression.Lambda(Expression.GetFuncType(arg1.Type, arg2.Type, expr.Type), expr, arg1, arg2).Compile();
                            }
                            return new Tuple<object, MethodInfo>(func, ReflectionHelper.GetOrAddMethodAny(func.GetType(), "Invoke"));
                        });

                var args = (op == ExpressionType.Convert || op == ExpressionType.ConvertChecked) ? new[] {left.Value} : new[] {left.Value, right.Value};
                object value;
                try
                {
                    value = method.Item2.Invoke(method.Item1, args);
                }
                catch (TargetInvocationException e)
                {
                    if (e.InnerException == null) throw;
                    throw e.InnerException;
                }
                return Expression.Constant(value, returnType ?? method.Item2.ReturnType);
            }
            catch (InvalidOperationException e)
            {
                throw new CompilationException(e.Message, root);
            }
        }

        /// <summary>
        /// Evaluates operator <paramref name="op"/> and returns result wrapped in <see cref="ConstantExpression"/>.
        /// </summary>
        public static Expression TryEvalConst(ParseTreeNode root, Expression left, ExpressionType op, Type returnType = null)
        {
            if (left == null)
            {
                throw new ArgumentNullException("left");
            }

            try
            {
                return !(left is ConstantExpression constant) ? Expression.MakeUnary(op, left, returnType ?? left.Type) : (Expression)EvalConst(root, constant, op, returnType);
            }
            catch (InvalidOperationException e)
            {
                throw new CompilationException(e.Message, root);
            }
        }

        /// <summary>
        /// Evaluates operator <paramref name="op"/> and returns result wrapped in <see cref="ConstantExpression"/>.
        /// </summary>
        public static ConstantExpression EvalConst(ParseTreeNode root, ConstantExpression left, ExpressionType op, Type returnType)
        {
            if (left == null)
            {
                throw new ArgumentNullException("left");
            }

            if (returnType == null)
            {
                throw new ArgumentNullException("returnType");
            }

            try
            {
                if (op == ExpressionType.Convert || op == ExpressionType.ConvertChecked)
                {
                    var method = BinaryOperators.GetOrAdd(
                        new Tuple<ExpressionType, Type, Type>(op, left.Type, returnType), tuple =>
                            {
                                var arg1 = Expression.Parameter(tuple.Item2);
                                var expr = Expression.MakeUnary(tuple.Item1, arg1, tuple.Item3);
                                var func = Expression.Lambda(Expression.GetFuncType(arg1.Type, expr.Type), expr, arg1).Compile();
                                return new Tuple<object, MethodInfo>(func, ReflectionHelper.GetOrAddMethodAny(func.GetType(), "Invoke"));
                            });

                    object value;
                    try
                    {
                        value = method.Item2.Invoke(method.Item1, new[] {left.Value});
                    }
                    catch (TargetInvocationException e)
                    {
                        if (e.InnerException == null) throw;
                        throw e.InnerException;
                    }
                    return Expression.Constant(value, returnType);
                }
                else
                {
                    var method = UnaryOperators.GetOrAdd(
                        new Tuple<ExpressionType, Type>(op, left.Type), tuple =>
                            {
                                var arg1 = Expression.Parameter(tuple.Item2);
                                var expr = Expression.MakeUnary(tuple.Item1, arg1, null);
                                var func = Expression.Lambda(Expression.GetFuncType(arg1.Type, expr.Type), expr, arg1).Compile();
                                return new Tuple<object, MethodInfo>(func, ReflectionHelper.GetOrAddMethodAny(func.GetType(), "Invoke"));
                            });

                    object value;
                    try
                    {
                        value = method.Item2.Invoke(method.Item1, new[] {left.Value});
                    }
                    catch (TargetInvocationException e)
                    {
                        if (e.InnerException == null) throw;
                        throw e.InnerException;
                    }
                    return Expression.Constant(value, returnType);
                }
            }
            catch (InvalidOperationException e)
            {
                throw new CompilationException(e.Message, root);
            }
        }

        /// <summary>
        /// Invokes specified binary operation
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static K InvokeBinaryOperation<T, K>(ExpressionType expressionType, T v1, T v2)
        {
            return (K)ConstantHelper.EvalConst(null, Expression.Constant(v1), Expression.Constant(v2), expressionType, typeof(K)).Value;
        }

        /// <summary>
        /// Invokes specified binary operation
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T InvokeBinaryOperation<T>(ExpressionType expressionType, T v1, T v2)
        {
            return InvokeBinaryOperation<T, T>(expressionType, v1, v2);
        }
    }
}