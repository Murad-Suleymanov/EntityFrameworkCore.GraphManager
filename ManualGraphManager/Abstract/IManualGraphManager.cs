using EntityFrameworkCore.GraphManager.ManualGraphManager.Helpers.Abstract;
using System;
using System.Collections.Generic;
using System.Text;

namespace EntityFrameworkCore.GraphManager.ManualGraphManager.Abstract
{
    public interface IManualGraphManager
    {
        /// <summary>
        /// Get Entry relevant to entity.
        /// </summary>
        /// <typeparam name="TEntity">Type of entity.</typeparam>
        /// <param name="entity">Entity to get entry relevant to.</param>
        /// <returns>Entry helper to be able to work on entry.</returns>
        IEntryHelper<TEntity> Entry<TEntity>(TEntity entity)
            where TEntity : class;
    }

    public interface IManualGraphManager<T>
        : IManualGraphManager
        where T : class
    {
        /// <summary>
        /// List of entities to manipulate.
        /// </summary>
        List<T> EntityCollection { get; set; }
    }
}
