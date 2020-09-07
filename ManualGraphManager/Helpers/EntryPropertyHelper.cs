using EntityFrameworkCore.GraphManager.ManualGraphManager.Helpers.Abstract;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System;

namespace EntityFrameworkCore.GraphManager.ManualGraphManager.Helpers
{
    internal class EntryPropertyHelper<TProperty>
        : IEntryPropertyHelper<TProperty>
    {
        private PropertyEntry EntryProperty { get; set; }

        /// <summary>
        /// EntryProperty helper to work on property of entry.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// When entryPropertyParam is null.
        /// </exception>
        /// <param name="entryPropertyParam">Entry proeprty to work on.</param>
        public EntryPropertyHelper(PropertyEntry entryPropertyParam)
        {
            if (entryPropertyParam == null)
                throw new ArgumentNullException("entryPropertyParam");

            EntryProperty = entryPropertyParam;
        }

        /// <summary>
        /// Get or set if property is modified.
        /// </summary>
        public bool IsModified
        {
            get { return EntryProperty.IsModified; }
            set { EntryProperty.IsModified = value; }
        }

        /// <summary>
        /// Current value of property entry.
        /// </summary>
        public TProperty Value
        {
            get { return (TProperty)EntryProperty.CurrentValue; }
        }
    }
}
