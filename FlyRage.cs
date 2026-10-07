using System;
using System.Threading;
using System.Threading.Tasks;

namespace AotForms
{
    internal static class FlyRage
    {
        private static Task _task;
        private static CancellationTokenSource _cts;
        private static bool _running;

        internal static void SetState(bool enable)
        {
            Config.FlyRage = enable;
            if (enable)
                Start();
            else
                Stop();
        }

        private static void Start()
        {
            if (_running) return;
            _running = true;
            _cts = new CancellationTokenSource();
            _task = Task.Run(() => Loop(_cts.Token), _cts.Token);
        }

        private static void Stop()
        {
            _cts?.Cancel();
            _running = false;
        }

        private static async Task Loop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (!Config.FlyRage || Core.LocalPlayer == 0)
                    {
                        await Task.Delay(40, token);
                        continue;
                    }

                    ulong lp = Core.LocalPlayer;
                    if (InternalMemory.Read<uint>(lp + 0x139C, out uint mc) && mc != 0)
                    {
                        InternalMemory.Write<byte>(lp + 0x13F0, 0);

                        float vSpeed = (WinAPI.GetAsyncKeyState((Keys)0x20) & 0x8000) != 0 ? 3.0f :
                                       (WinAPI.GetAsyncKeyState((Keys)0x01) & 0x8000) != 0 ? -3.0f : 0.02f;

                        InternalMemory.Write(mc + 0x2C, vSpeed);
                    }
                }
                catch { }

                await Task.Delay(10, token);
            }
        }
    }
}
