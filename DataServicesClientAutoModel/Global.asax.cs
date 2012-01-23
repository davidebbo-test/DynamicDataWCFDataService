using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Security;
using System.Web.SessionState;
using System.Net;

namespace DynamicDataClientSite {
    public class Global : System.Web.HttpApplication {

        protected void Application_Start(object sender, EventArgs e) {
            // Set up the proxy
            WebRequest.DefaultWebProxy = new WebProxy("http://itgproxy") { BypassProxyOnLocal = true };
        }
    }
}