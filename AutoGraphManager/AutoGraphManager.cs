using EntityFrameworkCore.GraphManager.AutoGraphManager.Abstract;
using Microsoft.EntityFrameworkCore;
using System;

namespace EntityFrameworkCore.GraphManager.AutoGraphManager
{
    public class AutoGraphManager
        : IAutoGraphManager
    {
        internal DbContext Context { get; set; }

        public AutoGraphManager(DbContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            Context = context;
        }
    }
}
