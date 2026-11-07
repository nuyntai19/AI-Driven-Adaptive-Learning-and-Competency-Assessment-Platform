using System;
using System.Security.Cryptography;

namespace EduTwin.BLL.AdaptivePractice;

internal static class AdaptivePracticeIdGenerator
{
    public static ulong NewId()
    {
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        ulong value;

        do
        {
            RandomNumberGenerator.Fill(bytes);
            value = BitConverter.ToUInt64(bytes);
        }
        while (value == 0);

        return value;
    }
}
