#nullable disable

using System;
using System.Threading;
using System.Threading.Tasks;

namespace BlazorFeatures.Abstractions.Tools
{
    /// <summary>
    /// Classe interna che gestisce un valore condiviso di tipo generico
    /// </summary>
    internal class SharedValue<T>(T value)
    {
        // Valore interno memorizzato come object
        private object Value { get; set; } = value;

        internal Action<T, T> ValueChangedFunc { get; set; }

        // Imposta il valore interno
        public void Set(T value)
        {
            var precValue = (T)Value;
            Value = value;
            ValueChangedFunc?.Invoke(precValue, value);
        }

        // Ottiene il valore convertito nel tipo specificato
        public T Get() => (T)Value;

        internal SharedValue<K> As<K>() => new((K)Value);
    }

    /// <summary>
    /// Classe che fornisce un wrapper thread-safe per un valore generico
    /// utilizzando un lock standard
    /// </summary>
    public class ObjectValue<T>
    {
        // Oggetto utilizzato per il lock thread-safe
        private readonly object ValueLock;

        // Riferimento al valore condiviso
        private SharedValue<T> Value { get; set; }

        public event Action<T, T> ValueChanged;

        // Costruttore privato per l'inizializzazione interna
        private ObjectValue(SharedValue<T> value, object valueLock)
        {
            Value = value;
            Value.ValueChangedFunc = OnValueChanged;
            ValueLock = valueLock;
        }

        // Costruttore pubblico che accetta il valore iniziale
        public ObjectValue(T value) : this(new(value), new()) { }

        private void OnValueChanged(T oldValue, T newValue) => ValueChanged?.Invoke(oldValue, newValue);

        // Converte il valore in un altro tipo mantenendo lo stesso lock
        public ObjectValue<K> As<K>()
        {
            return new ObjectValue<K>(Value.As<K>(), ValueLock);
        }

        // Ottiene il valore in modo thread-safe
        public T GetValue()
        {
            lock (ValueLock)
            {
                return Value.Get();
            }
        }

        // Imposta il valore in modo thread-safe
        public void SetValue(T value)
        {
            lock (ValueLock)
            {
                Value.Set(value);
            }
        }

        public T Queue(Func<T, T> func)
        {
            lock (ValueLock)
            {
                var value = func(Value.Get());
                Value.Set(value);
                return value;
            }
        }

        public void ProcessValue(Action<T> func)
        {
            lock (ValueLock)
            {
                func(Value.Get());
            }
        }
    }

    /// <summary>
    /// Versione asincrona di ObjectValue che utilizza SemaphoreSlim
    /// per operazioni thread-safe asincrone
    /// </summary>
    public class AsyncObjectValue<T>
    {
        // Semaforo per il controllo dell'accesso asincrono
        private readonly SemaphoreSlim ValueLock;

        // Riferimento al valore condiviso
        private SharedValue<T> Value { get; set; }

        public event Action<T, T> ValueChanged;

        // Costruttore privato per l'inizializzazione interna
        private AsyncObjectValue(SharedValue<T> value, SemaphoreSlim valueLock)
        {
            Value = value;
            Value.ValueChangedFunc = OnValueChanged;
            ValueLock = valueLock;
        }

        // Costruttore pubblico che accetta il valore iniziale
        public AsyncObjectValue(T value) : this(new(value), new(1, 1)) { }

        private void OnValueChanged(T oldValue, T newValue) => ValueChanged?.Invoke(oldValue, newValue);

        // Converte il valore in un altro tipo mantenendo lo stesso lock
        public AsyncObjectValue<K> As<K>()
        {
            return new AsyncObjectValue<K>(Value.As<K>(), ValueLock);
        }

        // Esegue una funzione di trasformazione sul valore in modo thread-safe
        public async Task<T> Queue(Func<T, T> func)
        {
            await ValueLock.WaitAsync();
            try
            {
                Value.Set(func(Value.Get()));
            }
            catch (Exception)
            {
                ValueLock.Release();
                throw;
            }
            var value = Value.Get();
            ValueLock.Release();
            return value;
        }

        // Esegue una funzione di trasformazione asincrona sul valore in modo thread-safe
        public async Task<T> Queue(Func<T, Task<T>> func)
        {
            await ValueLock.WaitAsync();
            try
            {
                Value.Set(await func(Value.Get()));
            }
            catch (Exception)
            {
                ValueLock.Release();
                throw;
            }
            var value = Value.Get();
            ValueLock.Release();
            return value;
        }

        // Processa il valore con un'azione sincrona
        public async Task ProcessValue(Action<T> func)
        {
            await ValueLock.WaitAsync();
            try
            {
                func(Value.Get());
                ValueLock.Release();
            }
            catch (Exception)
            {
                ValueLock.Release();
                throw;
            }
        }

        // Processa il valore con un'azione asincrona
        public async Task ProcessValue(Func<T, Task> func)
        {
            await ValueLock.WaitAsync();
            try
            {
                await func(Value.Get());
                ValueLock.Release();
            }
            catch (Exception)
            {
                ValueLock.Release();
                throw;
            }
        }

        // Processa il valore e restituisce un risultato di tipo diverso
        public async Task<K> ProcessValue<K>(Func<T, K> func)
        {
            await ValueLock.WaitAsync();
            try
            {
                var result = func(Value.Get());
                ValueLock.Release();
                return result;
            }
            catch (Exception)
            {
                ValueLock.Release();
                throw;
            }
        }

        // Processa il valore in modo asincrono e restituisce un risultato di tipo diverso
        public async Task<K> ProcessValue<K>(Func<T, Task<K>> func)
        {
            await ValueLock.WaitAsync();
            try
            {
                var result = await func(Value.Get());
                ValueLock.Release();
                return result;
            }
            catch (Exception)
            {
                ValueLock.Release();
                throw;
            }
        }

        // Imposta il valore in modo asincrono
        public async Task SetValue(T value)
        {
            await ValueLock.WaitAsync();
            Value.Set(value);
            ValueLock.Release();
        }

        // Ottiene il valore in modo asincrono
        public async Task<T> GetValue()
        {
            await ValueLock.WaitAsync();
            var value = Value.Get();
            ValueLock.Release();
            return value;
        }
    }
}
