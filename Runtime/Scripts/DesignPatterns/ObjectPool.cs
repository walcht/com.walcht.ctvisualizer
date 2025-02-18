using System;
using System.Runtime.InteropServices;

public class UnmanagedObjectPool<T> where T : struct {

    private struct PoolObjectTracker {
        public bool is_free;
        /// <summary>
        ///     Means that unmanaged data should be freed - this is important
        ///     in case of substructures to avoid memory leaks
        /// </summary>
        public bool is_data_valid;
    }

    private readonly int m_capacity;
    private readonly IntPtr[] m_pool;
    private readonly PoolObjectTracker[] m_pool_tracker;
    private int m_nbr_free_slots;

    public int GetNbrFreeSlots() {
        return m_nbr_free_slots;
    }

    public UnmanagedObjectPool(int capacity) {
        m_capacity = capacity;
        m_pool = new IntPtr[m_capacity];
        m_pool_tracker = new PoolObjectTracker[m_capacity];
        m_nbr_free_slots = m_capacity;
        for (int i = 0; i < m_capacity; ++i) {
            m_pool[i] = Marshal.AllocHGlobal(Marshal.SizeOf<T>());
            // be on the safe side and initialize/re-initialize the tracker structs
            m_pool_tracker[i] = new PoolObjectTracker();
            m_pool_tracker[i].is_free = true;
            m_pool_tracker[i].is_data_valid = false;
        }
    }

    public int Acquire(T val, out IntPtr ptr) {
        for (int i = 0; i < m_capacity; ++i) {
            if (m_pool_tracker[i].is_free) {
                ptr = m_pool[i];
                Marshal.StructureToPtr(val, ptr, fDeleteOld: m_pool_tracker[i].is_data_valid);
                m_pool_tracker[i].is_free = false;
                m_pool_tracker[i].is_data_valid = true;
                --m_nbr_free_slots;
                return i;
            }
        }
        throw new Exception("object pool is full. Use ResizableObjectPool instead");
    }

    public void Release(int pool_object_idx) {
        if (m_pool_tracker[pool_object_idx].is_free) return;
        m_pool_tracker[pool_object_idx].is_free = true;
        ++m_nbr_free_slots;
    }

    public void ReleaseAll() {
        for (int i = 0; i < m_capacity; ++i) {
            m_pool_tracker[i].is_free = true;
        }
        m_nbr_free_slots = m_capacity;
    }

    ~UnmanagedObjectPool() {
        for (int i = 0; i < m_capacity; ++i) {
            Marshal.FreeHGlobal(m_pool[i]);
        }
    }
}
