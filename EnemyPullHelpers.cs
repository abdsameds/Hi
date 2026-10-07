using System.Runtime.InteropServices;

namespace AotForms
{
    internal static class EnemyPullHelpers
    {
        private const int VK_LBUTTON = 0x01;

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        internal static bool IsLocalFiring()
        {
            if ((GetAsyncKeyState(VK_LBUTTON) & 0x6000) != 0)
                return true;

            if (Core.LocalPlayer == 0)
                return false;

            if (InternalMemory.Read(Core.LocalPlayer + Offsets.IsFiring, out bool isFiring) && isFiring)
                return true;

            if (InternalMemory.Read(Core.LocalPlayer + Offsets.IS_FIRING, out bool isFiringAlt) && isFiringAlt)
                return true;

            return false;
        }

        internal static float GetPullFovRadius()
        {
            if (Config.AimFov > 0f)
                return Config.AimFov;
            return Config.AimBotFov;
        }
    }
}
