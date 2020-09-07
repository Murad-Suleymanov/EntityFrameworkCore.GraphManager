﻿using System;
using System.Linq.Expressions;
using System.Reflection;
using EntityFrameworkCore.GraphManager.ManualGraphManager.Helpers.Abstract;
using Ma.ExtensionMethods.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace EntityFrameworkCore.GraphManager.ManualGraphManager.Helpers
{
    internal class EntryHelper<T> : IEntryHelper<T>
       where T : class
    {
        private EntityEntry Entry { get; set; }

        /// <summary>
        /// Entry helper to manually manage Entry.
        /// </summary>
        /// <param name="entryParam"></param>
        internal EntryHelper(EntityEntry entryParam)
        {
            if (entryParam == null)
                throw new ArgumentNullException("entryParam");

            Entry = entryParam;
        }

        /// <summary>
        /// Get or set state of entry.
        /// </summary>
        public EntityState State
        {
            get { return Entry.State; }
            set { Entry.State = value; }
        }

        /// <summary>
        /// Get PropertyEntry of source to work on.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// When propertyLambda is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// When propertyLambda does not select a proeprty
        /// or when Entry has no relevant property.
        /// </exception>
        /// <typeparam name="TProperty">Type of property.</typeparam>
        /// <param name="propertyLambda">Lambda expression to get property.</param>
        /// <returns>Entry property helper to be able to work on property.</returns>
        public IEntryPropertyHelper<TProperty> Property<TProperty>(
            Expression<Func<T, TProperty>> propertyLambda)
        {
            if (propertyLambda == null)
                throw new ArgumentNullException("propertyLambda");

            PropertyInfo property = propertyLambda.GetPropertyInfo();

            if (property == null)
                throw new ArgumentException(string.Format(
                    "Expression '{0}' does not select any property.",
                    propertyLambda.ToString()));

            PropertyEntry propertyEntry = Entry.Property(property.Name);

            if (propertyEntry == null)
                throw new ArgumentException(string.Format(
                    "Entry does not have property with '{0}' name.",
                    property.Name));

            EntryPropertyHelper<TProperty> helper =
                new EntryPropertyHelper<TProperty>(propertyEntry);
            return helper;
        }
    }
}
