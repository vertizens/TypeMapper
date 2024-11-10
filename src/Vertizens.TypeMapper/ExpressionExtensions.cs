﻿using System.Linq.Expressions;

namespace Vertizens.TypeMapper;
public static class ExpressionExtensions
{
    public static Expression<Func<TSource, TTarget>> Union<TSource, TTarget>(this Expression<Func<TSource, TTarget>> projection1, Expression<Func<TSource, TTarget>> projection2)
        where TTarget : class, new()
    {
        if (projection1.Body.NodeType == ExpressionType.MemberInit && projection2.Body.NodeType == ExpressionType.MemberInit)
        {
            var bindings1 = ((MemberInitExpression)projection1.Body).Bindings;
            var bindings2 = ((MemberInitExpression)projection2.Body).Bindings;
            if (bindings1.All(x => x.BindingType == MemberBindingType.Assignment) && bindings2.All(x => x.BindingType == MemberBindingType.Assignment))
            {
                var projection2ReplacedBody = ReplaceParameterExpressionVisitor.ReplaceParameter(projection2.Body, projection2.Parameters[0], projection1.Parameters[0]);
                var bindingsByMember1 = bindings1.ToDictionary(x => x.Member, x => (MemberAssignment)x);
                IList<MemberAssignment> newAssignments = [];
                foreach (var binding in ((MemberInitExpression)projection2ReplacedBody).Bindings)
                {
                    if (bindingsByMember1.TryGetValue(binding.Member, out var existingBinding))
                    {
                        bindingsByMember1[binding.Member] = (MemberAssignment)binding;
                    }
                    else
                    {
                        newAssignments.Add((MemberAssignment)binding);
                    }
                }

                var allMemberAssignments = bindingsByMember1.Values.Concat(newAssignments);
                var memberInit = Expression.MemberInit(Expression.New(typeof(TTarget)), allMemberAssignments);

                return Expression.Lambda<Func<TSource, TTarget>>(memberInit, projection1.Parameters);
            }
            else
            {
                throw new ArgumentException("Both body expressions must use Assignment member bindings only");
            }
        }
        else
        {
            throw new ArgumentException("Both body expressions need to be MemberInit nodes");
        }
    }
}