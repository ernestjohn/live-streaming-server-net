﻿namespace LiveStreamingServer.Rtmp.Core.Utilities
{
    public static class HandshakeConstants
    {
        public static readonly byte[] FPKey = new byte[] {
            0x47, 0x65, 0x6E, 0x75, 0x69, 0x6E, 0x65, 0x20,
            0x41, 0x64, 0x6F, 0x62, 0x65, 0x20, 0x46, 0x6C,
            0x61, 0x73, 0x68, 0x20, 0x50, 0x6C, 0x61, 0x79,
            0x65, 0x72, 0x20, 0x30, 0x30, 0x31
        };

        public static readonly byte[] FMSKey = new byte[] {
            0x47, 0x65, 0x6e, 0x75, 0x69, 0x6e, 0x65, 0x20,
            0x41, 0x64, 0x6f, 0x62, 0x65, 0x20, 0x46, 0x6c,
            0x61, 0x73, 0x68, 0x20, 0x4d, 0x65, 0x64, 0x69,
            0x61, 0x20, 0x53, 0x65, 0x72, 0x76, 0x65, 0x72,
            0x20, 0x30, 0x30, 0x31,
            0xf0, 0xee, 0xc2, 0x4a, 0x80, 0x68, 0xbe, 0xe8,
            0x2e, 0x00, 0xd0, 0xd1, 0x02, 0x9e, 0x7e, 0x57,
            0x6e, 0xec, 0x5d, 0x2d, 0x29, 0x80, 0x6f, 0xab,
            0x93, 0xb8, 0xe6, 0x36, 0xcf, 0xeb, 0x31, 0xae
        };
    }
}