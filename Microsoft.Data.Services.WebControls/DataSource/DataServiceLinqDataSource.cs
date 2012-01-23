using System;
using System.Collections.Generic;
using System.Data.Services.Client;
using System.Data.Services.Common;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web;
using System.Web.DynamicData;
using System.Web.UI;
using System.Collections;
using System.Web.UI.WebControls;

namespace Microsoft.Web.Data.Services.Client {
    public class DataServiceLinqDataSource : LinqDataSource {
        protected override LinqDataSourceView CreateView() {
            return new DataServiceLinqDataSourceView(this, "DefaultView", Context);
        }

        /// <summary>
        /// This method is only callable when using Dynamic Data.  It causes the related entities to be
        /// preloaded.  e.g. in Product, the Category and Supplier are made available.
        /// </summary>
        public void AutoLoadForeignKeys() {

            Selecting += delegate(object sender, LinqDataSourceSelectEventArgs e) {
                MetaTable table = this.GetTable();

                var context = (DataServiceContext)table.CreateContext();

                var query = table.GetQuery(context);
                if (!String.IsNullOrEmpty(table.ForeignKeyColumnsNames)) {
                    MethodInfo addQueryOptionMethod = query.GetType().GetMethod("AddQueryOption");
                    if (addQueryOptionMethod != null) {
                        query = (IQueryable)addQueryOptionMethod.Invoke(query, new object[] { "$expand", table.ForeignKeyColumnsNames });
                        e.Result = query;
                    }
                }
            };
        }
    }

    public class DataServiceLinqDataSourceView : LinqDataSourceView {
        private Dictionary<string, string> _etagMap;
        private DataServiceContext _dataServiceContext;

        public DataServiceLinqDataSourceView(DataServiceLinqDataSource owner, string name, HttpContext context)
            : base(owner, name, context) {
        }

        protected override void LoadViewState(object savedState) {
            if (savedState == null) {
                base.LoadViewState(null);
            }
            else {
                var myState = (Pair)savedState;
                base.LoadViewState(myState.First);
                _etagMap = (Dictionary<string, string>)myState.Second;
            }
        }

        protected override object SaveViewState() {
            var myState = new Pair();
            myState.First = base.SaveViewState();
            if (_dataServiceContext != null) {
                myState.Second = _dataServiceContext.Entities.Where(ed => !String.IsNullOrEmpty(ed.ETag)).ToDictionary(
                    ed => DataServiceUtilities.BuildCompositeKey(ed.Entity), ed => ed.ETag);
            }
            return myState;
        }

        protected override void OnContextCreated(LinqDataSourceStatusEventArgs e) {
            base.OnContextCreated(e);

            _dataServiceContext = (DataServiceContext)e.Result;
        }

        protected override void ValidateContextType(Type contextType, bool selecting) { }
        protected override void ValidateTableType(Type tableType, bool selecting) { }


        protected override IQueryable ExecuteQuery(IQueryable source, QueryContext context) {

            // If we're not supposed to retrieve the total row count, just call the base
            if (!context.Arguments.RetrieveTotalRowCount)
                return base.ExecuteQuery(source, context);

            // Turn that off so that the base implementation won't make a count request
            context.Arguments.RetrieveTotalRowCount = false;

            // Call the base to build the query
            source = base.ExecuteQuery(source, context);

            // Include the total Row Count as part of the data query, to avoid making two separate queries
            MethodInfo includeTotalCountMethod = source.GetType().GetMethod("IncludeTotalCount");
            if (includeTotalCountMethod == null)
                return source;

            source = (IQueryable)includeTotalCountMethod.Invoke(source, null);

            // Execute the query
            MethodInfo executeMethod = source.GetType().GetMethod("Execute");
            var queryOperationResponse = (QueryOperationResponse)executeMethod.Invoke(source, null);

            // Get the count and set it in the Arguments
            context.Arguments.TotalRowCount = (int)queryOperationResponse.TotalCount;

            // Return it as an IQueryable.
            // Note that we end up returning an executed query, while the base implementation doesn't.
            // But this should be harmless, since all the LINQ operations were already included in the query
            return queryOperationResponse.AsQueryable();
        }

        protected override void DeleteDataObject(object dataContext, object table, object oldDataObject) {
            var dataServiceContext = (DataServiceContext)dataContext;
            var idataServiceContext = dataContext as IDataServiceContext;
            string etag = null;
            if (this._etagMap != null && this._etagMap.TryGetValue(DataServiceUtilities.BuildCompositeKey(oldDataObject), out etag)) {
                if (idataServiceContext != null)
                    idataServiceContext.AttachTo(TableName, oldDataObject, etag);
                else
                    dataServiceContext.AttachTo(TableName, oldDataObject, etag);
            }
            else {
                if (idataServiceContext != null)
                    idataServiceContext.AttachTo(TableName, oldDataObject);
                else
                    dataServiceContext.AttachTo(TableName, oldDataObject);
            }
            dataServiceContext.DeleteObject(oldDataObject);
            dataServiceContext.SaveChanges();
        }

