using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

public class CacheEntry<T> where T : unmanaged
{
    public readonly T[] data;
    public readonly T min;
    public readonly T max;

    public CacheEntry(T[] data, T min, T max)
    {
        this.data = data;
        this.min = min;
        this.max = max;
    }
};

/// <summary>
///     LRU-based CPU memory brick cache.
/// </summary>
/// 
/// <typeparam name="T">
///     color depth of the underlying brick data
/// </typeparam>
public class MemoryCache<T> where T : unmanaged
{

    private class InternalCacheEntry<P> where P : unmanaged
    {
        public readonly CacheEntry<P> entry;
        public long timestamp;

        public InternalCacheEntry(CacheEntry<P> entry)
        {
            this.entry = entry;
            this.timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        }
    }

    private class HomogeneousBrick<K> where K : unmanaged
    {
        public HomogeneousBrick(K min, K max)
        {
            this.min = min;
            this.max = max;
        }
        public K min;
        public K max;
    }


    // had to do this because C# sucks ...
    private class BrickCacheComparer<Y> : IComparer<UInt32> where Y : unmanaged
    {
        private readonly ConcurrentDictionary<UInt32, InternalCacheEntry<Y>> m_cache;
        public BrickCacheComparer(ConcurrentDictionary<UInt32, InternalCacheEntry<Y>> brick_cache)
        {
            m_cache = brick_cache;
        }
        int IComparer<UInt32>.Compare(UInt32 x, UInt32 y) =>
            m_cache[x].timestamp.CompareTo(m_cache[y].timestamp);
    }


    private readonly ConcurrentDictionary<UInt32, InternalCacheEntry<T>> m_cache;
    private readonly ConcurrentDictionary<UInt32, HomogeneousBrick<T>> m_homogeneious_cache;

    private object m_lock = new();

    private readonly int m_max_capacity;

    private readonly UInt32[] m_sorted_keys;
    private readonly IComparer<UInt32> m_comparer;


    public MemoryCache(long memory_size_limit_mb, long brick_size_bytes)
    {
        m_max_capacity = Mathf.CeilToInt(1024 * memory_size_limit_mb / (brick_size_bytes / 1024.0f));
        m_cache = new(Environment.ProcessorCount, m_max_capacity);
        m_homogeneious_cache = new(Environment.ProcessorCount, m_max_capacity);
        m_sorted_keys = new UInt32[m_max_capacity];
        m_comparer = (IComparer<UInt32>)new BrickCacheComparer<T>(m_cache);
        Debug.Log($"max nbr CPU brick cache entries: {m_max_capacity}");
    }


    /// <summary>
    ///     Sets the provided brick cache entry into the brick cache.  In case this is a non-homogeneous brick
    ///     and the maximal capacity is reached, a brick is evicted using LRU CRP.
    /// </summary>
    /// 
    /// <param name="id">
    /// </param>
    /// 
    /// <param name="entry">
    /// </param>
    public void Set(UInt32 id, CacheEntry<T> entry)
    {
        if (entry.min.Equals(entry.max))
        {
            m_homogeneious_cache.TryAdd(id, new HomogeneousBrick<T>(entry.min, entry.max));
            return;
        }

        lock (m_lock)
        {
            // CRP in case no more entries can be added
            if (m_cache.Count >= m_max_capacity)
            {
                m_cache.Keys.CopyTo(m_sorted_keys, 0);
                // good luck supplying a lambda to this pos function
                Array.Sort(m_sorted_keys, 0, m_cache.Count, m_comparer);
                UInt32 key_to_evict = m_sorted_keys[0];
                m_cache.TryRemove(key_to_evict, out _);

            }
            m_cache.TryAdd(id, new(entry));
        }
    }


    /// <summary>
    ///     Tries to get the brick cache entry with the provided id.
    /// </summary>
    ///
    /// <param name="id">
    ///     The unique ID of the brick to retrieve.
    /// </param>
    /// 
    /// <returns>
    ///     The chache entry if it exists, null otherwise.
    /// </returns>
    public CacheEntry<T> Get(UInt32 id)
    {
        if (m_homogeneious_cache.TryGetValue(id, out HomogeneousBrick<T> h))
        {
            return new(null, h.min, h.max);
        }
        // update entry's timestamp
        if (m_cache.TryGetValue(id, out InternalCacheEntry<T> e))
        {
            lock (m_lock)
            {
                e.timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            }
            return e.entry;
        }
        return null;
    }


    public bool Contains(UInt32 id) => m_cache.ContainsKey(id);
}