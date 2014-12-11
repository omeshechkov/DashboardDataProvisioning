using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web.Http;
using System.Xml;
using System.Xml.Linq;
using DashboardDataProvisioning.Configuration;
using log4net;
using Oracle.DataAccess.Client;
using Saxon.Api;

namespace DashboardDataProvisioning.Controllers
{
    public class GateController : ApiController
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(GateController));

        private readonly string _scenariosPath;
        private readonly string _connectionString;

        public GateController()
        {
            Log.Info("Initialized...");

            try
            {
                var configuration = (ProvisioningSection)ConfigurationManager.GetSection(ProvisioningSection.Name);
                _scenariosPath = configuration.Scenarios.Path;

                _connectionString = ConfigurationManager.ConnectionStrings["source"].ConnectionString;
            }
            catch (Exception ex)
            {
                Log.Error("Cannot read configuration", ex);
            }
        }

        [HttpGet]
        public HttpResponseMessage RunScenario(string path)
        {
            var scenarioPath = Path.Combine(_scenariosPath, path);

            XElement xScenario;
            try
            {
                xScenario = XElement.Load(scenarioPath);
            }
            catch (Exception ex)
            {
                Log.Error("Cannot load scenario", ex);
                return GetMessage(HttpStatusCode.NotFound, null);
            }

            try
            {
                var sources = xScenario.Element("Sources").Elements("Source").Select(s => GetAttribute(s, "name"));

                var xData = new XElement("Data");

                foreach (var source in sources)
                {
                    var xSource = new XElement("Source", new XAttribute("name", source));
                    ProvideData(xSource, source);

                    xData.Add(xSource);
                }

                var transformFileName = xScenario.Element("TransformFileName").Value;

                return GetMessage(HttpStatusCode.OK, Transform(xData, transformFileName));

            }
            catch (Exception ex)
            {
                Log.Error("Internal system error", ex);
                return GetMessage(HttpStatusCode.InternalServerError, null);
            }
        }

        public void ProvideData(XElement xSource, string name)
        {
            using (var connection = new OracleConnection(_connectionString))
            {
                connection.Open();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "select * from " + name;

                    using (var dataReader = command.ExecuteReader())
                    {
                        while (dataReader.Read())
                        {
                            var xRow = new XElement("Row");
                            xSource.Add(xRow);
                            for (var i = 0; i < dataReader.FieldCount; i++)
                            {
                                var xColumn = new XElement(dataReader.GetName(i), dataReader.GetValue(i));
                                xRow.Add(xColumn);
                            }
                        }
                    }
                }
            }
        }

        private static string Transform(XElement inputData, string transformFileName)
        {
            var processor = new Processor();

            var docBuilder = processor.NewDocumentBuilder();

            var doc = new XmlDocument();
            doc.LoadXml(inputData.ToString());
            var inputDocument = docBuilder.Wrap(doc);

            var xsltCompiler = processor.NewXsltCompiler();
            var xsltExec = xsltCompiler.Compile(new Uri(transformFileName));
            var saxonTransformer = xsltExec.Load();

            saxonTransformer.InitialContextNode = inputDocument;

            var outputDocument = new DomDestination();
            saxonTransformer.Run(outputDocument);

            var sb = new StringBuilder();

            using (var stringWriter = new StringWriter(sb))
            {
                using (var xmlWriter = XmlWriter.Create(stringWriter, new XmlWriterSettings { CheckCharacters = false, Indent = true, OmitXmlDeclaration = true}))
                    outputDocument.XmlDocument.WriteTo(xmlWriter);
            }

            return sb.ToString();
        }

        private static HttpResponseMessage GetMessage(HttpStatusCode code, string content)
        {
            return new HttpResponseMessage(code)
            {
                Content = new StringContent(content, Encoding.UTF8, "text/xml")
            };
        }

        public static string GetAttribute(XElement xElement, string attributeName)
        {
            var xAttr = xElement.Attribute(attributeName);

            return xAttr == null ? string.Empty : xAttr.Value;
        }
    }
}
