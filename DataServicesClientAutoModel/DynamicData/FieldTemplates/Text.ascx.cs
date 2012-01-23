using System;
using System.Collections.Specialized;
using System.ComponentModel.DataAnnotations;
using System.Web.DynamicData;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace DynamicDataClientSite {
    public partial class TextField : System.Web.DynamicData.FieldTemplateUserControl {
        private const int MAX_DISPLAYLENGTH_IN_LIST = 25;

        public override void DataBind() {
            base.DataBind();

            string value = FieldValueString;
            if (String.IsNullOrEmpty(value))
                return;

            // If it looks like an image link, show the image
            if (value.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)) {
                Image1.ImageUrl = value;
                Image1.Visible = true;
            }
            else {
                if (ContainerType == ContainerType.List) {
                    if (value.Length > MAX_DISPLAYLENGTH_IN_LIST) {
                        value = value.Substring(0, MAX_DISPLAYLENGTH_IN_LIST - 3) + "...";
                    }
                }

                Literal1.Text = value;
                Literal1.Visible = true;
            }
        }

        public override Control DataControl {
            get {
                return Literal1;
            }
        }
    }
}
