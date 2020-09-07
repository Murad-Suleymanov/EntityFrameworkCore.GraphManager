﻿using EntityFrameworkCore.GraphManager.AutoGraphManager.Helpers;
using EntityFrameworkCore.GraphManager.ManualGraphManager.Abstract;
using EntityFrameworkCore.GraphManager.ManualGraphManager.Helpers;
using EntityFrameworkCore.GraphManager.ManualGraphManager.Helpers.Abstract;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System;
using System.Collections.Generic;
using System.Text;

namespace EntityFrameworkCore.GraphManager.ManualGraphManager
{
    /// <summary>
    /// Manage graphs manually after automatic state define.
    /// </summary>
    public class ManualGraphManager
        : IManualGraphManager
    {
        private Lazy<ContextHelper> lazyContextHelper;

        private DbContext Context { get; set; }

        /// <summary>
        /// Constructor with context.
        /// </summary>
        /// <param name="contextParam">DbContext.</param>
        internal ManualGraphManager(DbContext contextParam)
        {
            if (contextParam == null)
                throw new ArgumentNullException("contextParam");

            Context = contextParam;
            lazyContextHelper = new Lazy<ContextHelper>(
                () => new ContextHelper(contextParam));
        }

        private ContextHelper ContextHelper
        {
            get { return lazyContextHelper.Value; }
        }

        /// <summary>
        /// Get Entry relevant to entity.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// When entity is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// When no entry was found according to entity.
        /// </exception>
        /// <typeparam name="TEntity">Type of entity.</typeparam>
        /// <param name="entity">Entity to get entry relevant to.</param>
        /// <returns>Entry helper to be able to work on entry.</returns>
        public IEntryHelper<TEntity> Entry<TEntity>(TEntity entity)
            where TEntity : class
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            EntityEntry entry = Context.Entry(entity);

            if (entry == null)
                throw new ArgumentException(string.Format(
                    "No entry was found relevant to entity of type '{0}'.",
                    entity.GetType().Name));

            return new EntryHelper<TEntity>(entry);
        }
    }

    /// <summary>
    /// Manage graphs manually after automatic state define.
    /// </summary>
    public class ManualGraphManager<T>
        : ManualGraphManager, IManualGraphManager<T>
        where T : class
    {
        /// <summary>
        /// List of entities to manipulate.
        /// </summary>
        public List<T> EntityCollection { get; set; }

        /// <summary>
        /// Constructor with context.
        /// </summary>
        /// <param name="contextParam">DbContext.</param>
        internal ManualGraphManager(DbContext contextParam)
            : base(contextParam)
        {
        }
    }
}
