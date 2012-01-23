using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Metadata.Edm;
using System.Data.Objects;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Web.DynamicData.ModelProviders;
using System.Data.Services.Client;
using System.Xml.Linq;
using Microsoft.Data.Services.WebControls;

namespace Microsoft.Data.Services.DynamicDataEdmProvider {
    public sealed class EdmDataModelProvider : DataModelProvider {
        private ReadOnlyCollection<TableProvider> _tables;

        internal Dictionary<long, EdmColumnProvider> RelationshipEndLookup { get; private set; }
        internal Dictionary<EntityType, EdmTableProvider> TableEndLookup { get; private set; }
        private Func<object> ContextFactory { get; set; }
        private Dictionary<EdmType, Type> _entityTypeToClrType = new Dictionary<EdmType, Type>();
        private IEnumerable<ComplexType> _complexTypes;

        private static IEnumerable<XElement> GetSchemas(XDocument xdoc) {
            return xdoc.Descendants().Where(i => i.Name.LocalName == "Schema");
        }

        public EdmDataModelProvider(Uri serviceUri, bool isReadonly, bool supportPagingAndSorting) {

            var context = new DataServiceContext(serviceUri);
            Uri metadataUri = context.GetMetadataUri();
            var doc = XDocument.Load(metadataUri.AbsoluteUri);

            var itemCollection = new EdmItemCollection(GetSchemas(doc).Select(s => s.CreateReader()));

            RelationshipEndLookup = new Dictionary<long, EdmColumnProvider>();
            TableEndLookup = new Dictionary<EntityType, EdmTableProvider>();

            var tables = new List<TableProvider>();

            // Create a dictionary from entity type to entity set. The entity type should be at the root of any inheritance chain.
            IDictionary<EntityType, EntitySet> entitySetLookup = itemCollection.GetItems<EntityContainer>().SelectMany(c => c.BaseEntitySets.OfType<EntitySet>()).ToDictionary(e => e.ElementType);

            // Create a lookup from parent entity to entity
            ILookup<EntityType, EntityType> derivedTypesLookup = itemCollection.GetItems<EntityType>().ToLookup(e => (EntityType)e.BaseType);

            _complexTypes = itemCollection.GetItems<ComplexType>();

            // Keeps track of the current entity set being processed
            EntitySet currentEntitySet = null;

            // Do a DFS to get the inheritance hierarchy in order
            // i.e. Consider the hierarchy
            // null -> Person
            // Person -> Employee, Contact
            // Employee -> SalesPerson, Programmer
            // We'll walk the children in a depth first order -> Person, Employee, SalesPerson, Programmer, Contact.
            var objectStack = new Stack<EntityType>();
            // Start will null (the root of the hierarchy)
            objectStack.Push(null);
            while (objectStack.Any()) {
                EntityType entityType = objectStack.Pop();
                if (entityType != null) {
                    // Update the entity set when we are at another root type (a type without a base type).
                    if (entityType.BaseType == null) {
                        currentEntitySet = entitySetLookup[entityType];
                    }

                    var table = CreateTableProvider(currentEntitySet, entityType, isReadonly, supportPagingAndSorting);
                    tables.Add(table);
                }

                foreach (EntityType derivedEntityType in derivedTypesLookup[entityType]) {
                    // Push the derived entity types on the stack
                    objectStack.Push(derivedEntityType);
                }
            }

            _tables = tables.AsReadOnly();

            GenerateClrTypes(serviceUri);
        }

        private void GenerateClrTypes(Uri serviceUri) {
            var contextGenerator = new DataServiceContextGenerator(serviceUri);

            foreach (EdmTableProvider table in Tables) {
                table.GenerateEntitySet(contextGenerator);
                _entityTypeToClrType[table.EdmEntityType] = table.EntityType;
            }

            foreach (ComplexType complexType in _complexTypes) {
                var clrType = contextGenerator.AddComplexType(complexType.Name);
                _entityTypeToClrType[complexType] = clrType;
            }

            foreach (EdmTableProvider table in Tables) {
                table.GenerateProperties(contextGenerator);
            }

            foreach (ComplexType complexType in _complexTypes) {
                foreach (EdmProperty prop in complexType.Properties) {
                    Type propertyType = ((PrimitiveType)prop.TypeUsage.EdmType).ClrEquivalentType;
                    if (propertyType.IsValueType) {
                        propertyType = typeof(Nullable<>).MakeGenericType(propertyType);
                    }
                    contextGenerator.AddColumnProperty(
                        complexType.Name, propertyType, prop.Name);
                }
            }

            Dictionary<Type, Type> typeBuilderToRealTypeMapping = contextGenerator.GenerateContextType();

            ContextType = contextGenerator.GeneratedType;

            // Fix up the types to now be the real ones
            foreach (EdmTableProvider table in Tables) {
                Type realEntityType = typeBuilderToRealTypeMapping[table.EntityType];
                _entityTypeToClrType[table.EdmEntityType] = realEntityType;
                table.SetEntityType(realEntityType);

                foreach (EdmColumnProvider column in table.Columns) {
                    column.ResetColumnType();
                }
            }

            ContextFactory = () => Activator.CreateInstance(ContextType);
        }

