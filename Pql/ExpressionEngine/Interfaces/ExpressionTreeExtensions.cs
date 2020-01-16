using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using Irony.Parsing;
using Pql.ExpressionEngine.Utilities;

namespace Pql.ExpressionEngine.Interfaces
{
    /// <summary>
    /// Utilities for analyzer.
    /// </summary>
    public static class ExpressionTreeExtensions
    {
        private static readonly List<Type> Numerics = new List<Type>
            {
                typeof (SByte),
                typeof (Byte),
                typeof (Int16),
                typeof (UInt16),
                typeof (Int32),
                typeof (UInt32),
                typeof (Int64),
                typeof (UInt64),
                typeof (Single),
                typeof (Double),
                typeof (Decimal)
            };
        
        private static readonly List<Type> Integers = new List<Type>
            {
                typeof (Byte),
                typeof (SByte),
                typeof (Int16),
                typeof (UInt16),
                typeof (Int32),
                typeof (UInt32),
                typeof (Int64),
                typeof (UInt64)
            };

        /// <summary>
        /// Marker type to indicate "NULL" literals and any other expressions that are not supposed to return a value.
        /// </summary>
        public struct VoidTypeMarker
        {
        }

        /// <summary>
        /// Keywords and operators use ASCII. Most of them (comparison, arith, negation) are not affected by case sensitivity,
        /// and thus we won't need to lowercase them during analysis.
        /// </summary>
        /// <returns>True if first character <paramref name="text"/> is non-ASCII, or is ASCII letter [a..z], [A..Z]</returns>
        public static bool IsKeywordAffectedByCase(this string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            var c = (int) text[0];
            return c > 255 || c >= 65 && c <= 90 || c >= 97 && c <= 122;
        }

        /// <summary>
        /// Verifies that the given root has nested levels of children, as specified by <paramref name="childIndexes"/> for each nesting level.
        /// </summary>
        /// <param name="root">The parse tree node</param>
        /// <param name="leafTermName">Optional name of the leaf term, to be checked on the lowest leaf</param>
        /// <param name="childIndexes">Child index to take in each subsequent child node</param>
        /// <returns>The lowest parse tree node</returns>
        /// <exception cref="ArgumentNullException"><paramref name="root"/> is null</exception>
        /// <exception cref="CompilationException">Child is absent at some level, or lead child's term name does not match <paramref name="leafTermName"/></exception> 
        public static ParseTreeNode RequireChild(this ParseTreeNode root, string leafTermName, params int[] childIndexes)
        {
            if (root == null)
            {
                throw new ArgumentNullException("root");
            }

            foreach (var index in childIndexes) 
            {
                if (root == null || root.ChildNodes.Count <= index)
                {
                    throw new CompilationException(String.Format("Node {0} is expected to have at least {1} children", root, index + 1), root);
                }

                root = root.ChildNodes[index];
            }

            if (leafTermName != null)
            {
                if (root.Term == null || 0 != StringComparer.OrdinalIgnoreCase.Compare(root.Term.Name, leafTermName))
                {
                    throw new CompilationException(String.Format("Expected {0} instead of {1}", leafTermName, root), root);
                }
            }

            return root;
        }

        /// <summary>
        /// Verifies that the given root has nested levels of children, as specified by <paramref name="childIndexes"/> for each nesting level.
        /// </summary>
        /// <param name="root">The parse tree node</param>
        /// <param name="leafTermName">Optional name of the leaf term, to be checked on the lowest leaf</param>
        /// <param name="childIndexes">Child index to take in each subsequent child node</param>
        /// <returns>The lowest parse tree node</returns>
        /// <exception cref="ArgumentNullException"><paramref name="root"/> is null</exception>
        public static ParseTreeNode TryGetChild(this ParseTreeNode root, string leafTermName, params int[] childIndexes)
        {
            if (root == null)
            {
                throw new ArgumentNullException("root");
            }

            foreach (var index in childIndexes) 
            {
                if (root == null || root.ChildNodes.Count <= index)
                {
                    return null;
                }

                root = root.ChildNodes[index];
            }

            if (leafTermName != null)
            {
                if (root.Term == null || 0 != StringComparer.OrdinalIgnoreCase.Compare(root.Term.Name, leafTermName))
                {
                    return null;
                }
            }

            return root;
        }

        /// <summary>
        /// Requires that a given parse tree node has specified number of children.
        /// </summary>
        /// <param name="root">The parse tree node</param>
        /// <param name="count">Number of children to check for</param>
        /// <exception cref="ArgumentNullException"><paramref name="root"/> is null</exception>
        /// <exception cref="CompilationException">Number of children does not match <paramref name="count"/></exception>
        public static void RequireChildren(this ParseTreeNode root, int count)
        {
            if (root == null)
            {
                throw new ArgumentNullException("root");
            }

            var realCount = root.ChildNodes == null ? 0 : root.ChildNodes.Count;
            if (count != realCount)
            {
                throw new CompilationException(root);
            }
        }

