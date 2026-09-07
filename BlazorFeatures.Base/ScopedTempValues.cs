using System.Collections;

namespace BlazorFeatures.Base
{
    /// <summary>
    /// Keeps the temporary values received by the current invocation separate
    /// from values written for the next direct invocation.
    /// </summary>
    internal sealed class ScopedTempValues : IDictionary<string, object>
    {
        private readonly Dictionary<string, object> _receivedValues;
        private readonly Dictionary<string, object> _outgoingValues = [];
        private readonly HashSet<string> _hiddenReceivedKeys = [];
        private readonly Lock _sync = new();

        public ScopedTempValues()
            : this(new Dictionary<string, object>())
        {
        }

        public ScopedTempValues(IReadOnlyDictionary<string, object> receivedValues)
        {
            _receivedValues = new Dictionary<string, object>(receivedValues);
        }

        public object this[string key]
        {
            get
            {
                lock (_sync)
                {
                    if (_outgoingValues.TryGetValue(key, out var outgoingValue))
                        return outgoingValue;
                    if (!_hiddenReceivedKeys.Contains(key)
                        && _receivedValues.TryGetValue(key, out var receivedValue))
                        return receivedValue;
                    throw new KeyNotFoundException();
                }
            }
            set
            {
                ArgumentNullException.ThrowIfNull(key);
                lock (_sync)
                {
                    _hiddenReceivedKeys.Remove(key);
                    _outgoingValues[key] = value;
                }
            }
        }

        public ICollection<string> Keys => Snapshot().Keys;

        public ICollection<object> Values => Snapshot().Values;

        public int Count => Snapshot().Count;

        public bool IsReadOnly => false;

        public void Add(string key, object value)
        {
            ArgumentNullException.ThrowIfNull(key);
            lock (_sync)
            {
                if (ContainsKeyCore(key))
                    throw new ArgumentException("An item with the same key has already been added.", nameof(key));
                _hiddenReceivedKeys.Remove(key);
                _outgoingValues.Add(key, value);
            }
        }

        public bool ContainsKey(string key)
        {
            ArgumentNullException.ThrowIfNull(key);
            lock (_sync)
                return ContainsKeyCore(key);
        }

        public bool Remove(string key)
        {
            ArgumentNullException.ThrowIfNull(key);
            lock (_sync)
            {
                var removed = _outgoingValues.Remove(key);
                if (_receivedValues.ContainsKey(key) && !_hiddenReceivedKeys.Contains(key))
                {
                    _hiddenReceivedKeys.Add(key);
                    removed = true;
                }
                return removed;
            }
        }

        public bool TryGetValue(string key, out object value)
        {
            ArgumentNullException.ThrowIfNull(key);
            lock (_sync)
            {
                if (_outgoingValues.TryGetValue(key, out value!))
                    return true;
                if (!_hiddenReceivedKeys.Contains(key)
                    && _receivedValues.TryGetValue(key, out value!))
                    return true;
                value = default!;
                return false;
            }
        }

        public void Add(KeyValuePair<string, object> item) => Add(item.Key, item.Value);

        public void Clear()
        {
            lock (_sync)
            {
                _outgoingValues.Clear();
                _hiddenReceivedKeys.UnionWith(_receivedValues.Keys);
            }
        }

        public bool Contains(KeyValuePair<string, object> item) =>
            TryGetValue(item.Key, out var value)
            && EqualityComparer<object>.Default.Equals(value, item.Value);

        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex) =>
            ((ICollection<KeyValuePair<string, object>>)Snapshot()).CopyTo(array, arrayIndex);

        public bool Remove(KeyValuePair<string, object> item) =>
            Contains(item) && Remove(item.Key);

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() =>
            Snapshot().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        internal IReadOnlyDictionary<string, object> TakeOutgoingValues()
        {
            lock (_sync)
            {
                var result = new Dictionary<string, object>(_outgoingValues);
                _outgoingValues.Clear();
                return result;
            }
        }

        private bool ContainsKeyCore(string key) =>
            _outgoingValues.ContainsKey(key)
            || (!_hiddenReceivedKeys.Contains(key) && _receivedValues.ContainsKey(key));

        private Dictionary<string, object> Snapshot()
        {
            lock (_sync)
            {
                var result = new Dictionary<string, object>();
                foreach (var pair in _receivedValues)
                {
                    if (!_hiddenReceivedKeys.Contains(pair.Key))
                        result[pair.Key] = pair.Value;
                }
                foreach (var pair in _outgoingValues)
                    result[pair.Key] = pair.Value;
                return result;
            }
        }
    }
}
