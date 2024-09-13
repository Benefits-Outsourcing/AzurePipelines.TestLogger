using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace AzurePipelines.TestLogger
{
    public class LocalCache<T>
    {
        private readonly List<T> _Items = new List<T>();
        private readonly string FileName = $"{typeof(T).Name}_cache.json";

        public LocalCache()
        {
            if (File.Exists(FileName))
            {
                _Items = JsonConvert.DeserializeObject<List<T>>(File.ReadAllText(FileName));
            }
        }

        public List<T> Items => _Items;

        public void Add(T item)
        {
            _Items.Add(item);
            WriteCache();
        }

        public void AddRange(IEnumerable<T> items)
        {
            _Items.AddRange(items);
            WriteCache();
        }

        public void WriteCache()
        {
            File.WriteAllText(FileName, JsonConvert.SerializeObject(_Items));
        }

        //public void UpdateRange(IEnumerable<T> items, Func<T, T, bool> matcher)
        //{
        //    foreach (var item in items)
        //    {
        //        var existingItem = _Items.Find(x => matcher(x, item));
        //        if (existingItem != null)
        //        {
        //            _Items.Remove(existingItem);
        //        }
        //        _Items.Add(item);
        //    }
        //    File.WriteAllText(FileName, JsonConvert.SerializeObject(_Items));
        //}
    }
}
