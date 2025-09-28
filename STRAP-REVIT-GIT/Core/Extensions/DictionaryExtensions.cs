using System.Collections.Generic;

namespace StrapRevit.Core.Extensions
{
    /// <summary>
    /// Extensões para Dictionary
    /// </summary>
    public static class DictionaryExtensions
    {
        /// <summary>
        /// Obtém valor ou retorna padrão se chave não existir
        /// </summary>
        public static TValue GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue = default(TValue))
        {
            return dictionary.TryGetValue(key, out TValue value) ? value : defaultValue;
        }
    }
}