        //public EFDataModelProvider(object contextInstance, Func<object> contextFactory) {
        //    ContextFactory = contextFactory;
        //    RelationshipEndLookup = new Dictionary<long, EFColumnProvider>();
        //    TableEndLookup = new Dictionary<EntityType, EFTableProvider>();

        //    _context = (ObjectContext)contextInstance ?? (ObjectContext)CreateContext();
        //    ContextType = _context.GetType();

        //    // get a "container" (a scope at the instance level)
        //    EntityContainer container = _context.MetadataWorkspace.GetEntityContainer(_context.DefaultContainerName, DataSpace.CSpace);
        //    // load object space metadata
        //    //_context.MetadataWorkspace.LoadFromAssembly(ContextType.Assembly);
        //    _objectSpaceItems = (ObjectItemCollection)_context.MetadataWorkspace.GetItemCollection(DataSpace.OSpace);

        //    var tables = new List<TableProvider>();

        //    // Create a dictionary from entity type to entity set. The entity type should be at the root of any inheritance chain.
        //    IDictionary<EntityType, EntitySet> entitySetLookup = container.BaseEntitySets.OfType<EntitySet>().ToDictionary(e => e.ElementType);

        //    // Create a lookup from parent entity to entity
        //    ILookup<EntityType, EntityType> derivedTypesLookup = _context.MetadataWorkspace.GetItems<EntityType>(DataSpace.CSpace).ToLookup(e => (EntityType)e.BaseType);

        //    // Keeps track of the current entity set being processed
        //    EntitySet currentEntitySet = null;

        //    // Do a DFS to get the inheritance hierarchy in order
        //    // i.e. Consider the hierarchy
        //    // null -> Person
        //    // Person -> Employee, Contact
        //    // Employee -> SalesPerson, Programmer
        //    // We'll walk the children in a depth first order -> Person, Employee, SalesPerson, Programmer, Contact.
        //    var objectStack = new Stack<EntityType>();
        //    // Start will null (the root of the hierarchy)
        //    objectStack.Push(null);
        //    while (objectStack.Any()) {
        //        EntityType entityType = objectStack.Pop();
        //        if (entityType != null) {
        //            // Update the entity set when we are at another root type (a type without a base type).
        //            if (entityType.BaseType == null) {
        //                currentEntitySet = entitySetLookup[entityType];
        //            }

        //            var table = CreateTableProvider(currentEntitySet, entityType);
        //            tables.Add(table);
        //        }

        //        foreach (EntityType derivedEntityType in derivedTypesLookup[entityType]) {
        //            // Push the derived entity types on the stack
        //            objectStack.Push(derivedEntityType);
        //        }
        //    }

        //    _tables = tables.AsReadOnly();
        //}

        public override object CreateContext() {
            return ContextFactory();
        }

        public override ReadOnlyCollection<TableProvider> Tables {
            get {
                return _tables;
            }
        }        

        internal Type GetClrType(EdmType entityType) {
            var result = _entityTypeToClrType[entityType];
            Debug.Assert(result != null, String.Format(CultureInfo.CurrentCulture, "Cannot map EdmType '{0}' to matching CLR Type", entityType));
            return result;
        }

        private Type GetClrType(EntityType entityType) {
            throw new NotImplementedException();
            //var objectSpaceType = (EntityType)_context.MetadataWorkspace.GetObjectSpaceType(entityType);
            //return _objectSpaceItems.GetClrType(objectSpaceType);
        }

        private TableProvider CreateTableProvider(EntitySet entitySet, EntityType entityType,
            bool isReadonly, bool supportPagingAndSorting) {
            EntityType parentEntityType = entityType.BaseType as EntityType;

            // Normally, use the entity set name as the table name
            string tableName = entitySet.Name;

            EdmTableProvider table = new EdmTableProvider(this, entitySet, entityType, tableName, isReadonly, supportPagingAndSorting);
            TableEndLookup[entityType] = table;

            return table;
        }
    }
}