        /// <summary>
        /// Requires that a given parse tree node has specified number of children.
        /// </summary>
        /// <param name="root">The parse tree node</param>
        /// <param name="minCount">Minimum number of children to check for</param>
        /// <param name="maxCount">Maximum number of children to check for</param>
        /// <exception cref="ArgumentNullException"><paramref name="root"/> is null</exception>
        /// <exception cref="CompilationException">Number of children is not between <paramref name="minCount"/> and <paramref name="maxCount"/></exception>
        public static void RequireChildren(this ParseTreeNode root, int minCount, int maxCount)
        {
            if (root == null)
            {
                throw new ArgumentNullException("root");
            }

            if (minCount < 0)
            {
                throw new ArgumentOutOfRangeException("minCount", minCount, "Must be non-negative");
            }

            if (maxCount < 0)
            {
                throw new ArgumentOutOfRangeException("maxCount", maxCount, "Must be non-negative");
            }

            if (maxCount < minCount)
            {
                throw new ArgumentException("maxCount must be greater or equal to minCount");
            }

            var realCount = root.ChildNodes == null ? 0 : root.ChildNodes.Count;
            if (realCount > maxCount || realCount < minCount)
            {
                throw new CompilationException(String.Format(
                    "Invalid number of child nodes, expected from {0} to {1}, found {2}", minCount, maxCount, realCount), root);
            }
        }

        /// <summary>
        /// Requires that the logical data type of specified expression node is one of the integer types.
        /// </summary>
        /// <param name="target">The expression node</param>
        /// <param name="root">Corresponding parse tree node</param>
        /// <exception cref="ArgumentNullException"><paramref name="root"/> is null</exception>
        /// <exception cref="CompilationException">Expression must be of integer type</exception>
        public static void RequireInteger(this Expression target, ParseTreeNode root)
        {
            if (root == null)
            {
                throw new ArgumentNullException("root");
            }

            if (!IsInteger(target))
            {
                throw new CompilationException("Expression must be of integer type. Actual: " + target.Type.FullName, root);
            }
        }

        /// <summary>
        /// Requires that the data type of specified expression node is one of the integer types.
        /// </summary>
        /// <param name="target">The expression node</param>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> is null</exception>
        public static bool IsInteger(this Expression target)
        {
            if (target == null)
            {
                throw new ArgumentNullException("target");
            }

            return IsInteger(target.Type);
        }

        /// <summary>
        /// Requires that the data type of specified expression node is one of the integer types.
        /// </summary>
        /// <param name="target">The expression node</param>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> is null</exception>
        public static bool IsInteger(this Type target)
        {
            if (target == null)
            {
                throw new ArgumentNullException("target");
            }

            return Integers.Contains(target);
        }

        /// <summary>
        /// Requires that the data type of specified expression node is one of the real numeric types.
        /// </summary>
        /// <param name="target">The expression node</param>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> is null</exception>
        public static bool IsRealNumeric(this Expression target)
        {
            if (target == null)
            {
                throw new ArgumentNullException("target");
            }

            return IsRealNumeric(target.Type);
        }

        /// <summary>
        /// Requires that the data type of specified expression node is one of the real numeric types.
        /// </summary>
        /// <param name="target">The expression node</param>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> is null</exception>
        public static bool IsRealNumeric(this Type target)
        {
            if (target == null)
            {
                throw new ArgumentNullException("target");
            }

            return target == typeof(Single) || target == typeof(Double) || target == typeof(Decimal);
        }

        /// <summary>
        /// Requires that the data type of specified expression node is of boolean type.
        /// </summary>
        /// <param name="target">The expression node</param>
        /// <param name="root">Corresponding parse tree node</param>
        /// <exception cref="ArgumentNullException"><paramref name="root"/> is null</exception>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> is null</exception>
        /// <exception cref="CompilationException">Expression must be of boolean type</exception>
        public static void RequireBoolean(this Expression target, ParseTreeNode root)
        {
            if (root == null)
            {
                throw new ArgumentNullException("root");
            }

            if (!IsBoolean(target))
            {
                throw new CompilationException("Expression must be of boolean type. Actual: " + target.Type.FullName, root);
            }
        }

        /// <summary>
        /// Requires that the data type of specified expression node is of boolean type.
        /// </summary>
        /// <param name="target">The expression node</param>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> is null</exception>
        public static bool IsBoolean(this Expression target)
        {
            if (target == null)
            {
                throw new ArgumentNullException("target");
            }

            return IsBoolean(target.Type);
        }

        /// <summary>
        /// Requires that the data type of specified expression node is of boolean type.
        /// </summary>
        /// <param name="target">The expression node</param>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> is null</exception>
        public static bool IsBoolean(this Type target)
        {
            if (target == null)
            {
                throw new ArgumentNullException("target");
            }

            return ReferenceEquals(target, typeof(Boolean));
        }

