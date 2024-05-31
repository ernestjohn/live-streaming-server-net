﻿using LiveStreamingServerNet.Networking.Configurations;
using LiveStreamingServerNet.Networking.Contracts;
using LiveStreamingServerNet.Utilities;
using LiveStreamingServerNet.Utilities.Contracts;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace LiveStreamingServerNet.Networking
{
    public sealed class NetBufferPool : INetBufferPool
    {
        private readonly Pool<PoolableNetBuffer> _pool;
        private readonly IBufferPool? _bufferPool;
        private readonly int _netBufferCapacity;
        private readonly int _maxPoolSize;
        private readonly ConcurrentBag<PoolableNetBuffer> _poolableNetBuffers = new();

        public NetBufferPool(IOptions<NetBufferPoolConfiguration> config, IBufferPool? bufferPool = null)
        {
            _pool = new Pool<PoolableNetBuffer>(CreatePoolableNetBuffer);
            _bufferPool = bufferPool;
            _netBufferCapacity = config.Value.NetBufferCapacity;
            _maxPoolSize = config.Value.MaxPoolSize;
        }

        private PoolableNetBuffer CreatePoolableNetBuffer()
        {
            var netBuffer = new PoolableNetBuffer(this, _bufferPool, _netBufferCapacity);
            _poolableNetBuffers.Add(netBuffer);
            return netBuffer;
        }

        public INetBuffer Obtain()
        {
            if (_maxPoolSize >= 0 && _poolableNetBuffers.Count >= _maxPoolSize && _pool.GetPooledCount() == 0)
            {
                return new NetBuffer(_bufferPool, _netBufferCapacity);
            }
            else
            {
                var netBuffer = _pool.Obtain();
                netBuffer.OnObtained();
                return netBuffer;
            }
        }

        internal void RecycleNetBuffer(PoolableNetBuffer netBuffer)
        {
            _pool.Recycle(netBuffer);
        }

        public void Dispose()
        {
            foreach (var netBuffer in _poolableNetBuffers)
                netBuffer.Destroy();

            GC.SuppressFinalize(this);
        }
    }

    internal sealed class PoolableNetBuffer : NetBuffer
    {
        private readonly NetBufferPool _pool;
        private int _inPool;

        public PoolableNetBuffer(NetBufferPool pool, IBufferPool? bufferPool, int initialCapacity) : base(bufferPool, initialCapacity)
        {
            _pool = pool;
            _inPool = 1;
        }

        public void OnObtained()
        {
            _inPool = 0;
        }

        public override void Dispose()
        {
            if (Interlocked.CompareExchange(ref _inPool, 1, 0) == 1)
            {
                return;
            }

            Reset();
            _pool.RecycleNetBuffer(this);
        }

        public void Destroy()
        {
            base.Dispose();
            GC.SuppressFinalize(this);
        }

        ~PoolableNetBuffer()
        {
            Destroy();
        }
    }
}
