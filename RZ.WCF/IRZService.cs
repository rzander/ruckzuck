using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;

namespace RuckZuck_WCF
{
    [ServiceContract]
    public interface IRZService
    {
        [OperationContract]
        [WebGet(UriTemplate = "AuthenticateUser")]
        string AuthenticateUser();

        [OperationContract]
        [WebGet(UriTemplate = "SWResults?search={SearchPattern}")]
        List<GetSoftware> SWResults(string SearchPattern);

        [OperationContract]
        [WebGet(UriTemplate = "SWGet?name={PkgName}&ver={PkgVersion}")]
        List<GetSoftware> SWGetByPkgNameAndVersion(string PkgName, string PkgVersion);

        [OperationContract]
        [WebGet(UriTemplate = "SWGetPkg?name={PkgName}&manuf={Manufacturer}&ver={PkgVersion}")]
        List<GetSoftware> SWGetByPkg(string PkgName, string Manufacturer, string PkgVersion);

        [OperationContract]
        [WebGet(UriTemplate = "SWGetShort?name={Shortname}")]
        List<GetSoftware> SWGetByShortname(string Shortname);

        [OperationContract]
        [WebInvoke(UriTemplate = "UploadSWEntry", Method = "POST",
            BodyStyle = WebMessageBodyStyle.Bare)]
        bool UploadSWEntry(AddSoftware SoftwareItem);

        [OperationContract]
        [WebGet(UriTemplate = "GetSWDefinition?name={productName}&ver={productVersion}&man={manufacturer}")]
        List<AddSoftware> GetSWDefinitions(string productName, string productVersion, string manufacturer);

        /*[OperationContract] //(IsOneWay = true)
        [WebGet(UriTemplate = "Feedback?name={productName}&ver={productVersion}&ok={working}&user={userkey}&text={feedback}")]
        void Feedback(string productName, string productVersion, string working, string userKey, string feedback);*/

        /*[OperationContract] //(IsOneWay = true)
        [WebGet(UriTemplate = "Feedback?name={productName}&ver={productVersion}&man={manufacturer}&ok={working}&user={userkey}&text={feedback}")]
        void Feedback(string productName, string productVersion, string manufacturer, string working, string userKey, string feedback);*/

        [OperationContract] //(IsOneWay = true)
        [WebGet(UriTemplate = "Feedback?name={productName}&ver={productVersion}&man={manufacturer}&arch={architecture}&ok={working}&user={userkey}&text={feedback}")]
        void Feedback(string productName, string productVersion, string manufacturer, string architecture, string working, string userKey, string feedback);

        [OperationContract]
        [WebInvoke(UriTemplate = "CheckForUpdateXml", Method = "POST",
           RequestFormat = WebMessageFormat.Xml,
           ResponseFormat = WebMessageFormat.Xml,
            BodyStyle = WebMessageBodyStyle.Bare)]

        List<AddSoftware> CheckForUpdate(List<AddSoftware> lSoftware);

        [OperationContract]
        [WebInvoke(UriTemplate = "CheckForUpdate", Method = "POST",
            RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json)]
        List<AddSoftware> CheckForUpdateJ(List<AddSoftware> lSoftware);


        [OperationContract(IsOneWay = true)]
        [WebGet(UriTemplate = "TrackDownloads/{contentId}")]
        void TrackDownloads(string contentId);

        [OperationContract(IsOneWay = true)]
        [WebGet(UriTemplate = "TrackDownloadsNew?SWId={SWId}&arch={Architecture}")]
        void TrackDownloads2(string SWId, string Architecture);

        [OperationContract(IsOneWay = true)]
        [WebGet(UriTemplate = "PushMessage?msg={Message}&body={Body}")]
        void PushBullet(string Message, string Body);

        [OperationContract]
        [WebGet(UriTemplate = "GetIcon?id={iconid}")]
        Stream GetIcon(string iconid);

        [OperationContract]
        [WebGet(UriTemplate = "SyncSW?id={s1}")]
        void SyncSW(string s1);

        [OperationContract]
        [WebGet(UriTemplate = "UpdateCatalog")]
        string UpdateCatalog();

        [OperationContract]
        [WebGet(UriTemplate = "ApproveSW?SWId={SWId}&arch={Architecture}")]
        bool ApproveSW(long SWId, string Architecture);