        /// <summary>
        /// Requires that the data type of specified expression node is equal to <paramref name="type"/>.
        /// </summary>
        /// <param name="target">The expression node</param>
        /// <param name="root">Corresponding parse tree node</param>
        /// <param name="type">The data type to check for</param>
        /// <exception cref="ArgumentNullException"><paramref name="root"/> is null</exception>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> is null</exception>
        /// <exception cref="ArgumentNullException"><paramref name="type"/> is null</exception>
        /// <exception cref="CompilationException">Expression must be of specified type</exception>
        public static void RequireType(this Expression target, Type type, ParseTreeNode root)
        {
            if (root == null)
            {
                throw new ArgumentNullException("root");
            }

            if (type == null)
            {
                throw new ArgumentNullException("type");
            }

            if (target == null)
            {
                throw new ArgumentNullException("target");
            }

            if (!ReferenceEquals(type, target.Type))
            {
                throw new CompilationException("Expression must be of type " + type.FullName + ". Actual: " + target.Type.FullName, root);
            }
        }

        /// <summary>
        /// Requires that the data type of specified expression node is of any reference type.
        /// </summary>
        /// <param name="target">The expression node</param>
        /// <param name="root">Corresponding parse tree node</param>
        /// <exception cref="ArgumentNullException"><paramref name="root"/> is null</exception>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> is null</exception>
        /// <exception cref="CompilationException">Expression must be of reference type</exception>
        public static void RequireReferenceType(this Expression target, ParseTreeNode root)
        {
            if (root == null)
            {
                throw new ArgumentNullException("root");
            }

            if (target == null)
            {
                throw new ArgumentNullException("target");
            }

            if (target.Type.IsValueType)
            {
                throw new CompilationException("Expression must be of reference type. Actual: " + target.Type.FullName, root);
            }
        }

        /// <summary>
        /// Requires that the data type of specified expression node is one of the numeric types.
        /// </summary>
        /// <param name="target">The expression node</param>
        /// <param name="root">Corresponding parse tree node</param>
        /// <exception cref="ArgumentNullException"><paramref name="root"/> is null</exception>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> is null</exception>
        /// <exception cref="CompilationException">Expression must be of numeric type</exception>
        public static void RequireNumeric(this Expression target, ParseTreeNode root)
        {
            if (root == null)
            {
                throw new ArgumentNullException("root");
            }

            if (!IsNumeric(target))
            {
                throw new CompilationException("Expression must be numeric. Actual: " + target.Type.FullName, root);
            }
        }

        /// <summary>
        /// Requires that the data type of specified expression node is one of the numeric types.
        /// </summary>
        /// <param name="target">The expression node</param>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> is null</exception>
        public static bool IsNumeric(this Expression target)
        {
            if (target == null)
            {
                throw new ArgumentNullException("target");
            }

            return Numerics.Contains(target.Type.IsNullableType() ? target.Type.GetUnderlyingType() : target.Type);
        }

        /// <summary>
        /// Requires that the data type of specified expression node is one of the numeric types.
        /// </summary>
        /// <param name="target">The expression node</param>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> is null</exception>
        public static bool IsNumeric(this Type target)
        {
            if (target == null)
            {
                throw new ArgumentNullException("target");
            }

            return Numerics.Contains(target);
        }

        /// <summary>
        /// Requires that the data type of specified expression node is binary data.
        /// </summary>
        /// <param name="target">The expression node</param>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> is null</exception>
        public static bool IsBinary(this Expression target)
        {
            if (target == null)
            {
                throw new ArgumentNullException("target");
            }

            return IsBinary(target.Type);
        }

        /// <summary>
        /// Requires that the data type of specified expression node is binary data.
        /// </summary>
        /// <param name="target">The expression node</param>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> is null</exception>
        public static bool IsBinary(this Type target)
        {
            if (target == null)
            {
                throw new ArgumentNullException("target");
            }

            return ReferenceEquals(target, typeof(SizableArrayOfByte));
        }

        /// <summary>
        /// Requires that the data type of specified expression node is string.
        /// </summary>
        /// <param name="target">The expression node</param>
        /// <param name="root">Corresponding parse tree node</param>
        /// <exception cref="ArgumentNullException"><paramref name="root"/> is null</exception>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> is null</exception>
        /// <exception cref="CompilationException">Expression must be of string type</exception>
        public static void RequireString(this Expression target, ParseTreeNode root)
        {
            if (root == null)
            {
                throw new ArgumentNullException("root");
            }

            if (!IsString(target))
            {
                throw new CompilationException("Expression must be string. Actual: " + target.Type.FullName, root);
            }
        }

        /// <summary>
        /// Requires that the data type of specified expression node is string.
        /// </summary>
        /// <param name="target">The expression node</param>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> is null</exception>
        public static bool IsString(this Expression target)
        {
            if (target == null)
            {
                throw new ArgumentNullException("target");
            }

            return IsString(target.Type);
        }

        /// <summary>
        /// Requires that the data type of specified expression node is string.
        /// </summary>
        /// <param name="target">The expression node</param>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> is null</exception>
        public static bool IsString(this Type target)
        {
            if (target == null)
            {
                throw new ArgumentNullException("target");
            }

            return ReferenceEquals(target, typeof(String));
        }

        /// <summary>
        /// Requires that the data type of specified expression node is a datetime value.
        /// </summary>
        /// <param name="target">The expression node</param>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> is null</exception>
        public static bool IsDateTime(this Expression target)
        {
            if (target == null)
            {
                throw new ArgumentNullException("target");
            }

            return ReferenceEquals(target.Type.IsNullableType() ? target.Type.GetUnderlyingType() : target.Type, typeof(DateTime));
        }

