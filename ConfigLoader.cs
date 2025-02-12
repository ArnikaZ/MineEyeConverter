using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace ConsoleApp9
{
    public static class ConfigLoader
    {
        public static Configuration LoadConfiguration(string filePath)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(Configuration));
            using (FileStream fs = new FileStream(filePath, FileMode.Open))
            {
                return (Configuration)serializer.Deserialize(fs);
            }
        }
    }
}
