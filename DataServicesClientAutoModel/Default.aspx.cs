using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.DynamicData;
using Microsoft.Data.Services.WebControls;
using Microsoft.Web.Data.Services.Client;
using System.Web.Routing;
using System.Xml.Linq;
using System.Data.Metadata.Edm;
using Microsoft.Data.Services.DynamicDataEdmProvider;
using System.Data.Services.Client;

namespace DynamicDataClientSite {
    public partial class Default : System.Web.UI.Page {

        private static int _modelCount;

        protected void Page_Load(object sender, EventArgs e) {

            var model = Session["model"] as MetaModel;

            if (model == null) {
                tableList.Visible = false;
                return;
            }

            System.Collections.IList visibleTables = model.VisibleTables;
            if (visibleTables.Count == 0) {
                throw new InvalidOperationException("There are no accessible tables. Make sure that at least one data model is registered in Global.asax and scaffolding is enabled or implement custom pages.");
            }
            Menu1.DataSource = visibleTables;
            Menu1.DataBind();
        }

        protected void Button1_Click(object sender, EventArgs e) {

            var model = new MetaModel();
            System.Threading.Interlocked.Increment(ref _modelCount);

            var provider = new EdmDataModelProvider(
                new Uri(TextBox1.Text.Trim()),
                isReadonly: !CheckBoxSupportEditing.Checked,
                supportPagingAndSorting: CheckBoxSupportPagingSorting.Checked);

            model.RegisterContext(
                provider,
                new ContextConfiguration() { ScaffoldAllTables = true });

            var routes = RouteTable.Routes;

            // TODO: remove stale routes!

            // The following statements support combined-page mode, where the List, Detail, Insert, and
            // Update tasks are performed by using the same page. To enable this mode, uncomment the
            // following routes and comment out the route definition in the separate-page mode section above.
            routes.Add(new DynamicDataRoute(String.Format("{0}/{{table}}/ListDetails.aspx", _modelCount)) {
                Action = PageAction.List,
                ViewName = "ListDetails",
                Model = model
            });

            routes.Add(new DynamicDataRoute(String.Format("{0}/{{table}}/ListDetails.aspx", _modelCount)) {
                Action = PageAction.Details,
                ViewName = "ListDetails",
                Model = model
            });

            Session["model"] = model;

            Response.Redirect("Default.aspx");
        }
    }
}