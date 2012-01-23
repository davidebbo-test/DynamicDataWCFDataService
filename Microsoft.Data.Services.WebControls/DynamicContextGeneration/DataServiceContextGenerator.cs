using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Data.Services.Client;
using System.Xml.Linq;
using System.Web;
using System.Net;
using System.Data.Services.Common;

namespace Microsoft.Data.Services.WebControls {
    public class DataServiceContextGenerator {
        private ModuleBuilder _moduleBuilder;
        private TypeBuilder _contextTypeBuilder;
        private Dictionary<string, TypeBuilder> _entityTypeBuilders = new Dictionary<string, TypeBuilder>();
        internal Type GeneratedType { get; private set; }

        public DataServiceContextGenerator(Uri serviceRoot) {
            AssemblyName myAsmName = new AssemblyName("DynamicAssembly" + Guid.NewGuid().ToString());

            AssemblyBuilder myAsmBuilder = Thread.GetDomain().DefineDynamicAssembly(myAsmName,
                                                            AssemblyBuilderAccess.Run);
            // Generate a single-module assembly.
            _moduleBuilder =
                myAsmBuilder.DefineDynamicModule(myAsmName.Name);
            _contextTypeBuilder = _moduleBuilder.DefineType("MyDataContext",
                                                            TypeAttributes.Public,
                                                            typeof(MyDataServiceContext));
            ConstructorBuilder ctorBuilder = _contextTypeBuilder.DefineConstructor(
                MethodAttributes.Public,
                CallingConventions.Standard,
                null);
            ILGenerator ctorIL = ctorBuilder.GetILGenerator();
            ctorIL.Emit(OpCodes.Ldarg_0);
            ctorIL.Emit(OpCodes.Ldstr, serviceRoot.AbsoluteUri);
            ctorIL.Emit(OpCodes.Newobj, typeof(Uri).GetConstructor(new[] { typeof(string) }));
            ctorIL.Emit(OpCodes.Call, typeof(MyDataServiceContext).GetConstructors().First());
            ctorIL.Emit(OpCodes.Ret);
        }

        public Type AddEntitySet(string entityTypeName, string entitySetName, IEnumerable<string> pkNames) {
            TypeBuilder entityTypeBuilder = _moduleBuilder.DefineType(entityTypeName, TypeAttributes.Public);

            // If there are any PKs, and a DataServiceKeyAttribute attribute
            if (pkNames.Any()) {
                var pkNamesArray = pkNames.ToArray();
                ConstructorInfo dataServiceKeyCtor = typeof(DataServiceKeyAttribute).GetConstructor(
                    new[] { typeof(string[]) });

                entityTypeBuilder.SetCustomAttribute(
                    new CustomAttributeBuilder(dataServiceKeyCtor, new[] { pkNamesArray }));
            }

            _entityTypeBuilders[entityTypeName] = entityTypeBuilder;

            // Add the entity set property on the context
            AddEntitySetProperty(entityTypeBuilder, entitySetName);

            return entityTypeBuilder;
        }

        public Type AddComplexType(string typeName) {
            TypeBuilder entityTypeBuilder = _moduleBuilder.DefineType(typeName, TypeAttributes.Public);

            _entityTypeBuilders[typeName] = entityTypeBuilder;

            return entityTypeBuilder;
        }

        public void AddColumnProperty(string entityTypeName, Type propertyType, string propertyName, bool nullable = true) {
            TypeBuilder entityTypeBuilder = _entityTypeBuilders[entityTypeName];
            AddColumnProperty(entityTypeBuilder, propertyType, propertyName);
        }

        public Dictionary<Type, Type> GenerateContextType() {
            Dictionary<Type, Type> typeBuilderToRealTypeMapping = new Dictionary<Type, Type>();

            if (GeneratedType == null) {
                foreach (var typeBuilder in _entityTypeBuilders.Values) {
                    typeBuilderToRealTypeMapping[typeBuilder] = typeBuilder.CreateType();
                }

                GeneratedType = _contextTypeBuilder.CreateType();
                typeBuilderToRealTypeMapping[_contextTypeBuilder] = GeneratedType;

                AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);
            }

            return typeBuilderToRealTypeMapping;
        }

