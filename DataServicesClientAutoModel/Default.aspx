<%@ Page Title="" Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="DynamicDataClientSite.Default" %>
<asp:Content ID="Content1" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
    <asp:ScriptManagerProxy ID="ScriptManagerProxy2" runat="server" />

    <span runat="server" id="tableList">

    <h2 class="DDSubHeader">My tables</h2>

    <br /><br />

    <asp:GridView ID="Menu1" runat="server" AutoGenerateColumns="false"
        CssClass="DDGridView" RowStyle-CssClass="td" HeaderStyle-CssClass="th" CellPadding="6">
        <Columns>
            <asp:TemplateField HeaderText="Table Name" SortExpression="TableName">
                <ItemTemplate>
                    <asp:DynamicHyperLink ID="HyperLink1" runat="server"><%# Eval("DisplayName") %></asp:DynamicHyperLink>
                </ItemTemplate>
            </asp:TemplateField>
        </Columns>
    </asp:GridView>
    </span>

    <br />
    <span class="DD">
    <div class="DDSubHeader">Enter the service URL and capabilities</div>
    <div>
    <asp:CheckBox ID="CheckBoxSupportEditing" runat="server" Text="Service supports editing" />
    <asp:CheckBox ID="CheckBoxSupportPagingSorting" runat="server" Text="Service supports paging/sorting" />
    </div><br />
    <div>
    <asp:TextBox ID="TextBox1" runat="server" Width="332px" CssClass="DDTextBox"></asp:TextBox> 
    <asp:Button ID="Button1" runat="server" Text="Navigate Model" 
        onclick="Button1_Click" />
    </div>

    <br />
    <br />
    <div class="DDSubHeader">For instance, try one of the following services</div>
    <p>
        http://services.odata.org/(S(readwrite))/OData/OData.svc/  --> Full Access (Read-Write) Service<br />
        http://services.odata.org/OData/OData.svc/ --> Read-Only service<br />
        http://services.odata.org/Northwind/Northwind.svc --> Read-Only Northwind Service <br />
        http://api.visitmix.com/OData.svc/ <br />
        http://datafeed.edmonton.ca/v1/coe/<br />
        http://ogdi.cloudapp.net/v1/dc<br />
        http://odata.netflix.com/Catalog.svc/<br />
        http://davidebb-dev10/DataServicesServer/WcfDataService1.svc/<br />
    </p>
    </span>
</asp:Content>
