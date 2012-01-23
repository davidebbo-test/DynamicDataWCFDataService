using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Web.DynamicData.ModelProviders;
using System.Web.UI;
using System.Data.Services.Client;

namespace Microsoft.Web.Data.Services.Client {
    internal class DataServiceTableProvider : TableProvider {
        private Collection<ColumnProvider> _columns;
        private ReadOnlyCollection<ColumnProvider> _readOnlyColumns;
        private MethodInfo _createQueryMethod;

        public DataServiceTableProvider(DataModelProvider model, string entitySetName, Type entityType)
            : base(model) {
            Name = entitySetName;
            EntityType = entityType;

            _columns = new Collection<ColumnProvider>();
            _readOnlyColumns = new ReadOnlyCollection<ColumnProvider>(_columns);

            AddColumnsRecursive(EntityType);
        }

        /// <summary>
        /// The reason we use this recursive approach instead of simply not specifying BindingFlags.DeclaredOnly
        /// is that we want to make sure that we get the base type columns before the derive type columns, and the
        /// default reflection behavior does it the other way around
        /// </summary>
        private void AddColumnsRecursive(Type entityType) {
            // First add all the base type's columns
            if (entityType != typeof(object)) {
                AddColumnsRecursive(entityType.BaseType);
            }

            // Then add the columns for the current type
            foreach (PropertyInfo columnProp in entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)) {
                _columns.Add(new DataServiceColumnProvider(
                    this, columnProp, IsKeyColumn(columnProp)));
            }
        }

        private bool IsKeyColumn(PropertyInfo columnProp) {
            // In Azure, don't treat the PartitionKey as a PK since it is full handled
            // internally in the data service context
            if (columnProp.Name == "PartitionKey")
                return false;

            return DataServiceUtilities.IsKeyColumn(columnProp);
        }

        public override IQueryable GetQuery(object context) {
            // Get the CreateQuery method if needed
            if (_createQueryMethod == null) {
                MethodInfo createQueryGenericMethod = typeof(DataServiceContext).GetMethod("CreateQuery");
                _createQueryMethod = createQueryGenericMethod.MakeGenericMethod(EntityType);
            }

            return (IQueryable)_createQueryMethod.Invoke(context, new[] { Name });

            // OLD CODE that used the context property
            //object contextPropValue = _prop.GetValue(context, null);

            //if (!(contextPropValue is IQueryable)) {
            //    throw new NotSupportedException("Entry points representing tables must implement IQueryable");
            //}

            //return (IQueryable)contextPropValue;
        }

        public override ReadOnlyCollection<ColumnProvider> Columns {
            get {
                return _readOnlyColumns;
            }
        }

        internal void AddColumn(ColumnProvider cp) {
            DeleteColumnIfExists(cp.Name);
            _columns.Add(cp);
        }

        private void DeleteColumnIfExists(string name) {
            ColumnProvider existingColumn = _columns.FirstOrDefault(c => c.Name == name);
            if (existingColumn != null) {
                _columns.Remove(existingColumn);
            }
        }

        public override object EvaluateForeignKey(object row, string foreignKeyName) {
            return DataBinder.Eval(row, foreignKeyName);
        }

        internal void Initialize() {
            for (int i=0; i<_columns.Count; i++) {
                var column = (DataServiceColumnProvider)_columns[i];

                var associationColumn = column.TryCreateAssociationColumn();
                if (associationColumn != null) {
                    // Replace the FK column by the association column
                    _columns.RemoveAt(i);

                    DeleteColumnIfExists(associationColumn.Name);
                    _columns.Insert(i, associationColumn);
                }
                else {
                    column.Initialize();
                }
            }
        }

    }
}

