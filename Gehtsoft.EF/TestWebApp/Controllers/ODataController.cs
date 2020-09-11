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
        private ODataProcessor mPocessor;

        public ODataController(EdmModelBuilder edmModelBuilder, ISqlDbConnectionFactory connectionFactory)
        {
            mPocessor = new ODataProcessor(connectionFactory, edmModelBuilder);
            mPocessor.ODataCountName = "@odata.count"; // Kendo specific
            //mPocessor.ODataCountName = "__count"; // Kendo specific
            mPocessor.ODataMetadataName = "@odata.context"; // Kendo specific
        }

        [HttpGet("Order")]
        public string Order()
        {
            mPocessor.Root = $"{Request.Scheme}://{Request.Host.Value}/OData";
            string result = mPocessor.GetFormattedData(new Uri($"/Order{Request.QueryString}", UriKind.Relative));

            return result;
        }

        [HttpGet("Product({key}")]
        public string GetProduct(int key)
        {
            mPocessor.Root = $"{Request.Scheme}://{Request.Host.Value}/OData";
            string result = mPocessor.GetFormattedData(new Uri($"/Product(){Request.QueryString}", UriKind.Relative));

            return result;
        }

        [HttpGet("Product")]
        public string Product()
        {
            mPocessor.Root = $"{Request.Scheme}://{Request.Host.Value}/OData";
            string result = mPocessor.GetFormattedData(new Uri($"/Product{Request.QueryString}", UriKind.Relative));

            return result;
        }

        [HttpPut("Product({key})")]
        public IActionResult UpdateProduct(int key)
        {
            bool wasError;
            string result = mPocessor.UpdateRecord("Product", BodyContent, key, out wasError);
            if (wasError)
                return StatusCode((int)HttpStatusCode.InternalServerError, result);
            else
                return Ok(result);
        }

        [HttpPost("Product")]
        public IActionResult NewProduct()
        {
            bool wasError;
            string result = mPocessor.AddNewRecord("Product", BodyContent, out wasError);
            if (wasError)
                return StatusCode((int)HttpStatusCode.InternalServerError, result);
            else
                return Ok(result);
        }

        [HttpDelete("Product({key})")]
        public IActionResult DeleteProduct(int key)
        {
            string result = mPocessor.RemoveRecord("Product", key);
            if (string.IsNullOrEmpty(result))
                return StatusCode((int)HttpStatusCode.NoContent);
            else
                return StatusCode((int)HttpStatusCode.InternalServerError, result);
        }

        [HttpGet("Category")]
        public string Category()
        {
            mPocessor.Root = $"{Request.Scheme}://{Request.Host.Value}/OData";
            string result = mPocessor.GetFormattedData(new Uri($"/Category{Request.QueryString}", UriKind.Relative));

            return result;
        }

        [HttpGet("Supplier")]
        public string Supplier()
        {
            mPocessor.Root = $"{Request.Scheme}://{Request.Host.Value}/OData";
            string result = mPocessor.GetFormattedData(new Uri($"/Supplier{Request.QueryString}", UriKind.Relative));

            return result;
        }

        private string BodyContent
        {
            get
            {
                using (Stream receiveStream = Request.Body)
                {
                    using (StreamReader readStream = new StreamReader(receiveStream))
                    {
                        return readStream.ReadToEnd();
                    }
                }
            }
        }
    }
}