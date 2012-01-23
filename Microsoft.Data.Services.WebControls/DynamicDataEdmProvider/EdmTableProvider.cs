using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Metadata.Edm;
using System.Data.Objects;
using System.Linq;
using System.Reflection;
using System.Web.DynamicData.ModelProviders;
using Microsoft.Data.Services.WebControls;

namespace Microsoft.Data.Services.DynamicDataEdmProvider {
    public sealed class EdmTableProvider : TableProvider {
        private ReadOnlyCollection<ColumnProvider> _roColumns;
        private bool _isReadonly;

        public EdmTableProvider(EdmDataModelProvider dataModel, EntitySet entitySet, EntityType entityType, string name,
            bool isReadonly, bool supportPagingAndSorting)
            : base(dataModel) {

            _isReadonly = isReadonly;
            SupportPaging = supportPagingAndSorting;
            Name = name;
            DataContextPropertyName = entitySet.Name;
            EdmEntityType = entityType;

            var keyMembers = entityType.KeyMembers;

            // columns (entity properties)
            // note 1: keys are also available through es.ElementType.KeyMembers
            // note 2: this includes "nav properties", kind of fancy, two-way relationship objects
            var columns = new List<ColumnProvider>();
            foreach (EdmMember m in entityType.Members) {
                if (EdmColumnProvider.IsSupportedEdmMemberType(m)) {
                    var entityMember = new EdmColumnProvider(entityType, this, m, keyMembers.Contains(m), supportPagingAndSorting);
                    columns.Add(entityMember);
                }
            }

            _roColumns = new ReadOnlyCollection<ColumnProvider>(columns);
        }

        internal EntityType EdmEntityType { get; private set; }
        private string EntityTypeName { get { return EdmEntityType.Name; } }

        private MethodInfo CreateQueryMethod { get; set; }

        public override ReadOnlyCollection<ColumnProvider> Columns {
            get {
                return _roColumns;
            }
        }

        public override IQueryable GetQuery(object context) {
            return (IQueryable)CreateQueryMethod.Invoke(context,
                new object[] { Name });
        }

        public override object EvaluateForeignKey(object row, string foreignKeyName) {
            try {
                // First try to evaluate the whole name as the property name
                return System.Web.UI.DataBinder.GetPropertyValue(row, foreignKeyName);
            }
            catch {
                // If that fails, walk the dots
                return System.Web.UI.DataBinder.Eval(row, foreignKeyName);
            }
        }

        internal void GenerateEntitySet(DataServiceContextGenerator contextGenerator) {
            var pkNames = Columns.Where(c => c.IsPrimaryKey).Select(c => c.Name);
            EntityType = contextGenerator.AddEntitySet(EntityTypeName, Name, pkNames);
        }

        internal void GenerateProperties(DataServiceContextGenerator contextGenerator) {
            foreach (EdmColumnProvider column in Columns) {
                contextGenerator.AddColumnProperty(
                    EntityTypeName, column.GetColumnPropertyType(), column.Name, column.Nullable);
            }
        }

        internal void SetEntityType(Type type) {
            EntityType = type;

            var genericMethod = DataModel.ContextType.GetMethod("CreateQuery");
            CreateQueryMethod = genericMethod.MakeGenericMethod(EntityType);
        }

        public override bool CanUpdate(System.Security.Principal.IPrincipal principal) {
            return !_isReadonly;
        }

        public override bool CanDelete(System.Security.Principal.IPrincipal principal) {
            return !_isReadonly;
        }

        public override bool CanInsert(System.Security.Principal.IPrincipal principal) {
            return !_isReadonly;
        }

        public bool SupportPaging { get; set; }
    }
}
