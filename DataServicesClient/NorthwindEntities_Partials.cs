using System.Web;
using System;
namespace DynamicDataClientSite.ServiceReference1 {

    public partial class NorthwindEntities : global::System.Data.Services.Client.DataServiceContext {
        public NorthwindEntities() : this(new Uri("http://localhost:34189/WcfDataService1.svc/")) { }

        partial void OnContextCreated() {
            this.SendingRequest += new System.EventHandler<System.Data.Services.Client.SendingRequestEventArgs>(NorthwindClientEntities_SendingRequest);
        }

        void NorthwindClientEntities_SendingRequest(object sender, System.Data.Services.Client.SendingRequestEventArgs e) {
            // Write some logging information to the page to demonstrate the ADO.NET Data Service requests
            // that are being made.
            HttpContext.Current.Response.Write(e.Request.RequestUri + "<br>");
        }
    }
}
