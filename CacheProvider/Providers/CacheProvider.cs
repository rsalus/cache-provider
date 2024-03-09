﻿using CacheProvider.Caches;
using CacheProvider.Providers.Interfaces;
using MemCache = Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Microsoft.Extensions.Caching.Memory;

namespace CacheProvider.Providers
{
    /// <summary>
    /// CacheProvider is a generic class that implements the <see cref="ICacheProvider{T}"/> interface.
    /// </summary>
    /// <remarks>
    /// This class makes use of two types of caches: <see cref="MemoryCache"/> and <see cref="DistributedCache"/>.
    /// It uses the <see cref="IRealProvider{T}>"/> interface to retrieve entries from the real provider.
    /// </remarks>
    /// <typeparam name="T">The type of object to cache.</typeparam>
    public class CacheProvider<T> : ICacheProvider<T> where T : class
    {
        private readonly IRealProvider<T> _realProvider;
        private readonly CacheSettings _settings;
        private readonly ILogger _logger;
        private readonly DistributedCache _cache;

        /// <summary>
        /// Primary constructor for the CacheProvider class.
        /// </summary>
        /// <remarks>
        /// Takes a real provider, cache type, and cache settings as parameters.
        /// </remarks>
        /// <param name="provider"></param>
        /// <param name="type"></param>
        /// <param name="settings"></param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public CacheProvider(IConnectionMultiplexer connection, IRealProvider<T> provider, CacheSettings settings, ILogger logger)
        {
            // Null checks
            ArgumentNullException.ThrowIfNull(connection);
            ArgumentNullException.ThrowIfNull(provider);
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(logger);

            // Initializations
            _realProvider = provider;
            _settings = settings;
            _logger = logger;
            _cache = new DistributedCache(connection, new MemCache.MemoryCache(new MemoryCacheOptions()), settings, logger);
        }

        /// <summary>
        /// Gets the cache instance.
        /// </summary>
        public DistributedCache Cache => _cache;

        /// <summary>
        /// Asynchronously checks the cache for an entry with a specified key.
        /// </summary>
        /// <remarks>
        /// If the entry is found in the cache, it is returned. If not, the entry is retrieved from the RealProvider and then cached before being returned.
        /// </remarks>
        /// <param name="">The  to cache.</param>
        /// <param name="key">The key to use for caching the .</param>
        /// <returns>The cached .</returns>
        /// <exception cref="ArgumentNullException">Thrown when the entry is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the key is null, an empty string, or contains only white-space characters.</exception>
        /// <exception cref="NullReferenceException">Thrown when the entry is not successfully retrieved from the RealProvider.</exception>
        public async Task<T?> GetFromCacheAsync(T data, string key, GetFlags? flag = null)
        {
            try
            {
                // Null Checks
                ArgumentNullException.ThrowIfNull(data);
                ArgumentException.ThrowIfNullOrWhiteSpace(key);

                // Try to get entry from the cache
                var cached = await _cache.GetAsync<T>(key);
                if (cached is not null)
                {
                    _logger.LogInformation("Cached entry with key {key} found in cache.", key);
                    return cached;
                }
                else if (GetFlags.ReturnNullIfNotFoundInCache == flag)
                {
                    _logger.LogInformation("Cached entry with key {key} not found in cache.", key);
                    return null;
                }

                // If not found, get the entry from the real provider
                _logger.LogInformation("Cached entry with key {key} not found in cache. Getting entry from real provider.", key);
                cached = await _realProvider.GetAsync(data);

                if (cached is null)
                {
                    _logger.LogError("Entry with key {key} not received from real provider.", key);
                    throw new NullReferenceException(string.Format("Entry with key {0} was not successfully retrieved.", key));
                }

                // Set the entry in the cache
                if (GetFlags.DoNotSetCacheEntry != flag && await _cache.SetAsync(key, cached))
                {
                    _logger.LogInformation("Entry with key {key} received from real provider and set in cache.", key);
                }
                else
                {
                    _logger.LogError("Failed to set entry with key {key} in cache.", key);
                }

                return cached;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while checking the cache.");
                throw ex.GetBaseException();
            }
        }

        public async Task<bool> SetInCacheAsync(string key, T data)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(data);
                ArgumentException.ThrowIfNullOrWhiteSpace(key);

