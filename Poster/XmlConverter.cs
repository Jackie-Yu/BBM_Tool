using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.IO;

namespace Achievo.Poster
{
    public class XmlConverter
    {
        public static string ToXml(object xmlobj)
        {
            string returnXml = String.Empty;
            XmlSerializer serializer = new XmlSerializer(xmlobj.GetType());
            using (MemoryStream ms = new MemoryStream())
            {
                serializer.Serialize(ms, xmlobj);
                using (StreamReader reader = new StreamReader(ms))
                {
                    ms.Position = 0;
                    returnXml = reader.ReadToEnd();
                }
            }

            return returnXml;
        }

        /// <summary>
        /// xml Deserialize
        /// </summary>
        public static T FromXmlTo<T>(string xml)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(T));
            MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(xml));
            T xmlObject = (T)serializer.Deserialize(ms);
            ms.Close();
            return xmlObject;
        }
    }
}