        public override void Insert(IDictionary values, DataSourceViewOperationCallback callback) {

            // Keep track of the values to do foreign key processing in InsertDataObject
            _values = values;
            base.Insert(values, callback);
        }

        protected override void InsertDataObject(object dataContext, object table, object newDataObject) {
            var dataServiceContext = (DataServiceContext)dataContext;
            var idataServiceContext = dataContext as IDataServiceContext;
            if (idataServiceContext != null)
                idataServiceContext.AddObject(TableName, newDataObject);
            else
                dataServiceContext.AddObject(TableName, newDataObject);
            ProcessForeignKeys(dataServiceContext, newDataObject, _values);
            dataServiceContext.SaveChanges();
        }

        private IDictionary _values;
        public override void Update(System.Collections.IDictionary keys, IDictionary values, IDictionary oldValues, System.Web.UI.DataSourceViewOperationCallback callback) {

            // Keep track of the values to do foreign key processing in UpdateDataObject
            _values = values;
            base.Update(keys, values, oldValues, callback);
        }

        protected override void UpdateDataObject(object dataContext, object table, object oldDataObject, object newDataObject) {

            var dataServiceContext = (DataServiceContext)dataContext;
            var idataServiceContext = dataContext as IDataServiceContext;
            string etag = null;
            if (this._etagMap != null && this._etagMap.TryGetValue(DataServiceUtilities.BuildCompositeKey(oldDataObject), out etag)) {
                if (idataServiceContext != null)
                    idataServiceContext.AttachTo(TableName, newDataObject, etag);
                else
                    dataServiceContext.AttachTo(TableName, newDataObject, etag);
            }
            else {
                if (idataServiceContext != null)
                    idataServiceContext.AttachTo(TableName, newDataObject);
                else
                    dataServiceContext.AttachTo(TableName, newDataObject);
            }

            ProcessForeignKeys(dataServiceContext, newDataObject, _values);

            dataServiceContext.UpdateObject(newDataObject);
            DataServiceResponse dataServiceResponse = dataServiceContext.SaveChanges();
        }

        private void ProcessForeignKeys(DataServiceContext dataServiceContext, object dataObject, IDictionary values) {
            foreach (string key in values.Keys) {
                // Check if it looks like a FK, e.g. Category.CategoryID
                string[] parts = key.Split('.');
                if (parts.Length != 2)
                    continue;

                // Get the name of the entity ref property, e.g. Category
                string entityRefPropertyName = parts[0];

                // Get the PropertyInfo for the entity ref property
                PropertyInfo propInfo = dataObject.GetType().GetProperty(entityRefPropertyName);

                object entityRefObject = null;
                
                if (values[key] != null) {
                    // Create an 'empty' related entity, e.g a Category
                    entityRefObject = Activator.CreateInstance(propInfo.PropertyType);

                    // Set the PK in the related entity, e.g. set the CategoryID in the Category
                    PropertyInfo subPropInfo = propInfo.PropertyType.GetProperty(parts[1]);
                    subPropInfo.SetValue(
                        entityRefObject,
                        Convert.ChangeType(values[key], subPropInfo.PropertyType),
                        null);
                }

                // Find the entity set property for the association table e.g. Categories
                PropertyInfo entitySetProp = DataServiceUtilities.FindEntitySetProperty(
                    dataServiceContext.GetType(), propInfo.PropertyType);

                // Attach the related entity and set it as the link on the main entity
                if (entitySetProp != null) {
                    if (entityRefObject != null) {
                        dataServiceContext.AttachTo(entitySetProp.Name, entityRefObject);
                    }
                    dataServiceContext.SetLink(dataObject, entityRefPropertyName, entityRefObject);
                }
            }
        }

        private static bool IsKeyColumn(PropertyInfo pi) {
            // Astoria convention:
            // 1) try the DataServiceKey attribute
            // 2) if not attribute, try <typename>ID
            // 3) finally, try just ID

            object[] attribs = pi.DeclaringType.GetCustomAttributes(typeof(DataServiceKeyAttribute), true);
            if (attribs != null && attribs.Length > 0) {
                Debug.Assert(attribs.Length == 1);
                return ((DataServiceKeyAttribute)attribs[0]).KeyNames.Contains(pi.Name);
            }

            if (pi.Name.Equals(pi.DeclaringType.Name + "ID", System.StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            if (pi.Name == "ID") {
                return true;
            }

            return false;
        }
    }
}