                bool result = await _cache.SetAsync(key, data);
                if (result)
                {
                    _logger.LogInformation("Entry with key {key} set in cache.", key);
                }
                else
                {
                    _logger.LogError("Failed to set entry with key {key} in cache.", key);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while setting the cache.");
                throw ex.GetBaseException();
            }
        }

        public async Task<bool> RemoveFromCacheAsync(string key)
        {
            try
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(key);

                var result = await _cache.RemoveAsync(key);
                if (result)
                {
                    _logger.LogInformation("Entry with key {key} removed from cache.", key);
                }
                else
                {
                    _logger.LogError("Failed to remove entry with key {key} from cache.", key);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while removing from the cache.");
                throw ex.GetBaseException();
            }
        }

        public async Task<IDictionary<string, T>> GetBatchFromCacheAsync(IDictionary<string, T> data, IEnumerable<string> keys, GetFlags? flags = null, CancellationToken? cancellationToken = null)
        {
            try
            {
                // Null Checks
                ArgumentNullException.ThrowIfNull(data);
                foreach (var key in keys)
                {
                    ArgumentException.ThrowIfNullOrEmpty(key);
                }

                // Try to get entries from the cache
                var cached = await _cache.GetBatchAsync<T>(keys, cancellationToken);
                if (cached is not null && cached.Count > 0)
                {
                    _logger.LogInformation("Cached entries with keys {keys} found in cache.", string.Join(", ", keys));
                    return cached;
                }

                // If not found, get the entries from the real provider
                _logger.LogInformation("Cached entries with keys {keys} not found in cache. Getting entries from real provider.", string.Join(", ", keys));
                cached = await _realProvider.GetBatchAsync(keys, cancellationToken);

                if (cached is null || cached.Count == 0)
                {
                    _logger.LogError("Entries with keys {keys} not received from real provider.", string.Join(", ", keys));
                    throw new NullReferenceException(string.Format("Entries with keys {0} were not successfully retrieved.", string.Join(", ", keys)));
                }

                // Set the entries in the cache
                TimeSpan absoluteExpiration = TimeSpan.FromSeconds(_settings.AbsoluteExpiration);
                if (GetFlags.DoNotSetCacheEntry != flags && await _cache.SetBatchAsync(cached, absoluteExpiration, cancellationToken))
                {
                    _logger.LogInformation("Entries with keys {keys} received from real provider and set in cache.", string.Join(", ", keys));
                }
                else
                {
                    _logger.LogError("Failed to set entries with keys {keys} in cache.", string.Join(", ", keys));
                }

                return cached;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while checking the cache.");
                throw ex.GetBaseException();
            }
        }

        public async Task<bool> SetBatchInCacheAsync(Dictionary<string, T> data, CancellationToken? cancellationToken = null)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(data);
                foreach (var key in data.Keys)
                {
                    ArgumentException.ThrowIfNullOrEmpty(key);
                }

                TimeSpan absoluteExpiration = TimeSpan.FromSeconds(_settings.AbsoluteExpiration);
                var result = await _cache.SetBatchAsync(data, absoluteExpiration, cancellationToken);
                if (result)
                {
                    _logger.LogInformation("Entries with keys {keys} set in cache.", string.Join(", ", data.Keys));
                }
                else
                {
                    _logger.LogError("Failed to set entries with keys {keys} in cache.", string.Join(", ", data.Keys));
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while setting the cache.");
                throw ex.GetBaseException();
            }
        }

        public async Task<bool> RemoveBatchFromCacheAsync(IEnumerable<string> keys, CancellationToken? cancellationToken = null)
        {
            try
            {
                foreach (var key in keys)
                {
                    ArgumentException.ThrowIfNullOrEmpty(key);
                }

                var result = await _cache.RemoveBatchAsync(keys, cancellationToken);
                if (result)
                {
                    _logger.LogInformation("Entries with keys {keys} removed from cache.", string.Join(", ", keys));
                }
                else
                {
                    _logger.LogError("Failed to remove entries with keys {keys} from cache.", string.Join(", ", keys));
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while removing from the cache.");
                throw ex.GetBaseException();
            }
        }
    }
}