        /// <summary>
        /// Requires that the data type of specified expression node is a datetime value.
        /// </summary>
        /// <param name="target">The expression node</param>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> is null</exception>
        public static bool IsDateTime(this Type target)
        {
            if (target == null)
            {
                throw new ArgumentNullException("target");
            }

            return ReferenceEquals(target, typeof(DateTime));
        }

        /// <summary>
        /// Requires that the data type of specified expression node is a DateTimeOffset value.
        /// </summary>
        /// <param name="target">The expression node</param>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> is null</exception>
        public static bool IsDateTimeOffset(this Expression target)
        {
            if (target == null)
            {
                throw new ArgumentNullException("target");
            }

            return ReferenceEquals(target.Type.IsNullableType() ? target.Type.GetUnderlyingType() : target.Type, typeof(DateTimeOffset));
        }

        /// <summary>
        /// Requires that the data type of specified expression node is a DateTimeOffset value.
        /// </summary>
        /// <param name="target">The expression node</param>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> is null</exception>
        public static bool IsDateTimeOffset(this Type target)
        {
            if (target == null)
            {
                throw new ArgumentNullException("target");
            }

            return ReferenceEquals(target, typeof(DateTimeOffset));
        }

        /// <summary>
        /// Requires that the data type of specified expression node is a timespan value.
        /// </summary>
        /// <param name="target">The expression node</param>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> is null</exception>
        public static bool IsTimeSpan(this Type target)
        {
            if (target == null)
            {
                throw new ArgumentNullException("target");
            }

            return ReferenceEquals(target, typeof(TimeSpan));
        }

        /// <summary>
        /// Requires that the data type of specified expression node is a Guid value.
        /// </summary>
        /// <param name="target">The expression node</param>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> is null</exception>
        public static bool IsGuid(this Type target)
        {
            if (target == null)
            {
                throw new ArgumentNullException("target");
            }

            return ReferenceEquals(target, typeof(Guid));
        }

        /// <summary>
        /// Requires that the data type of specified expression node is a timespan value.
        /// </summary>
        /// <param name="target">The expression node</param>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> is null</exception>
        public static bool IsTimeSpan(this Expression target)
        {
            if (target == null)
            {
                throw new ArgumentNullException("target");
            }

            return ReferenceEquals(target.Type.IsNullableType() ? target.Type.GetUnderlyingType() : target.Type, typeof(TimeSpan));
        }

        /// <summary>
        /// Requires that the specified expression is of some specific Nullable(T) type.
        /// </summary>
        /// <param name="target">The expression node</param>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> is null</exception>
        public static bool IsNullableType(this Expression target)
        {
            if (target == null)
            {
                throw new ArgumentNullException("target");
            }

            return IsNullableType(target.Type);
        }

        /// <summary>
        /// Requires that the specified type is some specific Nullable(T) type.
        /// </summary>
        /// <param name="target">The type</param>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> is null</exception>
        public static bool IsNullableType(this Type target)
        {
            if (target == null)
            {
                throw new ArgumentNullException("target");
            }

            return target.IsConstructedGenericType && ReferenceEquals(target.GetGenericTypeDefinition(), typeof (UnboxableNullable<>));
        }

        /// <summary>
        /// If the specified type is some specific Nullable(T) type, returns its underlying type.
        /// Otherwise, returns the supplied type.
        /// </summary>
        /// <param name="target">The type</param>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> is null</exception>
        public static Type GetUnderlyingType(this Type target)
        {
            if (target == null)
            {
                throw new ArgumentNullException("target");
            }

            return target.IsNullableType() ? UnboxableNullable.GetUnderlyingType(target) : target;
        }

        /// <summary>
        /// Requires that the specified expression is a BlockExpression.
        /// </summary>
        /// <param name="target">The expression node</param>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> is null</exception>
        public static bool IsVoid(this Expression target)
        {
            if (target == null)
            {
                throw new ArgumentNullException("target");
            }

            return ReferenceEquals(target.Type, typeof(UnboxableNullable<VoidTypeMarker>));
        }

        /// <summary>
        /// Requires that the data type of specified expression node is not a void marker.
        /// </summary>
        /// <param name="target">The expression node</param>
        /// <param name="root">Corresponding parse tree node</param>
        /// <exception cref="ArgumentNullException"><paramref name="root"/> is null</exception>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> is null</exception>
        /// <exception cref="CompilationException">Expression must return some value</exception>
        /// <see cref="VoidTypeMarker"/>
        public static void RequireNonVoid(this Expression target, ParseTreeNode root)
        {
            if (root == null)
            {
                throw new ArgumentNullException("root");
            }

            if (target.IsVoid())
            {
                throw new CompilationException("Expression is expected to return some value, but it is void", root);
            }
        }

