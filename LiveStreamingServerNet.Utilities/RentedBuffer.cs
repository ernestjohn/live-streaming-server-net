﻿using LiveStreamingServerNet.Utilities.Contracts;
using System.Buffers;

namespace LiveStreamingServerNet.Utilities
{
    public class RentedBuffer : IRentedBuffer
    {
        public byte[] Bytes { get; private set; }
        public int Claimed => _claimed;
        private int _claimed;

        public RentedBuffer(int size, int claim = 1)
        {
            Bytes = ArrayPool<byte>.Shared.Rent(size);
            _claimed = claim;
        }

        public void Claim()
        {
            Interlocked.Increment(ref _claimed);
        }

        public void Unclaim()
        {
            if (Interlocked.Decrement(ref _claimed) == 0)
            {
                ArrayPool<byte>.Shared.Return(Bytes);
                Bytes = null!;
            }
        }
    }
}