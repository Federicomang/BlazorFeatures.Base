using System.Collections;

namespace BlazorFeatures.Base
{
    internal sealed class FeatureContextValues(
        IDictionary<string, object> permanentValues,
        IDictionary<string, object> tempValues) : IReadOnlyDictionary<string, object>
    {
        public object this[string key]
        {
            get
            {
                if (tempValues.TryGetValue(key, out var tempValue))
                    return tempValue;
                return permanentValues[key];
            }
        }

        public IEnumerable<string> Keys => Snapshot().Keys;

        public IEnumerable<object> Values => Snapshot().Values;

        public int Count => Snapshot().Count;

        public bool ContainsKey(string key) =>
            tempValues.ContainsKey(key) || permanentValues.ContainsKey(key);

        public bool TryGetValue(string key, out object value)
        {
            if (tempValues.TryGetValue(key, out value!))
                return true;
            return permanentValues.TryGetValue(key, out value!);
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() =>
            Snapshot().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private Dictionary<string, object> Snapshot()
        {
            var result = new Dictionary<string, object>(permanentValues);
            foreach (var pair in tempValues)
                result[pair.Key] = pair.Value;
            return result;
        }
    }
}