        /// <summary>
        /// Generates a constant expression of "default(type)".
        /// </summary>
        /// <param name="targetType">Desired type</param>
        public static ConstantExpression GetDefaultExpression(Type targetType)
        {
            if (targetType == null)
            {
                throw new ArgumentNullException("targetType");
            }

            if (targetType.IsValueType)
            {
                var ctr = targetType.GetConstructor(new Type[0]);
                if (ctr == null)
                {
                    return Expression.Constant(Activator.CreateInstance(targetType, null), targetType);
                }
                return Expression.Constant(ctr.Invoke(null), targetType);
            }
            
            return Expression.Constant(null, targetType);
        }

        /// <summary>
        /// If supplied expression is of Nullable(T) type, invokes its GetValueOrDefault method.
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        public static Expression RemoveNullability(this Expression expression)
        {
            return 
                expression.IsVoid() 
                ? expression
                : expression.IsNullableType()
                       ? ConstantHelper.TryEvalConst(null, expression.Type.GetField("Value"), expression)
                       : expression;
        }

        /// <summary>
        /// If supplied expression is of Nullable(T) type, invokes its HasValue method.
        /// </summary>
        public static Expression NullableHasValue(this Expression expression)
        {
            if (!expression.IsNullableType())
            {
                return expression;
            }
            
            return ConstantHelper.TryEvalConst(null, expression.Type.GetField("HasValue"), expression);
        }
        
        /// <summary>
        /// Attempts to auto-adjust the size of arguments for a binary arithmetic operation. 
        /// Integers grow up to Int64, Double or Decimal, floats grow up to Decimal. 
        /// </summary>
        /// <param name="leftExpr">Left argument</param>
        /// <param name="rightExpr">Right argument</param>
        /// <param name="root">Corresponding parse tree node</param>
        /// <exception cref="ArgumentNullException"><paramref name="root"/> is null</exception>
        /// <exception cref="ArgumentNullException"><paramref name="leftExpr"/> is null</exception>
        /// <exception cref="ArgumentNullException"><paramref name="rightExpr"/> is null</exception>
        /// <exception cref="CompilationException">Argument types cannot be adjusted, try conversion</exception>
        public static void AdjustArgumentsForBinaryOperation(ref Expression leftExpr, ref Expression rightExpr, ParseTreeNode root)
        {
            if (root == null)
            {
                throw new ArgumentNullException("root");
            }

            if (leftExpr == null)
            {
                throw new ArgumentNullException("leftExpr");
            }

            if (rightExpr == null)
            {
                throw new ArgumentNullException("rightExpr");
            }

            // do we actually have to adjust them?
            if (ReferenceEquals(leftExpr.Type, rightExpr.Type))
            {
                return;
            }

            if (leftExpr.IsNumeric() && rightExpr.IsNumeric())
            {
                var leftSize = Marshal.SizeOf(leftExpr.Type);
                var rightSize = Marshal.SizeOf(rightExpr.Type);

                if (leftSize < rightSize)
                {
                    leftExpr = ConstantHelper.TryEvalConst(root, leftExpr, ExpressionType.Convert, rightExpr.Type);
                }
                else if (leftSize > rightSize)
                {
                    rightExpr = ConstantHelper.TryEvalConst(root, rightExpr, ExpressionType.Convert, leftExpr.Type);
                }
                else if (ReferenceEquals(leftExpr.Type, typeof (Single)))
                {
                    // sizes are equal, so the other guy is probably Int32 or UInt32
                    rightExpr = ConstantHelper.TryEvalConst(root, rightExpr, ExpressionType.Convert, leftExpr.Type);
                }
                else if (ReferenceEquals(rightExpr.Type, typeof (Single)))
                {
                    // sizes are equal, so the other guy is probably Int32 or UInt32
                    leftExpr = ConstantHelper.TryEvalConst(root, leftExpr, ExpressionType.Convert, rightExpr.Type);
                }
                else if (ReferenceEquals(leftExpr.Type, typeof (Double)))
                {
                    // sizes are equal, so the other guy is probably Int64 or UInt64
                    rightExpr = ConstantHelper.TryEvalConst(root, rightExpr, ExpressionType.Convert, leftExpr.Type);
                }
                else if (ReferenceEquals(rightExpr.Type, typeof (Double)))
                {
                    // sizes are equal, so the other guy is probably Int64 or UInt64
                    leftExpr = ConstantHelper.TryEvalConst(root, leftExpr, ExpressionType.Convert, rightExpr.Type);
                }
            }
            else
            {
                throw new CompilationException(
                    String.Format(
                        "Argument types {0} and {1} cannot be auto-adjusted, try conversion",
                        leftExpr.Type.FullName, rightExpr.Type.FullName), root);
            }
        }

        /// <summary>
        /// Attempts to auto-adjust the size of argument to the desired type. 
        /// Integers grow up to Int64, Double or Decimal, floats grow up to Decimal. 
        /// </summary>
        /// <param name="root">Optional, corresponding parse tree node</param>
        /// <param name="expr">Expression whose value is to be adjusted, must be of numeric value type</param>
        /// <param name="targetType">Desired type</param>
        /// <exception cref="ArgumentNullException"><paramref name="expr"/> is null</exception>
        /// <exception cref="ArgumentNullException"><paramref name="targetType"/> is null</exception>
        /// <exception cref="CompilationException">Argument types cannot be adjusted, try conversion</exception>
        public static Expression AdjustReturnType(ParseTreeNode root, Expression expr, Type targetType)
        {

            if (!TryAdjustReturnType(root, expr, targetType, out var adjusted))
            {
                throw new CompilationException(
                    String.Format(
                        "Return type {0} cannot be auto-adjusted to {1}, try conversion", expr.Type.FullName, targetType.FullName), root);
            }

            return adjusted;
        }

