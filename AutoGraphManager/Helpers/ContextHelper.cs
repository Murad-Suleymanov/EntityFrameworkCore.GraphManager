﻿using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EntityFrameworkCore.GraphManager.AutoGraphManager.Helpers
{
    internal class ContextHelper
        : IContextHelper, IContextFactory
    {
        public DbContext Context { get; set; }
        public HelperStore Store { get; private set; }

        private List<RelationshipDetail> ForeignKeyDetails { get; set; }

        public ContextHelper(DbContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            Context = context;

            // Initialize store
            Store = new HelperStore();
        }

        /// <summary>
        /// Get object context associated with context.
        /// </summary>
        /// <returns>Object context for context.</returns>
        public ObjectContext ObjectContext
        {
            get { return ((IObjectContextAdapter)Context).ObjectContext; }
        }

        /// <summary>
        /// Get navigation details of context.
        /// </summary>
        /// <returns>Navigation details of context.</returns>
        public IEnumerable<NavigationDetail> GetNavigationDetails()
        {
            IEnumerable<NavigationDetail> navigationDetails = ObjectContext
                .MetadataWorkspace
                .GetItems<EntityType>(DataSpace.CSpace)
                .Select(n => new NavigationDetail(n));

            return navigationDetails;
        }

        /// <summary>
        /// Get foreign key details.
        /// </summary>
        /// <returns>Foreign key details.</returns>
        public List<RelationshipDetail> GetForeignKeyDetails()
        {
            if (ForeignKeyDetails == null)
                ForeignKeyDetails = ObjectContext
                    .MetadataWorkspace
                    .GetItems<AssociationType>(DataSpace.CSpace)
                    .Where(m => m.Constraint != null)
                    .Select(m => new RelationshipDetail(m.Constraint))
                    .ToList();

            return ForeignKeyDetails;
        }

        /// <summary>
        /// Get the uppermost principal parent of entity.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// When entity is null
        /// </exception>
        /// <typeparam name="TEntity">Type of entity.</typeparam>
        /// <param name="entity">Entity to get parent.</param>
        /// <returns>Uppermost principal parent of entity.</returns>
        public object GetUppermostPrincipalParent<TEntity>(TEntity entity)
            where TEntity : class
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            // Try to get from store
            if (Store.UppermostPrincipalParent.ContainsKey(entity))
                return Store.UppermostPrincipalParent[entity];

            object currentUppermostPrincipal = entity;
            List<object> parents = null;

            do
            {
                parents = GetParents(currentUppermostPrincipal, true)
                    .ToList();

                if (parents != null
                        && parents.Count > 0)
                    currentUppermostPrincipal = parents.FirstOrDefault();
            }
            while (parents != null
                && parents.Count() > 0);

            // Add to store
            Store.UppermostPrincipalParent.Add(entity, currentUppermostPrincipal);
            return currentUppermostPrincipal;
        }

        /// <summary>
        /// Get uppermost parent entity which contains this entity
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// When entity is null
        /// </exception>
        /// <typeparam name="TEntity">Type of entity</typeparam>
        /// <param name="entity">Entity to get uppermost parent</param>
        /// <returns>Uppermost parents</returns>
        public object GetUppermostParent<TEntity>(TEntity entity)
            where TEntity : class
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            // Try to get from store
            if (Store.UppermostParent.ContainsKey(entity))
                return Store.UppermostParent[entity];

            List<object> result = new List<object>();
            object currentUppermostParent = entity;

            List<object> parents = GetParents(currentUppermostParent, false)
                .ToList();

            while (parents != null
                && parents.Count() > 0)
            {
                if (parents != null
                        && parents.Count == 1)
                    currentUppermostParent = parents.FirstOrDefault();

                parents = parents.SelectMany(m => GetParents(m, false)).ToList();
            }

            // Add to store
            Store.UppermostParent.Add(entity, currentUppermostParent);
            return currentUppermostParent;
        }

        /// <summary>
        /// Get parents of entity which contains this entity
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// When entity is null
        /// </exception>
        /// <typeparam name="TEntity">Type of entity.</typeparam>
        /// <param name="entity">Entity to get parent.</param>
        /// <param name="onlyPrincipal">Get only one-to-one parent of entity</param>
        /// <returns>Principal parent of entity.</returns>
        public IEnumerable<object> GetParents<TEntity>(
            TEntity entity,
            bool onlyPrincipal)
            where TEntity : class
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            string typeName = entity.GetType().Name;
            List<RelationshipMultiplicity> principalRelationshipMultiplicity =
                new List<RelationshipMultiplicity>()
                {
                    RelationshipMultiplicity.One,
                    RelationshipMultiplicity.ZeroOrOne
                };

            IGraphEntityTypeManager graphEntityTypeMangeer = GetEntityTypeManager(typeName);
            NavigationDetail navigationDetailOfCurrent = graphEntityTypeMangeer
                .GetNavigationDetail();

            // Get only those parent property navigation details
            // which has navigation property to this entity
            var parentNavigationDetails = navigationDetailOfCurrent
                .Relations
                .Where(r => r.Direction == NavigationDirection.From)
                .Select(r =>
                {
                    IGraphEntityTypeManager typeManager =
                        GetEntityTypeManager(r.PropertyTypeName);
                    return new
                    {
                        SourceTypeName = r.PropertyTypeName,
                        Relation = typeManager
                             .GetNavigationDetail()
                             .Relations
                             .FirstOrDefault(pr =>
                                 pr.PropertyTypeName.Equals(typeName)
                                 && pr.SourceMultiplicity == r.TargetMultiplicity
                                 && pr.TargetMultiplicity == r.SourceMultiplicity
                                 && pr.ToKeyNames.SequenceEqual(r.ToKeyNames))
                    };
                })
                .Where(r => r.Relation != null);

            if (onlyPrincipal)
                parentNavigationDetails = parentNavigationDetails
                    .Where(r => principalRelationshipMultiplicity
                        .Contains(r.Relation.TargetMultiplicity));

            List<string> parentPropertyNames = navigationDetailOfCurrent
                .Relations
                .Where(r => parentNavigationDetails.Any(p =>
                    p.SourceTypeName == r.PropertyTypeName
                    && p.Relation.SourceMultiplicity == r.TargetMultiplicity
                    && p.Relation.TargetMultiplicity == r.SourceMultiplicity
                    && p.Relation.ToKeyNames.SequenceEqual(r.ToKeyNames)))
                .Select(r => r.PropertyName)
                .ToList();

            if (parentPropertyNames != null
                && parentPropertyNames.Count > 0)
            {
                foreach (string propertyName in parentPropertyNames)
                {
                    object parent = entity.GetPropertyValue(propertyName);
                    if (parent != null)
                        yield return parent;
                }
            }
        }

        /// <summary>
        /// Find duplicate entities in the local context, perform needed operations
        /// to be able insert or update value appropriately and detach duplicates.
        /// </summary>
        /// <typeparam name="TEntity">Type of entity.</typeparam>
        /// <param name="entity">Entity to find duplicates of.</param>
        private void DealWithDuplicates<TEntity>(
            TEntity entity)
            where TEntity : class
        {
            IGraphEntityManager<TEntity> graphEntityManager = GetEntityManager<TEntity>();
            Expression<Func<TEntity, bool>> filterExpression =
                graphEntityManager.ConstructFilterExpression(entity, FilterType.IdOrUnique);


            bool duplicatesFoundAndEliminatedFlag = false;
            if (filterExpression != null)
            {
                /// This is used instead of Context.Set<TEntity>().Local
                /// to improve perfermance.
                var duplicateEntityFromLocal = Context
                    .ChangeTracker
                    .Entries<TEntity>()
                    .Select(m => m.Entity)
                    .Where(filterExpression.Compile())
                    .ToList();

                if (duplicateEntityFromLocal.Any())
                {
                    dynamic uppermostPrincipalParentEntity =
                        GetUppermostPrincipalParent(entity);

                    foreach (TEntity duplicate in duplicateEntityFromLocal.Where(d => d != entity))
                    {
                        dynamic uppermostPrincipalParentDuplicate =
                            GetUppermostPrincipalParent(duplicate);

                        // If duplicate value is not in a collection
                        // we need to replace its duplicate value with 
                        // original one in parent entity. Becasue as we detach the 
                        // duplicate entry afterwards, not setting its value with original
                        // one will not send it to database where it has to be sent
                        ReplaceEntitiesInParents(
                            uppermostPrincipalParentDuplicate,
                            uppermostPrincipalParentEntity);

                        /*
                         * ******************************************************
                         * TO DO: ALSO CONSIDER WHEN ONE DUPLICATE IS IN A 
                         * NON-COLLECTION ENTITY AND ANOTHER IS IN A COLLECTION
                         * ******************************************************
                        */

                        DetachWithDependants(uppermostPrincipalParentDuplicate, true);
                    }

                    duplicatesFoundAndEliminatedFlag = true;
                }
            }

            // If no duplicaes has been found, get uppermost principal
            // parent entity and if it is not entity itself, try to find
            // and eliminate duplicates of parent
            if (!duplicatesFoundAndEliminatedFlag)
            {
                dynamic uppermostPrincipalParentEntity =
                        GetUppermostPrincipalParent(entity);

                if (!uppermostPrincipalParentEntity.Equals(entity))
                {
                    DealWithDuplicates(uppermostPrincipalParentEntity);
                }
            }
        }

        /// <summary>
        /// Set property of parent entity to targetValue 
        /// which has property value equals to currentValue.
        /// </summary>
        /// <typeparam name="TEntity">Type of entity.</typeparam>
        /// <param name="currentValue">Current value of property to replace.</param>
        /// <param name="targetValue">Target value to set value of property of parent.</param>
        private void ReplaceEntitiesInParents<TEntity>(
            TEntity currentValue,
            TEntity targetValue)
            where TEntity : class
        {
            IGraphEntityManager<TEntity> graphEntityManager = GetEntityManager<TEntity>();
            NavigationDetail navigationDetailOfCurrent = graphEntityManager
                .GetNavigationDetail();

            // Get parent properties of entity.
            // Properties which have navigation property type of which
            // is type of entity. Ignore navigation properties
            // which are mutual navigation properties with entity itself.     
            string typeName = currentValue.GetType().Name;
            List<NavigationDetail> parentNavigationDetails = GetNavigationDetails()
                .Select(n => new NavigationDetail()
                {
                    SourceTypeName = n.SourceTypeName,
                    Relations = n.Relations
                        .Where(r => r.PropertyTypeName.Equals(typeName)
                                   && r.SourceMultiplicity == RelationshipMultiplicity.Many
                                   && !navigationDetailOfCurrent
                                        .Relations
                                        .Any(c => c.PropertyTypeName.Equals(n.SourceTypeName)
                                                && c.TargetMultiplicity == r.SourceMultiplicity
                                                && c.FromKeyNames.SequenceEqual(r.FromKeyNames)
                                                && c.ToKeyNames.SequenceEqual(r.ToKeyNames)))
                        .ToList()
                })
                .Where(n => n.Relations != null
                    && n.Relations.Count() > 0)
                .ToList();

            // Get assembly to be able to get types according to type name
            Assembly entityAssembly = currentValue.GetType().Assembly;

            if (parentNavigationDetails != null && parentNavigationDetails.Count() > 0)
            {
                foreach (NavigationDetail parentNavigation in parentNavigationDetails)
                {
                    Type parentType = entityAssembly.GetTypes()
                        .FirstOrDefault(t => t.Name.Equals(parentNavigation.SourceTypeName));

                    // Get local set of parent
                    IEnumerable<object> localParentSet = Context
                        .Set(parentType)
                        .Local
                        .CastToGeneric();

                    foreach (NavigationRelation navigationRelation in parentNavigation.Relations)
                    {
                        PropertyInfo childProperty =
                            parentType.GetProperty(navigationRelation.PropertyName);

                        if (!childProperty.PropertyType.IsCollectionType())
                        {
                            // Get all parent entities which have current entity inside
                            var containerParentCollection = localParentSet
                                .Where(m => m.GetPropertyValue(navigationRelation.PropertyName) != null
                                    && m.GetPropertyValue(navigationRelation.PropertyName).Equals(currentValue))
                                .ToList();

                            // If collection is empty then skip.
                            if (containerParentCollection == null
                                || containerParentCollection.Count == 0)
                                continue;

                            foreach (var containerParent in containerParentCollection)
                            {
                                // If parent is null then skip.
                                if (containerParent == null)
                                    continue;

                                // If parent with property value of currentValue found replace values
                                /*
                                 * If duplicate entity is in the entity, state of which has already
                                 * been defined and if state of this parent entity is Unchanged or
                                 * Modified, trying to change navigation property of this parent entity
                                 * will throw InvalidOperationException with following message:
                                 * "A referential integrity constraint violation occurred: 
                                 *  A primary key property that is a part of referential integrity constraint 
                                 *  cannot be changed when the dependent object is Unchanged unless it is being 
                                 *  set to the association's principal object. The principal object must 
                                 *  be tracked and not marked for deletion."
                                 * As a workaround I am storing current state of parent entity, changing the
                                 * state to Added, then replacing duplicate entity and in the end
                                 * I set state of parent entity to stored current state.
                                */

                                // Store current state
                                var currentState = Context.Entry(containerParent).State;
                                // Change state to added
                                Context.Entry(containerParent).State = EntityState.Added;

                                // Replace value through 
                                // context.Entry(containerParent).Member(propertyName).CurrentValue
                                // because otherwise EntityFramework will not be able to track entities
                                Context.Entry(containerParent)
                                    .Member(navigationRelation.PropertyName).CurrentValue = targetValue;

                                // Restore state to current state
                                Context.Entry(containerParent).State = currentState;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Detach dependants of entity.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// When  entity is null
        /// </exception>
        /// <typeparam name="TEntity">Type of entity.</typeparam>
        /// <param name="entity">Entity to detach dependants.</param>
        /// <param name="detachItself">Also detach entity itself.</param>
        public void DetachWithDependants<TEntity>(
            TEntity entity,
            bool detachItself)
            where TEntity : class
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            string typeName = entity.GetType().Name;
            IGraphEntityTypeManager graphEntityTypeManager =
                GetEntityTypeManager(typeName);
            List<string> dependantPropertyTypes = graphEntityTypeManager
                .GetForeignKeyDetails()
                .Where(r => r.FromDetails
                               .ContainerClass
                               .Equals(typeName))
                .Select(r => r.ToDetails.ContainerClass)
                .ToList();

            List<PropertyInfo> dependantProperties = entity
                .GetType()
                .GetProperties()
                .Where(p => dependantPropertyTypes
                                .Contains(p.PropertyType.GetUnderlyingType().Name))
                .ToList();

            if (dependantProperties != null
                            && dependantProperties.Count > 0)
            {
                foreach (PropertyInfo childEntityProperty in dependantProperties)
                {
                    if (childEntityProperty.PropertyType.IsCollectionType())
                    {
                        // If child entity is collection detach all entities inside this collection
                        IEnumerable<object> enumerableChildEntity =
                                    ReflectionExtensions.GetPropertyValue(entity, childEntityProperty.Name)
                                    as IEnumerable<object>;

                        if (enumerableChildEntity != null)
                        {
                            foreach (dynamic childEntity in enumerableChildEntity.ToList())
                            {
                                if (childEntity != null)
                                    DetachWithDependants(childEntity, true);
                            }
                        }
                    }
                    else
                    {
                        // If child entity is not collection define state of its own                        
                        dynamic childEntity =
                            ReflectionExtensions.GetPropertyValue(entity, childEntityProperty.Name);

                        if (childEntity != null)
                            DetachWithDependants(childEntity, true);
                    }

                }
            }

            if (detachItself)
                Context.Entry(entity).State = EntityState.Detached;
        }

        /// <summary>
        /// Add all child or parents entities related to given entity
        /// and entity itself to the relatedEntityList.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// When entity or relatedEntityList is null.
        /// </exception>
        /// <typeparam name="TEntity">Type of entity.</typeparam>
        /// <param name="entity">Entity to get all related entities.</param>
        /// <param name="relatedEntityList">List of entities to add.</param>
        public void GetAllEntities<TEntity>(
            TEntity entity,
            List<object> relatedEntityList)
            where TEntity : class
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (relatedEntityList == null)
                throw new ArgumentNullException(nameof(relatedEntityList));

            if (!relatedEntityList.Contains(entity))
                relatedEntityList.Add(entity);

            IGraphEntityManager<TEntity> graphEntityManager = GetEntityManager<TEntity>();
            NavigationDetail navigationDetail = graphEntityManager
                .GetNavigationDetail();

            List<PropertyInfo> navigationPropeties = navigationDetail
                .Relations
                .Select(n => entity.GetProperty(n.PropertyName))
                .ToList();

            if (navigationPropeties != null
                        && navigationPropeties.Count > 0)
            {
                foreach (PropertyInfo childEntityProperty in navigationPropeties)
                {
                    if (childEntityProperty.PropertyType.IsCollectionType())
                    {
                        // If child entity is collection get all entities inside this collection.
                        IEnumerable<object> enumerableChildEntity =
                                    entity.GetPropertyValue(childEntityProperty.Name) as IEnumerable<object>;

                        if (enumerableChildEntity != null)
                        {
                            for (int i = 0; i < enumerableChildEntity.Count(); i++)
                            {
                                dynamic childEntity = enumerableChildEntity.ElementAt(i);
                                if (childEntity != null
                                        && !relatedEntityList.Contains(childEntity))
                                    GetAllEntities(
                                        childEntity,
                                        relatedEntityList);
                            }
                        }
                    }
                    else
                    {
                        // If child entity is not collection get its own.
                        dynamic childEntity = entity.GetPropertyValue(childEntityProperty.Name);

                        if (childEntity != null
                                && !relatedEntityList.Contains(childEntity))
                            GetAllEntities(
                                    childEntity,
                                    relatedEntityList);
                    }
                }
            }
        }

        /// <summary>
        /// Calculate state define order of added entities.
        /// </summary>
        /// <returns>Sorted stete define order.</returns>
        private IOrderedEnumerable<KeyValuePair<string, int>> CalculateStateDefineOrder()
        {
            // Initialize store
            Dictionary<string, int> store = new Dictionary<string, int>();

            List<string> typeNames = ObjectContext
                .MetadataWorkspace
                .GetItems<EntityType>(DataSpace.CSpace)
                .Select(m => m.Name)
                .ToList();

            foreach (string typeName in typeNames)
            {
                IGraphEntityTypeManager entityTypeManager =
                    GetEntityTypeManager(typeName);
                entityTypeManager.FindPrincipalCount(store);
            }

            IOrderedEnumerable<KeyValuePair<string, int>> sorted =
                store.OrderBy(m => m.Value);

            return sorted;
        }

        /// <summary>
        /// Define state of entity. If entity already exists in the source
        /// set and values has not been altered set the state to Unchanged, 
        /// else if values has been changed set the state of changed properties
        /// to Modified, otherwise set the state to Added.
        /// </summary>
        /// <typeparam name="TEntity">Type of entity.</typeparam>
        /// <param name="entity">Entity to define state of.</param>
        private void DefineState<TEntity>(TEntity entity)
            where TEntity : class
        {
            // If entity has been detached, then do not try to define its state
            if (Context.Entry(entity).State == EntityState.Detached)
                return;

            IGraphEntityManager<TEntity> entityManager = GetEntityManager<TEntity>();
            // Get matching entity.
            TEntity matchingEntity = entityManager.GetMatchingEntity(entity);

            if (matchingEntity != null)
            {
                /*
                 * If entity is already in the context, for example if 
                 * entity has been retrieved in program layer using .FirstOrDefault()
                 * without calling .AsNoTracking()
                 * then this entity is tracked by entity framework. If entity retrieved
                 * using this method then, some properties have been altered, setting its
                 * state to Unchanged will undo all changes. It means that made alterations
                 * will be lost, and all current values will be replaced by original values.
                 * And keys will not be altered in child entities by settting state to Unchanged
                 * which has been done below in this code ( after dealing with duplicates ).
                 * As a workaround, I detach and readd entity to context to clear original values.
                */
                if (Context.Entry(entity).State != EntityState.Added)
                {
                    Context.Entry(entity).State = EntityState.Detached;
                    Context.Entry(entity).State = EntityState.Added;
                }

                entityManager.SynchronizeKeys(entity, matchingEntity);
            }

            // Deal with duplicates before proceeding
            DealWithDuplicates(entity);

            if (matchingEntity != null)
            {
                Context.Entry(entity).State = EntityState.Unchanged;
                entityManager.DetectPropertyChanges(entity, matchingEntity);
            }
            else
            {
                // When priamry keys of entity is not store generated
                // and state of entity is added, value of primary
                // keys will not reflected at child entities.
                // If primary keys of  entity has values different 
                // than default values then set its state to 
                // unchanged to fixup keys to solve this issue
                // and after that set state to Added
                var primaryKeys = entityManager.GetPrimaryKeys();
                if (!entity.HasDefaultValues(entityManager
                        .GetPrimaryKeys()))
                    Context.Entry(entity).State = EntityState.Unchanged;

                Context.Entry(entity).State = EntityState.Added;
            }
        }

        /// <summary>
        /// Define state of all entities in the context.
        /// </summary>
        /// <returns>IManualGraphManager to continue to work on.</returns>
        public IManualGraphManager DefineState()
        {
            // Calculate state define order.
            var stateDefineOrderCollection = CalculateStateDefineOrder();
            List<Type> addedEntityTypes = Context
                .ChangeTracker
                .Entries()
                .Select(m => m.Entity.GetType())
                .ToList();

            List<string> addedEntityTypeNames = addedEntityTypes
                .Select(m => m.Name)
                .ToList();

            foreach (var stateDefineOrder in stateDefineOrderCollection)
            {
                // If entity exists according to current define order.
                bool entityExists = addedEntityTypeNames
                    .Contains(stateDefineOrder.Key);

                if (!entityExists)
                    continue;

                // Get type of entity
                Type entityType = addedEntityTypes
                    .First(m => m.Name == stateDefineOrder.Key);

                // Get list of entities to define state.
                List<object> definedEntityStore = new List<object>();
                IEnumerable<object> entitySet = Context
                         .Set(entityType)
                         .Local
                         .CastToGeneric();

                // Order entities to define state of them accordingly.
                entitySet = DefineStateDefineOrder(entitySet);

                // Loop through entiteis and define state.
                for (int i = 0; i < entitySet.Count(); i++)
                {
                    dynamic entity = entitySet.ElementAt(i);

                    DefineState(entity);
                }
            }

            ManualGraphManager.ManualGraphManager manualGraphManager =
                new ManualGraphManager.ManualGraphManager(Context);
            return manualGraphManager;
        }

        /// <summary>
        /// Define state of entity. If entity already exists in the source
        /// set and values has not been altered set the state to Unchanged, 
        /// else if values has been changed set the state of changed properties
        /// to Modified, otherwise set the state of entity to Added.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// When entity is null.
        /// </exception>
        /// <typeparam name="TEntity">Type of entity.</typeparam>
        /// <param name="entity">Entity to define state of.</param>
        /// <param name="defineStateOfChildEntities">
        /// If set to true define state of
        /// configured child entities. This rule also applied to child entities
        /// of child entities and so on.
        /// </param>
        /// <returns>IManualGraphManager associated with current context to work on further.</returns>
        public IManualGraphManager<TEntity> DefineState<TEntity>(
            TEntity entity,
            bool defineStateOfChildEntities)
            where TEntity : class
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            // Call overload with list
            return DefineState(new List<TEntity> { entity }, defineStateOfChildEntities);
        }

        /// <summary>
        /// Define state of list of entities.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// When enti
        /// </exception>
        /// <typeparam name="TEntity">Type of entity.</typeparam>
        /// <param name="entityList">List of entities to define state of.</param>
        /// <param name="defineStateOfChildEntities">
        /// If set to true define state of
        /// configured child entities. This rule also applied to child entities
        /// of child entities and so on.
        /// </param>
        /// <returns>IManualGraphManager associated with current context to work on further.</returns>
        public IManualGraphManager<TEntity> DefineState<TEntity>(
            List<TEntity> entityList,
            bool defineStateOfChildEntities)
            where TEntity : class
        {
            if (entityList == null)
                throw new ArgumentNullException(nameof(entityList));

            // If we do not need to define state of child entities,
            // then we only need to define state of list of entitis.
            // Otherwise we need to get all entities and define order
            // in which state of entities must be defined.
            if (!defineStateOfChildEntities)
            {
                // Before starting state define set state of all Detached entities to Added.
                foreach (TEntity entity in entityList)
                {
                    if (Context.Entry(entity).State == EntityState.Detached)
                        Context.Entry(entity).State = EntityState.Added;
                }

                // Define state of entities
                foreach (TEntity entity in entityList)
                {
                    DefineState(entity);
                }
            }
            else
            {
                // Calculate state define order.
                var stateDefineOrderCollection = CalculateStateDefineOrder();

                // Get all entities related to provided list
                List<object> allEntities = new List<object>();
                entityList.ForEach(m => GetAllEntities(m, allEntities));

                // Group list of entities by type
                var groupedEntityList = allEntities
                    .GroupBy(m => m.GetType().Name)
                    .Select(g => new
                    {
                        TypeName = g.Key,
                        Entities = g.Select(m => m)
                    })
                    .ToDictionary(m => m.TypeName);

                // Before starting state define set state of all Detached entities to Added.
                foreach (object entity in allEntities)
                {
                    if (Context.Entry(entity).State == EntityState.Detached)
                        Context.Entry(entity).State = EntityState.Added;
                }

                // Define sate of entities.
                foreach (var stateDefineOrder in stateDefineOrderCollection)
                {
                    // If entity exists according to current define order.
                    bool entityExists = groupedEntityList.Keys
                        .Contains(stateDefineOrder.Key);

                    if (!entityExists)
                        continue;

                    // Get list of entities to define state.                    
                    IEnumerable<object> entititiesToDefineStateOf = groupedEntityList[stateDefineOrder.Key].Entities;

                    // Order entities to define state of them accordingly.
                    entititiesToDefineStateOf = DefineStateDefineOrder(entititiesToDefineStateOf);

                    // Loop through entiteis and define state.
                    for (int i = 0; i < entititiesToDefineStateOf.Count(); i++)
                    {
                        dynamic entity = entititiesToDefineStateOf.ElementAt(i);

                        DefineState(entity);
                    }
                }
            }

            ManualGraphManager<TEntity> manualGraphManager =
                new ManualGraphManager<TEntity>(Context);
            manualGraphManager.EntityCollection = entityList;
            return manualGraphManager;
        }

        /// <summary>
        /// Order entities according to principal self navigation count ascendingly
        /// for defining state appropriately.
        /// </summary>
        /// <example>
        /// firstCategory which has null value on ParentCategory property has 0 principal self 
        /// navigation count. If ParentCategory of secondCategory is firstCategory, then secondCategory
        /// has 1 principal self navigation count, if ParentCategory of thirdCateogry is secondCategory, 
        /// then it has 2 principal self navigaiton count. In this case, state of enitites must be defined 
        /// in ascending order. In our example, the order is like below:
        /// 1. firstCategory, 2. secondCategory, 3. thirdCateogry.
        /// </example>
        /// <exception cref="ArgumentNullException">
        /// When entityCollection is null.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// When entities in collection do not have same type.
        /// </exception>
        /// <param name="entityCollection">EntityCollection to define state define order.</param>
        /// <returns>Orderer entity collection suiting for defining state.</returns>
        private IEnumerable<object> DefineStateDefineOrder(IEnumerable<object> entityCollection)
        {
            if (entityCollection == null)
                throw new ArgumentNullException(nameof(entityCollection));

            if (!entityCollection.Any())
                return entityCollection;

            // Get type of entity
            string entityTypeName = entityCollection.First().GetType().Name;

            // All entities must have same type in collection.
            if (entityCollection
                .Any(m => m != null && m.GetType().Name != entityTypeName))
                throw new InvalidOperationException(string.Format(
                    "All entities must have same type in collection to define state define order."
                        + " Entity type: {0}.",
                    entityTypeName));

            // Check if entity has navigation propert to itself. If not, then
            // there is no need to order them, otherwise order.
            IGraphEntityTypeManager entityTypeManager = GetEntityTypeManager(entityTypeName);
            NavigationDetail navigationDetail = entityTypeManager.GetNavigationDetail();
            bool hasPrinciplaSelfNavigationProperty = navigationDetail
                .Relations
                .Any(m => m.Direction == NavigationDirection.From
                    && m.PropertyTypeName == entityTypeName);
            if (!hasPrinciplaSelfNavigationProperty)
                return entityCollection;

            // Initialize store for ordered entities.
            Dictionary<object, int> principalCountStore =
                new Dictionary<object, int>();

            for (int i = 0; i < entityCollection.Count(); i++)
            {
                dynamic entity = entityCollection.ElementAt(i);
                int principalSelfCount = CalculatePrincipalSelfNavigationCount(
                    entity, principalCountStore);
            }

            // Order according to principal slef navigation properties ascendingly.
            return principalCountStore.OrderBy(m => m.Value).Select(m => m.Key);
        }

        /// <summary>
        /// Calculate count of principal self navigation properties for 
        /// ordering to be able to successfully define order.
        /// </summary>
        /// <remarks>
        /// If Category has principal navigation to itself named ParentCategory, we need to
        /// find count of them to be able to define order appropriately. We need to define state of
        /// them in ascending order.
        /// </remarks>
        /// <example>
        /// firstCategory which has null value on ParentCategory property has 0 principal self 
        /// navigation count. If ParentCategory of secondCategory is firstCategory, then secondCategory
        /// has 1 principal self navigation count, if ParentCategory of thirdCateogry is secondCategory, 
        /// then it has 2 principal self navigaiton count.
        /// </example>
        /// <typeparam name="TEntity">Type of entity.</typeparam>
        /// <param name="entity">Entity to find principla self navigaiton count.</param>
        /// <param name="store">Store to add calculated count and check first.</param>
        /// <returns>Count of principal self navigation count.</returns>
        private int CalculatePrincipalSelfNavigationCount<TEntity>(
            TEntity entity,
            Dictionary<object, int> store)
            where TEntity : class
        {
            if (store.ContainsKey(entity))
                return store[entity];

            string typeName = entity.GetType().Name;

            // Get navigation details and find properties which is refers to itslef.
            IGraphEntityManager<TEntity> entityManager = GetEntityManager<TEntity>();
            NavigationDetail navigationDetail = entityManager.GetNavigationDetail();
            List<string> selfReferringPrincipalPropertyNames = navigationDetail
                .Relations
                .Where(m => m.Direction == NavigationDirection.From
                    && m.PropertyTypeName == typeName)
                .Select(m => m.PropertyName)
                .ToList();

            // Loop through principal navigation properties and add their count.
            int principalSelfCount = 0;
            foreach (string propertyName in selfReferringPrincipalPropertyNames)
            {
                TEntity principalEntity = entity.GetPropertyValue(propertyName) as TEntity;

                // If principla entity is null then continue
                if (principalEntity == null)
                    continue;

                // Othewise calculate principal count as 1 + count for principal entity
                principalSelfCount++;
                principalSelfCount += CalculatePrincipalSelfNavigationCount(principalEntity, store);
            }

            store.Add(entity, principalSelfCount);
            return principalSelfCount;
        }

        #region IContextFactory members

        public IContextHelper GetContextHelper()
        {
            return this;
        }

        public IGraphEntityManager<TEntity> GetEntityManager<TEntity>()
            where TEntity : class
        {
            Type entityType = typeof(TEntity);

            // Try to get from store
            if (Store.EntityManager.ContainsKey(entityType))
                return Store.EntityManager[entityType] as IGraphEntityManager<TEntity>;

            // Initialize
            IGraphEntityManager<TEntity> entityManager =
                new GraphEntityManager<TEntity>(this);

            // Add to store and return
            Store.EntityManager.Add(entityType, entityManager);
            return entityManager;
        }

        public IGraphEntityTypeManager GetEntityTypeManager(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                throw new ArgumentNullException(nameof(typeName));

            // Try to get from store
            if (Store.EntityTypeManager.ContainsKey(typeName))
                return Store.EntityTypeManager[typeName];

            // Initialize
            IGraphEntityTypeManager entityTypeManager =
                new GraphEntityTypeManager(this, typeName);

            // Add to store and return
            Store.EntityTypeManager.Add(typeName, entityTypeManager);
            return entityTypeManager;
        }

        #endregion
    }
}
