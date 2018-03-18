using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;

namespace RaspberryPi.Camera.Serialization
{
    public static class JsonSerializer
    {
        public static T Deserialize<T>(string text)
        {
            object result;
            var serializer = new DataContractJsonSerializer(typeof(T));

            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(text)))
            {
                result = serializer.ReadObject(ms);
            }
            
            return (T)result;
        }

        public static T Deserialize<T>(Stream stream)
        {
            object result;
            var serializer = new DataContractJsonSerializer(typeof(T));

            result = serializer.ReadObject(stream);

            return (T)result;
        }

        public static string Serialize(object obj)
        {
            var serializer = new DataContractJsonSerializer(obj.GetType());
            
            using (var ms = new MemoryStream())
            {
                serializer.WriteObject(ms, obj);

                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        public static void Serialize(object obj, Stream destination)
        {
            var serializer = new DataContractJsonSerializer(obj.GetType());

            serializer.WriteObject(destination, obj);
        }
    }
}