        /// <summary>
        /// Attempts to auto-adjust the size of argument to the desired type. 
        /// Integers grow up to Int64, Double or Decimal, floats grow up to Decimal. 
        /// </summary>
        /// <param name="root">Optional, corresponding parse tree node</param>
        /// <param name="expr">Expression whose value is to be adjusted, must be of numeric value type</param>
        /// <param name="targetType">Desired type</param>
        /// <param name="adjusted">New or same expression</param>
        /// <exception cref="ArgumentNullException"><paramref name="expr"/> is null</exception>
        /// <exception cref="ArgumentNullException"><paramref name="targetType"/> is null</exception>
        public static bool TryAdjustReturnType(ParseTreeNode root, Expression expr, Type targetType, out Expression adjusted)
        {
            if (expr == null)
            {
                throw new ArgumentNullException("expr");
            }

            if (targetType == null)
            {
                throw new ArgumentNullException("targetType");
            }

            // do we actually have to adjust them?
            if (ReferenceEquals(expr.Type, targetType))
            {
                adjusted = expr;
                return true;
            }

            if (expr.IsVoid())
            {
                adjusted = GetDefaultExpression(targetType);
                return true;
            }

            if (targetType.IsNullableType())
            {
                if (expr.IsNullableType())
                {
                    var variable = Expression.Variable(expr.Type);
                    var setvar = Expression.Assign(variable, expr);
                    var hasvalue = Expression.Field(variable, "HasValue");
                    Expression value = Expression.Field(variable, "Value");
                    if (TryAdjustReturnType(root, value, UnboxableNullable.GetUnderlyingType(targetType), out var adjustedValue))
                    {
                        adjusted = Expression.Block(
                            targetType,
                            new[] { variable },
                            setvar,
                            Expression.Condition(hasvalue, MakeNewNullable(targetType, adjustedValue), MakeNewNullable(targetType)));
                        return true;
                    }
                }
                else
                {
                    if (TryAdjustReturnType(root, expr, UnboxableNullable.GetUnderlyingType(targetType), out var adjustedValue))
                    {
                        adjusted = MakeNewNullable(targetType, adjustedValue);
                        return true;
                    }
                }
            }
            else if (expr.IsNullableType())
            {
                var nonnullable = expr.RemoveNullability();
                return TryAdjustReturnType(root, nonnullable, targetType, out adjusted);
            }
            else
            {
                return TryAdjustNumericType(root, expr, targetType, out adjusted);
            }

            adjusted = null;
            return false;
        }

        /// <summary>
        /// Generates an expression of type Nullable(T), given its underlying value.
        /// </summary>
        public static Expression MakeNewNullable(Expression value)
        {
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }

            var nullableType = typeof(UnboxableNullable<>).MakeGenericType(value.Type);
            var constructorInfo = nullableType.GetConstructor(new[] { value.Type });
            if (constructorInfo == null)
            {
                throw new Exception(string.Format("Could not locate constructor on type {0} with a single argument of type {1}",
                    nullableType.FullName, value.Type.FullName));
            }

            if (value is ConstantExpression expression)
            {
                var constValue = constructorInfo.Invoke(new[] { expression.Value });
                return Expression.Constant(constValue, nullableType);
            }

            return Expression.New(constructorInfo, value);
        }

        /// <summary>
        /// Generate expression to convert nullable<T> to nullable<toType>
        /// </summary>
        public static Expression ConvertNullable(Expression value, Type toType)
        {
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }

            if (!value.Type.IsNullableType())
            {
                throw new ArgumentException("value must be nullable type");
            }

            if (!toType.IsNullableType())
            {
                throw new ArgumentException("toType must be nullable type");
            }

            var changeTypeMethod = typeof(Convert).GetMethod("ChangeType", new Type[] { typeof(object), typeof(Type) });
            var callExpressionReturningObject = Expression.Call(changeTypeMethod, Expression.Convert(value, typeof(object)), Expression.Constant(toType));

