using System;
using System.Collections.Generic;
using System.Text;

namespace EntityFrameworkCore.GraphManager.AutoGraphManager.Helpers.Abstract
{
    interface IContextFactory
    {
        IContextHelper GetContextHelper();
        IGraphEntityManager<TEntity> GetEntityManager<TEntity>()
            where TEntity : class;
        IGraphEntityTypeManager GetEntityTypeManager(string typeName);
    }
}
