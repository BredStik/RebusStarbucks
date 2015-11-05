using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rebus.Config;
using Rebus.Sagas;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rebus.SagaStorage
{
    public static class JsonDataStorageExtension
    {
        public static void StoreInJsonFile(this StandardConfigurer<ISagaStorage> configurer,
            string folderLocation)
        {
            configurer.Register(c =>
            {
                var sagaStorage = new JsonSagaStorage(folderLocation);
                return sagaStorage;
            });
        }
    }

    public class JsonSagaStorage : ISagaStorage
    {
        private static object _lock = new object();
        private readonly string _folderLocation;
        private const string INDEX_FILE_NAME = "index.json";

        private readonly static ConcurrentDictionary<string, string> _sagaStorages = new ConcurrentDictionary<string, string>();

        public JsonSagaStorage(string folderLocation)
        {
            _folderLocation = folderLocation;
        }

        public Task Delete(ISagaData sagaData)
        {
            ActionWithJson(j => {
                var storedSagaData = j[sagaData.GetType().Name] as JArray;

                if(storedSagaData == null)
                {
                    return;
                }

                var singleSagaData = storedSagaData.SingleOrDefault(x => x["Id"].Value<string>() == sagaData.Id.ToString());

                if (singleSagaData != null)
                {
                    singleSagaData.Remove();
                }
            }, Path.Combine(_folderLocation, string.Concat(sagaData.GetType().Name, ".json")), true);

            return Task.FromResult(0);
        }       

        public Task<ISagaData> Find(Type sagaDataType, string propertyName, object propertyValue)
        {
            ISagaData data = null;

            ActionWithJson(j => {
                var storedSagaData = j[sagaDataType.Name] as JArray;

                if (storedSagaData == null)
                {
                    return;
                }

                var found = storedSagaData.SingleOrDefault(x => x[propertyName].Value<string>() == propertyValue.ToString());

                data = found != null ? found.ToObject(sagaDataType) as ISagaData : null;
            }, Path.Combine(_folderLocation, string.Concat(sagaDataType.Name, ".json")));

            return Task.FromResult(data);
        }

        public Task Insert(ISagaData sagaData, IEnumerable<ISagaCorrelationProperty> correlationProperties)
        {
            //todo: keep correlation properties in separate file to use in Find (as an index) correlationId => sagaId

            ActionWithJson(j => {
                var storedSagaData = j[sagaData.GetType().Name] as JArray;

                if (storedSagaData == null)
                {
                    j[sagaData.GetType().Name] = new JArray(JObject.FromObject(sagaData));
                }
                else
                {
                    storedSagaData.Add(JObject.FromObject(sagaData));
                }
            }, Path.Combine(_folderLocation, string.Concat(sagaData.GetType().Name, ".json")), true);

            return Task.FromResult(0);
        }

        public Task Update(ISagaData sagaData, IEnumerable<ISagaCorrelationProperty> correlationProperties)
        {
            ActionWithJson(j => {
                var storedSagaData = j[sagaData.GetType().Name] as JArray;

                if (storedSagaData == null)
                {
                    throw new Exception("could not find any saga for the type");
                }
                else
                {
                    var existingData = storedSagaData.SingleOrDefault(x => x["Id"].Value<string>() == sagaData.Id.ToString());

                    if(existingData == null)
                    {
                        throw new Exception("could not find any saga for the id");
                    }

                    existingData.Replace(JObject.FromObject(sagaData));
                }
            }, Path.Combine(_folderLocation, string.Concat(sagaData.GetType().Name, ".json")), true);

            return Task.FromResult(0);
        }

        private void ActionWithJson(Action<JObject> action, string fileName, bool saveChanges = false)
        {
            lock (_lock)
            {
                JObject jObject;

                if (_sagaStorages.ContainsKey(fileName))
                {
                    jObject = JObject.Parse(_sagaStorages[fileName]);
                }
                else
                {
                    using (var file = File.Open(fileName, FileMode.OpenOrCreate, FileAccess.Read))
                    {
                        var bytes = ReadFully(file, file.Length);

                        var json = Encoding.UTF8.GetString(bytes);

                        if (!string.IsNullOrEmpty(json))
                        {
                            jObject = JObject.Parse(json);
                        }
                        else
                        {
                            jObject = new JObject();
                        }
                    }

                    _sagaStorages.AddOrUpdate(fileName, jObject.ToString(Formatting.None), (key, existing) => jObject.ToString(Formatting.None));
                }

                action(jObject);

                if (saveChanges)
                {
                    using (var file = File.Open(fileName, FileMode.Truncate, FileAccess.Write))
                    using (StreamWriter sw = new StreamWriter(file))
                    using (JsonWriter jw = new JsonTextWriter(sw))
                    {
                        JsonSerializer serializer = new JsonSerializer();
                        serializer.Serialize(jw, jObject);

                        //var bytes = Encoding.UTF8.GetBytes(jObject.ToString());
                        //file.Write(bytes, 0, bytes.Length);
                        //file.Flush();
                    }

                    _sagaStorages.AddOrUpdate(fileName, jObject.ToString(Formatting.None), (key, existing) => jObject.ToString(Formatting.None));
                }
            }
        }

        private byte[] ReadFully(Stream stream, long initialLength)
        {
            stream.Position = 0;

            try
            {
                // If we've been passed an unhelpful initial length, just
                // use 32K.
                if (initialLength < 1)
                {
                    initialLength = 32768;
                }

                var buffer = new byte[initialLength];
                int read = 0;

                int chunk;
                while ((chunk = stream.Read(buffer, read, buffer.Length - read)) > 0)
                {
                    read += chunk;

                    // If we've reached the end of our buffer, check to see if there's
                    // any more information
                    if (read == buffer.Length)
                    {
                        int nextByte = stream.ReadByte();

                        // End of stream? If so, we're done
                        if (nextByte == -1)
                        {
                            return buffer;
                        }

                        // Nope. Resize the buffer, put in the byte we've just
                        // read, and continue
                        var newBuffer = new byte[buffer.Length * 2];
                        Array.Copy(buffer, newBuffer, buffer.Length);
                        newBuffer[read] = (byte)nextByte;
                        buffer = newBuffer;
                        read++;
                    }
                }
                // Buffer is now too big. Shrink it.
                var ret = new byte[read];
                Array.Copy(buffer, ret, read);

                return ret;
            }
            finally
            {
                stream.Position = 0;
            }
        }
    }
}
