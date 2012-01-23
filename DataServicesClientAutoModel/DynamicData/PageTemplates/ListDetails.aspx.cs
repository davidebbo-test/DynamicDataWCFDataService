﻿using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Web.DynamicData;
using System.Web.Routing;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.UI.WebControls.Expressions;
using Microsoft.Data.Services.DynamicDataEdmProvider;
using System.Collections;
using System.Collections.Generic;

namespace DynamicDataClientSite {

    public class MyAutoFieldGenerator : IAutoFieldGenerator {
        private MetaTable _metaTable;

        public MyAutoFieldGenerator(MetaTable table) {
            _metaTable = table;
        }

        public ICollection GenerateFields(Control control) {
            DataBoundControlMode mode = GetMode(control);
            ContainerType containerType = GetControlContainerType(control);

            // Auto-generate fields from metadata.
            List<DynamicField> fields = new List<DynamicField>();
            foreach (MetaColumn column in _metaTable.GetScaffoldColumns(mode, containerType)) {

                // Ignore guid columns
                if (column.ColumnType == typeof(Guid))
                    continue;

                fields.Add(CreateField(column, containerType, mode));
            }

            return fields;
        }

        static bool foo = true;

        protected virtual DynamicField CreateField(MetaColumn column, ContainerType containerType, DataBoundControlMode mode) {
            string headerText = (containerType == ContainerType.List ? column.ShortDisplayName : column.DisplayName);

            return new DynamicField() {
                DataField = column.Name,
                HeaderText = headerText
            };
        }

        internal static ContainerType GetControlContainerType(Control control) {
            if (control is IDataBoundListControl || control is Repeater) {
                return ContainerType.List;
            }
            else if (control is IDataBoundItemControl) {
                return ContainerType.Item;
            }

            return ContainerType.List;
        }

        internal static DataBoundControlMode GetMode(Control control) {
            // Only item controls have distinct modes
            IDataBoundItemControl itemControl = control as IDataBoundItemControl;
            if (itemControl != null && GetControlContainerType(control) != ContainerType.List) {
                return itemControl.Mode;
            }

            return DataBoundControlMode.ReadOnly;
        }
    }

    public partial class ListDetails : System.Web.UI.Page {
        protected MetaTable table;

        protected void Page_Init(object sender, EventArgs e) {
            table = DynamicDataRouteHandler.GetRequestMetaTable(Context);
            GridView1.SetMetaTable(table, table.GetColumnValuesFromRoute(Context));
            FormView1.SetMetaTable(table);
            GridDataSource.EntityTypeName = table.EntityType.AssemblyQualifiedName;
            DetailsDataSource.EntityTypeName = table.EntityType.AssemblyQualifiedName;
            if (table.EntityType != table.RootEntityType) {
                GridQueryExtender.Expressions.Add(new OfTypeExpression(table.EntityType));
            }

            GridDataSource.AutoLoadForeignKeys();
            DetailsDataSource.AutoLoadForeignKeys();
        }

        protected void Page_Load(object sender, EventArgs e) {
            Title = table.DisplayName;

            GridView1.ColumnsGenerator = new MyAutoFieldGenerator(table);

            GridView1.AllowPaging = ((EdmTableProvider)table.Provider).SupportPaging;

            // Selection from url
            if (!Page.IsPostBack && table.HasPrimaryKey) {
                GridView1.SelectedPersistedDataKey = table.GetDataKeyFromRoute();
                if (GridView1.SelectedPersistedDataKey == null) {
                    GridView1.SelectedIndex = 0;
                }
            }

            // Disable various options if the table is readonly
            if (table.IsReadOnly) {
                DetailsPanel.Visible = false;
                GridView1.AutoGenerateSelectButton = false;
                GridView1.AutoGenerateEditButton = false;
                GridView1.AutoGenerateDeleteButton = false;
                GridView1.EnablePersistedSelection = false;
            }

            if (!table.CanUpdate(User)) {

                // Paging requires view state
                if (!GridView1.AllowPaging) {
                    GridDataSource.EnableViewState = false;
                    GridView1.EnableViewState = false;
                }
                GridDataSource.EnableUpdate = false;
                DetailsDataSource.EnableUpdate = false;
                GridView1.AutoGenerateEditButton = false;
            }

            if (!table.CanDelete(User)) {
                GridDataSource.EnableDelete = false;
                DetailsDataSource.EnableDelete = false;
                GridView1.AutoGenerateDeleteButton = false;
            }

            if (!table.CanInsert(User)) {
                DetailsDataSource.EnableInsert = false;
            }
        }

        protected void FormViewCUD_Load(object sender, EventArgs e) {
            if (!table.CanUpdate(User)) {
                ((Control)sender).Visible = false;
            }
        }

        protected void GridView1_DataBound(object sender, EventArgs e) {
            if (GridView1.Rows.Count == 0) {
                FormView1.ChangeMode(FormViewMode.Insert);
            }
        }

        protected void Label_PreRender(object sender, EventArgs e) {
            Label label = (Label)sender;
            DynamicFilter dynamicFilter = (DynamicFilter)label.FindControl("DynamicFilter");
            QueryableFilterUserControl fuc = dynamicFilter.FilterTemplate as QueryableFilterUserControl;
            if (fuc != null && fuc.FilterControl != null) {
                label.AssociatedControlID = fuc.FilterControl.GetUniqueIDRelativeTo(label);
            }
        }

        protected void DynamicFilter_FilterChanged(object sender, EventArgs e) {
            GridView1.EditIndex = -1;
            GridView1.PageIndex = 0;
            FormView1.ChangeMode(FormViewMode.ReadOnly);
        }

        protected void GridView1_RowEditing(object sender, EventArgs e) {
            FormView1.ChangeMode(FormViewMode.ReadOnly);
        }

        protected void GridView1_SelectedIndexChanging(object sender, EventArgs e) {
            GridView1.EditIndex = -1;
            FormView1.ChangeMode(FormViewMode.ReadOnly);
        }

        protected void GridView1_RowCreated(object sender, GridViewRowEventArgs e) {
            SetDeleteConfirmation(e.Row);
        }

        protected void GridView1_RowDeleted(object sender, GridViewDeletedEventArgs e) {
            FormView1.DataBind();
        }

        protected void GridView1_RowUpdated(object sender, GridViewUpdatedEventArgs e) {
            FormView1.DataBind();
        }

        protected void FormView1_ItemDeleted(object sender, FormViewDeletedEventArgs e) {
            GridView1.DataBind();
        }

        protected void FormView1_ItemUpdated(object sender, FormViewUpdatedEventArgs e) {
            GridView1.DataBind();
        }

        protected void FormView1_ItemInserted(object sender, FormViewInsertedEventArgs e) {
            GridView1.DataBind();
        }

        protected void FormView1_ModeChanging(object sender, FormViewModeEventArgs e) {
            if (e.NewMode != FormViewMode.ReadOnly) {
                GridView1.EditIndex = -1;
            }
        }

        protected void FormView1_PreRender(object sender, EventArgs e) {
            if (FormView1.Row != null) {
                SetDeleteConfirmation(FormView1.Row);
            }
        }

        private void SetDeleteConfirmation(TableRow row) {
            foreach (Control c in row.Cells[0].Controls) {
                LinkButton button = c as LinkButton;
                if (button != null && button.CommandName == DataControlCommands.DeleteCommandName) {
                    button.OnClientClick = "return confirm('Are you sure you want to delete this item?');";
                }
            }
        }

    }
}
