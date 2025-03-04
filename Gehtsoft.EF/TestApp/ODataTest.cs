using Gehtsoft.EF.Db.SqlDb.OData;
using Gehtsoft.EF.Entities;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.OData.Edm;
using FluentAssertions;
using Microsoft.OData.UriParser;
using System.Security.Policy;
using Microsoft.OData;
using System.Xml;
using System.IO;
using Microsoft.OData.Edm.Validation;
using Microsoft.OData.Edm.Csdl;

namespace TestApp
{
    [TestFixture]
    public class ODataTest
    {
        [Test]
        public void ModelBuilderTest()
        {
            //check expected result
            var entities = EntityFinder.FindEntities(new Assembly[] { this.GetType().Assembly }, "entities", false);
            var builder = new EdmModelBuilder();
            builder.Build(entities, "entities");
            IEdmModel model = builder.Model;
            model.Should().NotBeNull();
            model.FindType("entities." + nameof(TestEntity1.Employee) + "_Type").Should().NotBeNull();
            model.FindType("entities." + nameof(TestEntity1.Good) + "_Type").Should().NotBeNull();
            model.FindType("entities." + nameof(TestEntity1.Category) + "_Type").Should().NotBeNull();
            model.FindType("entities." + nameof(TestEntity1.Sale) + "_Type").Should().NotBeNull();

            var sale = model.FindType("entities." + nameof(TestEntity1.Sale) + "_Type");
            sale.SchemaElementKind.Should().Be(EdmSchemaElementKind.TypeDefinition);
            var saleDefinition = sale as IEdmEntityType;
            saleDefinition.Should().NotBeNull();
            saleDefinition.FindProperty(nameof(TestEntity1.Sale.ID)).Should().NotBeNull();
            saleDefinition.FindProperty(nameof(TestEntity1.Sale.SalesDate)).Should().NotBeNull();
            saleDefinition.FindProperty(nameof(TestEntity1.Sale.SalesPerson)).Should().NotBeNull();
            saleDefinition.FindProperty(nameof(TestEntity1.Sale.Good)).Should().NotBeNull();
            saleDefinition.FindProperty(nameof(TestEntity1.Sale.Total)).Should().NotBeNull();

            model.EntityContainer.FindEntitySet(nameof(TestEntity1.Employee)).Should().NotBeNull();
            var es = model.EntityContainer.FindEntitySet(nameof(TestEntity1.Employee));
            es.EntityType.Should().Be(model.FindType("entities." + nameof(TestEntity1.Employee) + "_Type"));

            //try to save model into XML
            using (var stringWriter = new StringWriter())
            {
                using (var writer = XmlWriter.Create(stringWriter))
                {
                    CsdlWriter.TryWriteCsdl(model, writer, CsdlTarget.OData, out IEnumerable<EdmError> _);
                }
                var csdl = stringWriter.ToString();
                Console.Write("{0}", csdl);
            }

            //check that model is enough to parse OData queries

            //entity
            ODataUriParser odataUriParser = new ODataUriParser(model, new Uri(nameof(TestEntity1.Employee), UriKind.Relative));
            ODataUri odataUri = odataUriParser.ParseUri();
            odataUri.Should().NotBeNull();

            //count of entities
            odataUriParser = new ODataUriParser(model, new Uri("/Employee/$count", UriKind.Relative));
            odataUri = odataUriParser.ParseUri();
            odataUri.Should().NotBeNull();

            //expand (*) to (1) relationship
            odataUriParser = new ODataUriParser(model, new Uri("/Sale(1)/Good", UriKind.Relative));
            odataUri = odataUriParser.ParseUri();
            odataUri.Should().NotBeNull();

            //expand (*) to (1) relationship
            odataUriParser = new ODataUriParser(model, new Uri("/Good(1)/Category", UriKind.Relative));
            odataUri = odataUriParser.ParseUri();
            odataUri.Should().NotBeNull();

            //expand entity property
            odataUriParser = new ODataUriParser(model, new Uri("/Good(1)/Name", UriKind.Relative));
            odataUri = odataUriParser.ParseUri();
            odataUri.Should().NotBeNull();

            //expand (*) to (1) relationship and then (*) to (1) relationship again
            odataUriParser = new ODataUriParser(model, new Uri("Sale(1)/Good/Category", UriKind.Relative));
            odataUri = odataUriParser.ParseUri();
            odataUri.Should().NotBeNull();

            //expand (1) to (*) relationship
            odataUriParser = new ODataUriParser(model, new Uri("/Good(1)/Sale", UriKind.Relative));
            odataUri = odataUriParser.ParseUri();
            odataUri.Should().NotBeNull();

            //expand (*) to (1) relationship and then (1) to (*) relationship
            odataUriParser = new ODataUriParser(model, new Uri("/Sale(1)/Good/Sale", UriKind.Relative));
            odataUri = odataUriParser.ParseUri();
            odataUri.Should().NotBeNull();

            odataUriParser = new ODataUriParser(model, new Uri("/Sale?$select=ID,SalesDate&$expand=Good", UriKind.Relative));
            odataUri = odataUriParser.ParseUri();
            odataUri.Should().NotBeNull();

            //expand operator
            odataUriParser = new ODataUriParser(model, new Uri("/Good?$expand=Sale", UriKind.Relative));
            odataUri = odataUriParser.ParseUri();
            odataUri.Should().NotBeNull();

            //check incorrect queries
            odataUriParser = new ODataUriParser(model, new Uri(nameof(TestEntity1.Employee) + "1", UriKind.Relative));
            ((Action)(() => odataUriParser.ParseUri())).Should().Throw<ODataUnrecognizedPathException>();
        }

        [Test]
        public void ParsingTest()
        {
            var entities = EntityFinder.FindEntities(new Assembly[] { this.GetType().Assembly }, "entities", false);
            var builder = new EdmModelBuilder();
            builder.Build(entities, "entities");
            IEdmModel model = builder.Model;

            ODataUriParser odataUriParser = new ODataUriParser(model, new Uri("/Good/$count", UriKind.Relative));
            ODataUri odataUri = odataUriParser.ParseUri();
            odataUri.Should().NotBeNull();
        }
    }
}