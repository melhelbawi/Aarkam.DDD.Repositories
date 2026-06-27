using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Aarkam.Domain.Repositories
{
    /// <summary>
    /// Defines a specification for filtering and shaping queries for entities of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of the entity.</typeparam>
    public interface ISpecification<T>
    {
        /// <summary>
        /// Checks if the provided entity satisfies the specification criteria.
        /// </summary>
        /// <param name="entity">The entity to check.</param>
        /// <returns>True if satisfied, false otherwise.</returns>
        bool IsSatisfiedBy(T entity);

        /// <summary>
        /// Returns an expression that represents the specification criteria.
        /// </summary>
        Expression<Func<T, bool>> ToExpression();

        /// <summary>
        /// Gets the list of navigation properties to include in the query.
        /// </summary>
        List<Expression<Func<T, object>>> Includes { get; }

        /// <summary>
        /// Gets the expression for ascending sort order.
        /// </summary>
        Expression<Func<T, object>> OrderBy { get; }

        /// <summary>
        /// Gets the expression for descending sort order.
        /// </summary>
        Expression<Func<T, object>> OrderByDescending { get; }

        /// <summary>
        /// Gets the number of items to take (for paging).
        /// </summary>
        int? Take { get; }

        /// <summary>
        /// Gets the number of items to skip (for paging).
        /// </summary>
        int? Skip { get; }
    }

    /// <summary>
    /// Base class for implementing specifications with optional includes, ordering, and paging.
    /// </summary>
    /// <typeparam name="T">The type of the entity.</typeparam>
    public abstract class SpecificationBase<T> : ISpecification<T>
    {
        /// <inheritdoc />
        public List<Expression<Func<T, object>>> Includes { get; set; }

        /// <inheritdoc />
        public Expression<Func<T, object>> OrderBy { get; set; }

        /// <inheritdoc />
        public Expression<Func<T, object>> OrderByDescending { get; set; }

        /// <inheritdoc />
        public int? Take { get; set; }

        /// <inheritdoc />
        public int? Skip { get; set; }

        /// <inheritdoc />
        public abstract Expression<Func<T, bool>> ToExpression();

        /// <inheritdoc />
        public bool IsSatisfiedBy(T entity)
        {
            Func<T, bool> predicate = ToExpression().Compile();
            return predicate(entity);
        }

        /// <summary>
        /// Combines this specification with another using a logical AND.
        /// </summary>
        public ISpecification<T> And(ISpecification<T> specification)
        {
            return new AndSpecification<T>(this, specification);
        }

        /// <summary>
        /// Combines this specification with another using a logical OR.
        /// </summary>
        public ISpecification<T> Or(ISpecification<T> specification)
        {
            return new OrSpecification<T>(this, specification);
        }

        /// <summary>
        /// Negates this specification with a logical NOT.
        /// </summary>
        public ISpecification<T> Not()
        {
            return new NotSpecification<T>(this);
        }
    }

    internal class AndSpecification<T> : SpecificationBase<T>
    {
        private readonly ISpecification<T> _left;
        private readonly ISpecification<T> _right;

        public AndSpecification(ISpecification<T> left, ISpecification<T> right)
        {
            _left = left;
            _right = right;
        }

        public override Expression<Func<T, bool>> ToExpression()
        {
            var leftExpression = _left.ToExpression();
            var rightExpression = _right.ToExpression();

            var parameter = Expression.Parameter(typeof(T));
            var combined = Expression.AndAlso(
                Expression.Invoke(leftExpression, parameter),
                Expression.Invoke(rightExpression, parameter)
            );

            return Expression.Lambda<Func<T, bool>>(combined, parameter);
        }
    }

    internal class OrSpecification<T> : SpecificationBase<T>
    {
        private readonly ISpecification<T> _left;
        private readonly ISpecification<T> _right;

        public OrSpecification(ISpecification<T> left, ISpecification<T> right)
        {
            _left = left;
            _right = right;
        }

        public override Expression<Func<T, bool>> ToExpression()
        {
            var leftExpression = _left.ToExpression();
            var rightExpression = _right.ToExpression();

            var parameter = Expression.Parameter(typeof(T));
            var combined = Expression.OrElse(
                Expression.Invoke(leftExpression, parameter),
                Expression.Invoke(rightExpression, parameter)
            );

            return Expression.Lambda<Func<T, bool>>(combined, parameter);
        }
    }

    internal class NotSpecification<T> : SpecificationBase<T>
    {
        private readonly ISpecification<T> _specification;

        public NotSpecification(ISpecification<T> specification)
        {
            _specification = specification;
        }

        public override Expression<Func<T, bool>> ToExpression()
        {
            var expression = _specification.ToExpression();
            var parameter = Expression.Parameter(typeof(T));
            var negated = Expression.Not(Expression.Invoke(expression, parameter));

            return Expression.Lambda<Func<T, bool>>(negated, parameter);
        }
    }
}
