using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.OData;
using Gehtsoft.EF.Northwind;
using Microsoft.AspNetCore.Mvc;

namespace TestWebApp.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class ODataController : Controller
    {
        private readonly ODataProcessor mProcessor;

        public ODataController(EdmModelBuilder edmModelBuilder, ISqlDbConnectionFactory connectionFactory)
        {
            mProcessor = new ODataProcessor(connectionFactory, edmModelBuilder)
            {
                ODataCountName = "@odata.count", // Kendo specific
                ODataMetadataName = "@odata.context" // Kendo specific
            };
        }

        [HttpGet("Order")]
        public string Order()
        {
            mProcessor.Root = $"{Request.Scheme}://{Request.Host.Value}/OData";
            string result = mProcessor.GetFormattedData(new Uri($"/Order{Request.QueryString}", UriKind.Relative));

            return result;
        }

        [HttpGet("Product({key}")]
        public string GetProduct(int key)
        {
            mProcessor.Root = $"{Request.Scheme}://{Request.Host.Value}/OData";
            return mProcessor.GetFormattedData(new Uri($"/Product(){Request.QueryString}", UriKind.Relative));
        }

        [HttpGet("Product")]
        public string Product()
        {
            mProcessor.Root = $"{Request.Scheme}://{Request.Host.Value}/OData";
            string result = mProcessor.GetFormattedData(new Uri($"/Product{Request.QueryString}", UriKind.Relative));

            return result;
        }

        [HttpPut("Product({key})")]
        public IActionResult UpdateProduct(int key)
        {
            string result = mProcessor.UpdateRecord("Product", BodyContent, key, out bool wasError);
            if (wasError)
                return StatusCode((int)HttpStatusCode.InternalServerError, result);
            else
                return Ok(result);
        }

        [HttpPost("Product")]
        public IActionResult NewProduct()
        {
            string result = mProcessor.AddNewRecord("Product", BodyContent, out bool wasError);
            if (wasError)
                return StatusCode((int)HttpStatusCode.InternalServerError, result);
            else
                return Ok(result);
        }

        [HttpDelete("Product({key})")]
        public IActionResult DeleteProduct(int key)
        {
            string result = mProcessor.RemoveRecord("Product", key);
            if (string.IsNullOrEmpty(result))
                return StatusCode((int)HttpStatusCode.NoContent);
            else
                return StatusCode((int)HttpStatusCode.InternalServerError, result);
        }

        [HttpGet("Category")]
        public string Category()
        {
            mProcessor.Root = $"{Request.Scheme}://{Request.Host.Value}/OData";
            string result = mProcessor.GetFormattedData(new Uri($"/Category{Request.QueryString}", UriKind.Relative));

            return result;
        }

        [HttpGet("Supplier")]
        public string Supplier()
        {
            mProcessor.Root = $"{Request.Scheme}://{Request.Host.Value}/OData";
            string result = mProcessor.GetFormattedData(new Uri($"/Supplier{Request.QueryString}", UriKind.Relative));

            return result;
        }

        private string BodyContent
        {
            get
            {
                using (Stream receiveStream = Request.Body)
                {
                    using (var readStream = new StreamReader(receiveStream))
                    {
                        return readStream.ReadToEnd();
                    }
                }
            }
        }
    }
}