using Microsoft.EntityFrameworkCore;
using System;
using System.Linq.Expressions;

namespace EntityFrameworkCore.GraphManager.ManualGraphManager.Helpers.Abstract
{
    public interface IEntryHelper<T>
       where T : class
    {
        EntityState State { get; set; }
        IEntryPropertyHelper<TProperty> Property<TProperty>(
            Expression<Func<T, TProperty>> propertyLambda);
    }
}
