using System;
using System.Text;
using System.Threading;

namespace AotForms
{
    /// <summary>32-bit nick spoof: localPlayer+0x2E4 → len +0x8, UTF-16 +0xC.</summary>
    internal static class SpoofName
    {
        // [BC] once, then per-letter hex (same format as working NATION CHEATS snippet — vivid colors)
        internal static readonly string DefaultDisplayName =
            "[BC][FFFFFF]K[FFFFFF]A[FFFFFF]N[FFFFFF]I[FFFFFF]S[FFFFFF]H[FFFFFF]K " +
            "[FF00FF]C[FF1AFF]H[FF33FF]E[FF4DFF]A[FF66FF]T";

        private const uint NamePtrOffset = 0x2E4;
        private const uint NameLengthOffset = 0x8;
        private const uint NameDataOffset = 0xC;

        private const int ApplyIntervalMs = 350;

        private static volatile bool _running;
        private static long _lastApplyTicks;

        internal static void Start()
        {
            if (_running) return;
            _running = true;
            new Thread(Work) { IsBackground = true, Priority = ThreadPriority.BelowNormal }.Start();
        }

        internal static void ApplyNow()
        {
            if (Core.LocalPlayer == 0)
                return;

            _lastApplyTicks = 0;
            ApplyToPlayer(Core.LocalPlayer);
        }

        internal static void ApplyToPlayer(ulong localPlayer)
        {
            if (!Config.SpoofNameEnabled || localPlayer == 0)
                return;

            long now = Environment.TickCount64;
            if (_lastApplyTicks != 0 && now - _lastApplyTicks < ApplyIntervalMs)
                return;

            _lastApplyTicks = now;
            ApplyExact32BitChain(localPlayer, DefaultDisplayName);
        }

        private static bool ApplyExact32BitChain(ulong localPlayer, string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            if (!InternalMemory.Read<uint>(localPlayer + NamePtrOffset, out uint nameAddr) || nameAddr == 0)
                return false;

            ulong nameObj = nameAddr;
            byte[] utf16 = Encoding.Unicode.GetBytes(name);

            InternalMemory.InvalidateAddress(nameObj + NameLengthOffset);
            InternalMemory.InvalidateAddress(nameObj + NameDataOffset);

            InternalMemory.Write<int>(nameObj + NameLengthOffset, name.Length);
            InternalMemory.WriteBytes(nameObj + NameDataOffset, utf16);
            return true;
        }

        private static void Work()
        {
            while (_running)
            {
                try
                {
                    if (Config.SpoofNameEnabled && Core.LocalPlayer != 0)
                        ApplyToPlayer(Core.LocalPlayer);
                }
                catch { }

                Thread.Sleep(Config.SpoofNameEnabled ? ApplyIntervalMs : 200);
            }
        }
    }
}
