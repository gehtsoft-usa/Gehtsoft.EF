using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb.OData;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Legacy.Entities;
using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Csdl;
using Microsoft.OData.Edm.Validation;
using Microsoft.OData.UriParser;
using Xunit;

namespace Gehtsoft.EF.Test.Legacy
{
    public class ODataModelTests
    {
        [Fact]
        public void ModelBuilderTest()
        {
            //check expected result
            var entities = EntityFinder.FindEntities(new Assembly[] { this.GetType().Assembly }, "entities", false);
            var builder = new EdmModelBuilder();
            builder.Build(entities, "entities");
            IEdmModel model = builder.Model;
            model.Should().NotBeNull();
            model.FindType("entities." + nameof(Employee) + "_Type").Should().NotBeNull();
            model.FindType("entities." + nameof(Good) + "_Type").Should().NotBeNull();
            model.FindType("entities." + nameof(Category) + "_Type").Should().NotBeNull();
            model.FindType("entities." + nameof(Sale) + "_Type").Should().NotBeNull();

            var sale = model.FindType("entities." + nameof(Sale) + "_Type");
            sale.SchemaElementKind.Should().Be(EdmSchemaElementKind.TypeDefinition);
            var saleDefinition = sale as IEdmEntityType;
            saleDefinition.Should().NotBeNull();
            saleDefinition.FindProperty(nameof(Sale.ID)).Should().NotBeNull();
            saleDefinition.FindProperty(nameof(Sale.SalesDate)).Should().NotBeNull();
            saleDefinition.FindProperty(nameof(Sale.SalesPerson)).Should().NotBeNull();
            saleDefinition.FindProperty(nameof(Sale.Good)).Should().NotBeNull();
            saleDefinition.FindProperty(nameof(Sale.Total)).Should().NotBeNull();

            model.EntityContainer.FindEntitySet(nameof(Employee)).Should().NotBeNull();
            var es = model.EntityContainer.FindEntitySet(nameof(Employee));
            es.EntityType.Should().Be(model.FindType("entities." + nameof(Employee) + "_Type"));

            //try to save model into XML
            using (var stringWriter = new StringWriter())
            {
                using (var writer = XmlWriter.Create(stringWriter))
                {
                    CsdlWriter.TryWriteCsdl(model, writer, CsdlTarget.OData, out IEnumerable<EdmError> _);
                }
                var csdl = stringWriter.ToString();
            }

            //check that model is enough to parse OData queries

            //entity
            ODataUriParser odataUriParser = new ODataUriParser(model, new Uri(nameof(Employee), UriKind.Relative));
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
            odataUriParser = new ODataUriParser(model, new Uri(nameof(Employee) + "1", UriKind.Relative));
            ((Action)(() => odataUriParser.ParseUri())).Should().Throw<ODataUnrecognizedPathException>();
        }

        [Fact]
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