            return Expression.Convert(callExpressionReturningObject, toType);
        }
        
        /// <summary>
        /// Generates an expression of type Nullable(T), given its underlying value.
        /// </summary>
        public static Expression MakeNewNullable(Type nullabletype, Expression value)
        {
            if (nullabletype == null)
            {
                throw new ArgumentNullException("nullabletype");
            }

            if (value == null)
            {
                throw new ArgumentNullException("value");
            }

            var constructorInfo = nullabletype.GetConstructor(new[] { value.Type });
            if (constructorInfo == null)
            {
                throw new Exception(string.Format("Could not locate constructor on type {0} with a single argument of type {1}",
                    nullabletype.FullName, value.Type.FullName));
            }

            if (value is ConstantExpression expression)
            {
                var constValue = constructorInfo.Invoke(new[] { expression.Value });
                return Expression.Constant(constValue, nullabletype);
            }

            return Expression.New(constructorInfo, value);
        }

        /// <summary>
        /// Generates a default expression of type Nullable(T), given its type.
        /// </summary>
        public static Expression MakeNewNullable(Type nullabletype)
        {
            if (nullabletype == null)
            {
                throw new ArgumentNullException("nullabletype");
            }

            return GetDefaultExpression(nullabletype);
        }

        private static bool TryAdjustNumericType(ParseTreeNode root, Expression expr, Type targetType, out Expression adjusted)
        {
            if (expr.IsNumeric() && Numerics.Contains(targetType))
            {
                var mySize = Marshal.SizeOf(expr.Type);
                var targetSize = Marshal.SizeOf(targetType);

                if (mySize < targetSize)
                {
                    adjusted = ConstantHelper.TryEvalConst(root, expr, ExpressionType.Convert, targetType);
                    return true;
                }

                if (mySize == targetSize)
                {
                    if (ReferenceEquals(targetType, typeof (Single))
                        || ReferenceEquals(targetType, typeof (Double)))
                    {
                        adjusted = ConstantHelper.TryEvalConst(root, expr, ExpressionType.Convert, targetType);
                        return true;
                    }
                }

                // attempt overflow-checked autocast between integer constants, 
                // or from integer to real constants (but never from reals to integers)
                if (!expr.IsRealNumeric() || !targetType.IsInteger())
                {
                    // only attempt to auto-convert constants
                    if (expr is ConstantExpression constExpr)
                    {
                        try
                        {
                            adjusted = ConstantHelper.EvalConst(root, constExpr, ExpressionType.ConvertChecked, targetType);
                            return true;
                        }
                        catch (Exception e)
                        {
                            if (!(e is TargetInvocationException || e is OverflowException))
                            {
                                // something unexpected happened
                                throw;
                            }

                            // we'll get here if a conversion to target type causes overflow
                        }
                    }
                }
            }

            adjusted = null;
            return false;
        }

        /// <summary>
        /// DO NOT REMOVE! Used implicitly, via generic method instantiation.
        /// </summary>
        public static HashSet<T> EnumerateValues<T>(IExpressionEvaluatorRuntime runtime, ParseTreeNode root, CompilerState state)
        {
            if (runtime == null)
            {
                throw new ArgumentNullException("runtime");
            }

            if (root == null)
            {
                throw new ArgumentNullException("root");
            }

            if (state == null)
            {
                throw new ArgumentNullException("state");
            }

            var isString = ReferenceEquals(typeof (T), typeof (string));
            var result = isString ? new HashSet<T>((IEqualityComparer<T>)StringComparer.OrdinalIgnoreCase) : new HashSet<T>();
            
            foreach (var childNode in root.ChildNodes)
            {
                var item = runtime.Analyze(childNode, state);
                item.RequireNonVoid(childNode);

                try
                {
                    result.Add((T)Convert.ChangeType(((ConstantExpression)item).Value, typeof(T)));
                }
                catch
                {
                    throw new CompilationException(
                        String.Format(
                            "All items in the IN arguments list must be constants of type compatible with {0}. Actual expression found: {1}, of type {2}",
                            typeof(T).FullName, item.NodeType, item.Type), childNode);
                }
            }

            return result;
        }

        /// <summary>
        /// Automatically drill down the nested levels of parentheses wrapping a single expression or expression list.
        /// Used to handle cases like ((1)), (1,2), (((('a', 'b')))).
        /// </summary>
        /// <param name="root">The root no to start with</param>
        /// <returns>Some lower-level non-tuple node</returns>
        public static ParseTreeNode UnwindTupleExprList(ParseTreeNode root)
        {
            if (root == null)
            {
                throw new ArgumentNullException("root");
            }

            if (root.ChildNodes.Count != 1)
            {
                return root;
            }

            var child = root.ChildNodes[0];
            return child.Term.Name == "tuple" || child.Term.Name == "exprList" ? UnwindTupleExprList(child) : root;
        }

        /// <summary>
        /// If one of the arguments is a void expression, will automatically replace it with default of another expression's type.
        /// Returns false if both arguments are void.
        /// </summary>
        public static bool TryAdjustVoid(ref Expression x, ref Expression y)
        {
            if (x == null)
            {
                throw new ArgumentNullException("x");
            }
            
            if (y == null)
            {
                throw new ArgumentNullException("y");
            }

            if (x.IsVoid())
            {
                if (y.IsVoid())
                {
                    return true;
                }

                x = GetDefaultExpression(y.Type.GetUnderlyingType());
            }
            else if (y.IsVoid())
            {
                y = GetDefaultExpression(x.Type.GetUnderlyingType());
            }

            return true;
        }

        /// <summary>
        /// If one of the arguments is a nullable expression, and second is not, will try to cast second argument to nullable
        /// Returns false if both arguments are not nullable.
        /// </summary>
        public static bool TryEnsureBothNullable(ref Expression x, ref Expression y)
        {
            if (x == null)
            {
                throw new ArgumentNullException("x");
            }

            if (y == null)
            {
                throw new ArgumentNullException("y");
            }

            if (x.IsNullableType() && !y.IsNullableType())
            {
                y = MakeNewNullable(y);
            }
            else if (!x.IsNullableType() && y.IsNullableType())
            {
                x = MakeNewNullable(x);
            }

            return x.IsNullableType();
        }

        /// <summary>
        /// Try to convert Nullable<T> to Nullable<K> if possible.
        /// Returns false if conversion is not possible.
        /// </summary>
        public static bool TryAdjustNullable(ref Expression x, ref Expression y)
        {
            if (x == null)
            {
                throw new ArgumentNullException("x");
            }

            if (y == null)
            {
                throw new ArgumentNullException("y");
            }

            if (!x.IsNullableType())
            {
                throw new ArgumentException("x is not nullable");
            }

            if (!y.IsNullableType())
            {
                throw new ArgumentException("y is not nullable");
            }

            if (x.Type.IsExplicitCastRequired(y.Type))
            {
                x = ConvertNullable(x, y.Type);
            }
            else if (y.Type.IsExplicitCastRequired(x.Type))
            {
                y = ConvertNullable(y, x.Type);
            }
            else
            {
                return false;
            }

            return true;
        }

        public static Expression ForEach<TSource>(this Expression enumerable, Expression loopContent)
        {
            var enumerableType = enumerable.Type;
            var getEnumerator = enumerableType.GetMethod("GetEnumerator");
            if (getEnumerator is null)
            {
                getEnumerator = typeof(IEnumerable<>).MakeGenericType(typeof(TSource)).GetMethod("GetEnumerator");
            }

            var enumeratorType = getEnumerator.ReturnType;
            var enumerator = Expression.Variable(enumeratorType, "enumerator");

            return Expression.Block(new[] { enumerator },
                Expression.Assign(enumerator, Expression.Call(enumerable, getEnumerator)),
                EnumerationLoop(enumerator, loopContent));
        }

        public static Expression ForEach<TSource>(this Expression enumerable, ParameterExpression loopVar, Expression loopContent)
        {
            var enumerableType = enumerable.Type;
            var getEnumerator = enumerableType.GetMethod("GetEnumerator");
            if (getEnumerator is null)
            {
                getEnumerator = typeof(IEnumerable<>).MakeGenericType(typeof(TSource)).GetMethod("GetEnumerator");
            }

            var enumeratorType = getEnumerator.ReturnType;
            var enumerator = Expression.Variable(enumeratorType, "enumerator");

            return Expression.Block(new[] { enumerator },
                Expression.Assign(enumerator, Expression.Call(enumerable, getEnumerator)),
                EnumerationLoop(enumerator,
                    Expression.Block(new[] { loopVar },
                        Expression.Assign(loopVar, Expression.Property(enumerator, "Current")),
                        loopContent)));
        }

        static Expression EnumerationLoop(this ParameterExpression enumerator, Expression loopContent)
        {
            var loop = While(
                Expression.Call(enumerator, typeof(IEnumerator).GetMethod("MoveNext")),
                loopContent);

            var enumeratorType = enumerator.Type;
            if (typeof(IDisposable).IsAssignableFrom(enumeratorType))
            {
                return Using(enumerator, loop);
            }

            if (!enumeratorType.IsValueType)
            {
                var disposable = Expression.Variable(typeof(IDisposable), "disposable");
                return Expression.TryFinally(
                    loop,
                    Expression.Block(new[] { disposable },
                        Expression.Assign(disposable, Expression.TypeAs(enumerator, typeof(IDisposable))),
                        Expression.IfThen(
                            Expression.NotEqual(disposable, Expression.Constant(null)),
                            Expression.Call(disposable, typeof(IDisposable).GetMethod("Dispose")))));
            }

            return loop;
        }

        public static Expression Using(this ParameterExpression variable, Expression content)
        {
            var variableType = variable.Type;

            if (!typeof(IDisposable).IsAssignableFrom(variableType))
            {
                throw new Exception($"'{variableType.FullName}': type used in a using statement must be implicitly convertible to 'System.IDisposable'");
            }

            var getMethod = typeof(IDisposable).GetMethod("Dispose");

            if (variableType.IsValueType)
            {
                return Expression.TryFinally(
                    content,
                    Expression.Call(Expression.Convert(variable, typeof(IDisposable)), getMethod));
            }

            if (variableType.IsInterface)
            {
                return Expression.TryFinally(
                    content,
                    Expression.IfThen(
                        Expression.NotEqual(variable, Expression.Constant(null)),
                        Expression.Call(variable, getMethod)));
            }

            return Expression.TryFinally(
                content,
                Expression.IfThen(
                    Expression.NotEqual(variable, Expression.Constant(null)),
                    Expression.Call(Expression.Convert(variable, typeof(IDisposable)), getMethod)));
        }

        public static Expression While(this Expression loopCondition, Expression loopContent)
        {
            var breakLabel = Expression.Label();
            return Expression.Loop(
                Expression.IfThenElse(
                    loopCondition,
                    loopContent,
                    Expression.Break(breakLabel)),
                breakLabel);
        }
    }
}