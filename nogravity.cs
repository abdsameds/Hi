using AotForms;
using System;
using System.Numerics;
using System.Threading;

namespace AotForms
{
    public static class Input
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        public static bool IsKeyPressed(int keyCode)
        {
            return (GetAsyncKeyState(keyCode) & 0x8000) != 0;
        }

        public const int KEY_W = 0x57;
        public const int KEY_A = 0x41;
        public const int KEY_S = 0x53;
        public const int KEY_D = 0x44;
        public const int KEY_SPACE = 0x20;
        public const int KEY_LCONTROL = 0xA2;
    }

    public static class KCNOGRAVITYFLY
    {
        // OFFSETS
        private const uint MOVEMENT_COMPONENT_OFFSET = 0X124C;
        private const uint POSITION_OFFSET = 0X20;
        private const uint VSPEED_OFFSET = 0x2C;
        private const uint IS_GROUNDED_OFFSET = 0x150;


        // SPEED CONFIG
        private const float HORIZONTAL_SPEED = 6.0f;
        private const float UP_SPEED = 3.0f;
        private const float DOWN_SPEED = -3.0f;
        private const float HOVER_SPEED = 0.02f;

        // HEIGHT LIMIT CONFIG (<<< FIX)
        private const float MAX_FLY_HEIGHT = 9000.0f; // base height + 12 meters
        private static float BaseHeight = 0f;
        private static bool HeightInitialized = false;

        private static CancellationTokenSource _cts;
        public static bool IsRunning => _cts != null && !_cts.IsCancellationRequested;

        public static void SetState(bool enable)
        {
            Config.FlyHack = enable;

            if (enable)
            {
                HeightInitialized = false;
                Start();
            }
            else
            {
                Stop();
            }
        }

        private static void Start()
        {
            if (IsRunning) return;
            _cts = new CancellationTokenSource();
            new Thread(() => Loop(_cts.Token)) { IsBackground = true }.Start();
        }

        private static void Stop()
        {
            _cts?.Cancel();
            _cts = null;
        }

        private static void Loop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (!Config.FlyHack || Core.LocalPlayer == 0)
                    {
                        Thread.Sleep(40);
                        continue;
                    }

                    ApplyFly();
                }
                catch { }

                Thread.Sleep(10);
            }
        }

        private static void ApplyFly()
        {
            ulong player = Core.LocalPlayer;
            if (player == 0) return;

            // If any teleport feature is active, let it handle the position
            if (Config.TeleportAnywhere || Config.teleportmap || Config.directteleport)
                return;

            if (!InternalMemory.Read(player + MOVEMENT_COMPONENT_OFFSET, out ulong moveComp))
                return;

            if (moveComp == 0) return;

            // keep airborne
            InternalMemory.Write<byte>(moveComp + IS_GROUNDED_OFFSET, 0);

            // read position
            if (!InternalMemory.Read(moveComp + POSITION_OFFSET, out Vector3 pos))
                return;

            // init base height once
            if (!HeightInitialized)
            {
                BaseHeight = pos.Y;
                HeightInitialized = true;
            }

            float maxAllowedHeight = BaseHeight + MAX_FLY_HEIGHT;

            // camera direction
            Vector3 forward = Core.CameraForward;
            Vector3 right = Core.CameraRight;

            Vector3 fwd = Vector3.Normalize(new Vector3(forward.X, 0, forward.Z));
            Vector3 rgt = Vector3.Normalize(new Vector3(right.X, 0, right.Z));

            Vector3 moveDir = Vector3.Zero;

            if (Input.IsKeyPressed(Input.KEY_W)) moveDir += fwd;
            if (Input.IsKeyPressed(Input.KEY_S)) moveDir -= fwd;
            if (Input.IsKeyPressed(Input.KEY_D)) moveDir += rgt;
            if (Input.IsKeyPressed(Input.KEY_A)) moveDir -= rgt;

            if (moveDir.Length() > 0)
                moveDir = Vector3.Normalize(moveDir) * HORIZONTAL_SPEED;

            pos.X += moveDir.X * 0.016f;
            pos.Z += moveDir.Z * 0.016f;

            // -------- HEIGHT CLAMP (<<< MAIN FIX)
            if (pos.Y > maxAllowedHeight)
                pos.Y = maxAllowedHeight;

            InternalMemory.Write(moveComp + POSITION_OFFSET, pos);

            // vertical speed control
            float vSpeed = HOVER_SPEED;

            if (Input.IsKeyPressed(Input.KEY_SPACE) && pos.Y < maxAllowedHeight - 0.1f)
                vSpeed = UP_SPEED;
            else if (Input.IsKeyPressed(Input.KEY_LCONTROL))
                vSpeed = DOWN_SPEED;

            InternalMemory.Write(moveComp + VSPEED_OFFSET, vSpeed);
        }
    }
}
