﻿<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Test.aspx.cs" Inherits="DynamicDataClientSite.RegularPage.Test" %>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">

<html xmlns="http://www.w3.org/1999/xhtml" >
<head runat="server">
    <title></title>
</head>
<body>
    <h2>Example of using DataServiceLinqDataSource outside Dynamic Data</h2>
    <form id="form1" runat="server">
    <div>
        <asp:GridView ID="GridView1" runat="server" DataSourceID="GridDataSource"
            AutoGenerateEditButton="true" AutoGenerateDeleteButton="true"
            AllowPaging="True" AllowSorting="True">
        </asp:GridView>

        <asp:DataServiceLinqDataSource ID="GridDataSource"
            ContextTypeName="NORTHWNDModel.NorthwindClientEntities" TableName="Products"
            runat="server" EnableDelete="true" EnableUpdate="true">
        </asp:DataServiceLinqDataSource>
    </div>
    </form>
</body>
</html>