        [OperationContract]
        [WebGet(UriTemplate = "AddIPFS?Id={contentID}&file={fileName}&hash={iPFS}&size={size}&upd={update}")]
        void AddIPFS(string contentID, string fileName, string iPFS, long size, bool update);

        [OperationContract]
        [WebGet(UriTemplate = "GetIPFS?Id={contentID}&file={fileName}")]
        string GetIPFS(string contentID, string fileName);
    }

    [DataContract]
    public class GetSoftware
    {
        [DataMember]
        public string ProductName { get; set; }

        [DataMember]
        public string Manufacturer { get; set; }

        [DataMember]
        public string Description { get; set; }

        [DataMember]
        public string Shortname { get; set; }

        [DataMember]
        public string ProductURL { get; set; }

        [DataMember]
        public string ProductVersion { get; set; }

        [DataMember]
        public byte[] Image { get; set; }

        [DataMember]
        public Int32? Quality { get; set; }

        [DataMember]
        public Int32? Downloads { get; set; }

        //28.12.14
        [DataMember]
        public List<string> Categories { get; set; }

        [DataMember]
        public long IconId { get; set; }

        //8.9.2017
        [DataMember]
        public bool isLatest { get; set; }
    }

    [DataContract]
    public class AddSoftware
    {
        [DataMember]
        public string ProductName { get; set; }
        [DataMember]
        public string Manufacturer { get; set; }
        [DataMember]
        public string Description { get; set; }
        [DataMember]
        public string Shortname { get; set; }
        [DataMember]
        public string ProductURL { get; set; }
        [DataMember]
        public string ProductVersion { get; set; }

        [JsonConverter(typeof(ByteArrayConverter))]
        [DataMember]
        public byte[] Image { get; set; }

        [DataMember]
        public string MSIProductID { get; set; }
        [DataMember]
        public string Architecture { get; set; }
        [DataMember]
        public string PSUninstall { get; set; }
        [DataMember]
        public string PSDetection { get; set; }
        [DataMember]
        public string PSInstall { get; set; }
        [DataMember]
        public string PSPreReq { get; set; }
        [DataMember]
        public string PSPreInstall { get; set; }
        [DataMember]
        public string PSPostInstall { get; set; }
        [DataMember]
        public string ContentID { get; set; }
        [DataMember]
        public List<contentFiles> Files { get; set; }

        //1.12.14
        [DataMember]
        public string Author { get; set; }

        //28.12.14
        [DataMember]
        public string Category { get; set; }

        //3.4.15
        [DataMember]
        public string[] PreRequisites { get; set; }

        //2.9.17
        [DataMember]
        public long IconId { get; set; }

        [DataMember]
        public long SWId { get { return IconId; } set { IconId = value; } }
    }

    [DataContract]
    public class contentFiles
    {
        [DataMember]
        public string URL { get; set; }
        [DataMember]
        public string FileName { get; set; }
        [DataMember]
        public string FileHash { get; set; }
        [DataMember]
        public string HashType { get; set; }
    }

    /// <summary>
    /// Needed because the  JavaScriptSerializer does not understand base64 byte arrays (Image).. so we have to convert them...
    /// </summary>
    public class ByteArrayConverter : JsonConverter
    {
        public override void WriteJson(
            JsonWriter writer,
            object value,
            JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            byte[] data = (byte[])value;

            // Compose an array.
            writer.WriteStartArray();

            for (var i = 0; i < data.Length; i++)
            {
                writer.WriteValue(data[i]);
            }

            writer.WriteEndArray();
        }

        public override object ReadJson(
            JsonReader reader,
            Type objectType,
            object existingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.StartArray)
            {
                var byteList = new List<byte>();

                while (reader.Read())
                {
                    switch (reader.TokenType)
                    {
                        case JsonToken.Integer:
                            byteList.Add(Convert.ToByte(reader.Value));
                            break;
                        case JsonToken.EndArray:
                            return byteList.ToArray();
                        case JsonToken.Comment:
                            // skip
                            break;
                        default:
                            throw new Exception(
                            string.Format(
                                "Unexpected token when reading bytes: {0}",
                                reader.TokenType));
                    }
                }

                throw new Exception("Unexpected end when reading bytes.");
            }
            else
            {
                throw new Exception(
                    string.Format(
                        "Unexpected token parsing binary. "
                        + "Expected StartArray, got {0}.",
                        reader.TokenType));
            }
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(byte[]);
        }
    }
}
