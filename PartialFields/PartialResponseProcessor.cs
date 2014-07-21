using System;
using System.IO;
using System.ServiceModel.Channels;
using System.ServiceModel.Configuration;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using System.Text;
using System.Web;
using System.Xml;
using System.Xml.Linq;

namespace PartialFields
{
    /// <summary>
    /// WCF behavior extension class for processing outgoing message to send partial response.
    /// </summary>
    public class PartialResponseProcessor : BehaviorExtensionElement, IDispatchMessageInspector, IEndpointBehavior
    {
        #region Public Methods

        #region BehaviorExtensionElement Methods

        /// <inheritDoc />
        public override Type BehaviorType
        {
            get { return typeof(PartialResponseProcessor); }
        }

        /// <inheritDoc />
        protected override object CreateBehavior()
        {
            return new PartialResponseProcessor();
        }

        #endregion BehaviorExtensionElement Methods

        #region IDispatchMessageInspector Members

        /// <inheritDoc />
        public object AfterReceiveRequest(ref System.ServiceModel.Channels.Message request, System.ServiceModel.IClientChannel channel, System.ServiceModel.InstanceContext instanceContext)
        {
            return null;
        }

        /// <inheritDoc />
        public void BeforeSendReply(ref System.ServiceModel.Channels.Message reply, object correlationState)
        {
            string fields = HttpContext.Current.Request.QueryString["fields"];

            if (String.IsNullOrEmpty(fields))
            {
                return;
            }

            reply = ProcessFields(reply, fields);
        }

        #endregion IDispatchMessageInspector Members

        #region IEndpointBehavior Members

        /// <inheritDoc />
        public void AddBindingParameters(ServiceEndpoint endpoint, System.ServiceModel.Channels.BindingParameterCollection bindingParameters)
        {
            return;
        }

        /// <inheritDoc />
        public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
        {
            return;
        }

        /// <inheritDoc />
        public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
        {
            endpointDispatcher.DispatchRuntime.MessageInspectors.Add(this);
        }

        /// <inheritDoc />
        public void Validate(ServiceEndpoint endpoint)
        {
            return;
        }

        #endregion IEndpointBehavior Members

        #region Class Methods

        /// <summary>
        /// Processes the given list of comma separated field names and removes the field values if corresponding field name is not present in the given field list.
        /// </summary>
        /// <param name="oldMessage">Original outgoing message.</param>
        /// <param name="fields">Comma seperated list of fields to be returned.</param>
        /// <returns>New outgoing message having only the required fields.</returns>
        public Message ProcessFields(Message oldMessage, string fields)
        {
            MemoryStream memoryStream = new MemoryStream();
            XmlWriter xmlWriter = XmlWriter.Create(memoryStream);
            oldMessage.WriteMessage(xmlWriter);
            xmlWriter.Flush();
            string body = Encoding.UTF8.GetString(memoryStream.ToArray());
            body = RemoveUtf8ByteOrderMark(body);
            xmlWriter.Close();

            XMLDocumentHelper xmlHelper = new XMLDocumentHelper();

            XDocument newBody = XDocument.Parse(xmlHelper.GetProcessedXml(body, fields));

            memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(newBody.ToString()));
            XmlDictionaryReader xdr = XmlDictionaryReader.CreateTextReader(memoryStream, new XmlDictionaryReaderQuotas());
            Message newMessage = Message.CreateMessage(xdr, int.MaxValue, oldMessage.Version);
            newMessage.Properties.CopyProperties(oldMessage.Properties);
            return newMessage;

        }

        /// <summary>
        /// Removes UTF-8 byte order mark from given XML string.
        /// </summary>
        /// <param name="xml">XML string.</param>
        /// <returns>Given XML string without UTF-8 byte order mark.</returns>
        public static string RemoveUtf8ByteOrderMark(string xml)
        {
            string byteOrderMarkUtf8 = Encoding.UTF8.GetString(Encoding.UTF8.GetPreamble());
            if (xml.StartsWith(byteOrderMarkUtf8))
            {
                xml = xml.Remove(0, byteOrderMarkUtf8.Length);
            }
            return xml;
        }

        #endregion Class Methods

        #endregion Public Methods
    }
}