        private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args) {
            if (args.Name == GeneratedType.Assembly.FullName) {
                return GeneratedType.Assembly;
            }

            return null;
        }

        private void AddEntitySetProperty(Type entityType, string entitySetName) {

            Type propertyType = typeof(DataServiceQuery<>).MakeGenericType(entityType);

            FieldBuilder fieldBldr = _contextTypeBuilder.DefineField("__" + entitySetName,
                                                            propertyType,
                                                            FieldAttributes.Private);

            // The last argument of DefineProperty is null, because the
            // property has no parameters. (If you don't specify null, you must
            // specify an array of Type objects. For a parameterless property,
            // use an array with no elements: new Type[] {})
            PropertyBuilder propBldr = _contextTypeBuilder.DefineProperty(entitySetName,
                                                             PropertyAttributes.HasDefault,
                                                             propertyType,
                                                             null);

            // The property set and property get methods require a special
            // set of attributes.
            MethodAttributes getSetAttr =
                MethodAttributes.Public | MethodAttributes.SpecialName |
                    MethodAttributes.HideBySig;

            // Define the "get" accessor method.
            MethodBuilder getPropMthdBldr =
                _contextTypeBuilder.DefineMethod("get_" + entitySetName,
                                           getSetAttr,
                                           propertyType,
                                           Type.EmptyTypes);

            ILGenerator getterIL = getPropMthdBldr.GetILGenerator();

            // Generate getter that looks like:
            //public global::System.Data.Services.Client.DataServiceQuery<Category> Categories
            //{
            //    get {
            //        if ((this._Categories == null)) {
            //            this._Categories = base.CreateQuery<Category>("Categories");
            //        }
            //        return this._Categories;
            //    }
            //}

            getterIL.DeclareLocal(propertyType);
            getterIL.DeclareLocal(typeof(bool));

            getterIL.Emit(OpCodes.Ldarg_0);             // this
            getterIL.Emit(OpCodes.Ldfld, fieldBldr);    // this.__categories
            getterIL.Emit(OpCodes.Ldnull);              // null
            getterIL.Emit(OpCodes.Ceq);                 // ==
            Label label1 = getterIL.DefineLabel();
            getterIL.Emit(OpCodes.Brfalse_S, label1);

            getterIL.Emit(OpCodes.Ldarg_0);
            getterIL.Emit(OpCodes.Ldarg_0);
            getterIL.Emit(OpCodes.Ldstr, entitySetName);
            MethodInfo createQueryGenericMethod = typeof(DataServiceContext).GetMethod("CreateQuery");
            MethodInfo createQueryMethod = createQueryGenericMethod.MakeGenericMethod(entityType);
            getterIL.Emit(OpCodes.Call, createQueryMethod); // base.CreateQuery<Category>("Categories")
            getterIL.Emit(OpCodes.Stfld, fieldBldr);        // Store in this.__categories

            getterIL.MarkLabel(label1);
            getterIL.Emit(OpCodes.Ldarg_0);
            getterIL.Emit(OpCodes.Ldfld, fieldBldr);
            getterIL.Emit(OpCodes.Ret);                     // return this._Categories;

            propBldr.SetGetMethod(getPropMthdBldr);
        }

        private void AddColumnProperty(TypeBuilder entityTypeBuilder, Type propertyType, string propertyName) {
            FieldBuilder fieldBldr = entityTypeBuilder.DefineField("__" + propertyName,
                                                            propertyType,
                                                            FieldAttributes.Private);

            // The last argument of DefineProperty is null, because the
            // property has no parameters. (If you don't specify null, you must
            // specify an array of Type objects. For a parameterless property,
            // use an array with no elements: new Type[] {})
            PropertyBuilder propBldr = entityTypeBuilder.DefineProperty(propertyName,
                                                             PropertyAttributes.HasDefault,
                                                             propertyType,
                                                             null);

            // The property set and property get methods require a special
            // set of attributes.
            MethodAttributes getSetAttr =
                MethodAttributes.Public | MethodAttributes.SpecialName |
                    MethodAttributes.HideBySig;

            // Define the "get" accessor method.
            MethodBuilder getPropMthdBldr =
                entityTypeBuilder.DefineMethod("get_" + propertyName,
                                           getSetAttr,
                                           propertyType,
                                           Type.EmptyTypes);

            ILGenerator custNameGetIL = getPropMthdBldr.GetILGenerator();

            custNameGetIL.Emit(OpCodes.Ldarg_0);
            custNameGetIL.Emit(OpCodes.Ldfld, fieldBldr);
            custNameGetIL.Emit(OpCodes.Ret);

            // Define the "set" accessor method.
            MethodBuilder setPropMthdBldr =
                entityTypeBuilder.DefineMethod("set_" + propertyName,
                                           getSetAttr,
                                           null,
                                           new Type[] { propertyType });

            ILGenerator custNameSetIL = setPropMthdBldr.GetILGenerator();

            custNameSetIL.Emit(OpCodes.Ldarg_0);
            custNameSetIL.Emit(OpCodes.Ldarg_1);
            custNameSetIL.Emit(OpCodes.Stfld, fieldBldr);
            custNameSetIL.Emit(OpCodes.Ret);

            // Last, we must map the two methods created above to our PropertyBuilder to 
            // their corresponding behaviors, "get" and "set" respectively. 
            propBldr.SetGetMethod(getPropMthdBldr);
            propBldr.SetSetMethod(setPropMthdBldr);
        }
    }

    public class MyDataServiceContext : DataServiceContext {
        public MyDataServiceContext(Uri serviceRoot) : base(serviceRoot) {
            //IgnoreMissingProperties = true;
            SendingRequest += new EventHandler<SendingRequestEventArgs>(MyDataServiceContext_SendingRequest);
        }

        void MyDataServiceContext_SendingRequest(object sender, SendingRequestEventArgs e) {
            // Write some logging information to the page to demonstrate the ADO.NET Data Service requests
            // that are being made.
            HttpContext.Current.Response.Write("<div class=\"DD\">" + e.Request.RequestUri + "</div>");
        }
    }
}
