using AotForms;
using System.Numerics;

public static class SpinBot
{
    private static CancellationTokenSource _cts;
    private static float currentAngle = 0f;

    private const float SPIN_SPEED = 720f; // degrees per second

    // Sabse common rotation offsets (ek baar mein sirf ek try karo)
    private static readonly uint[] PossibleRotationOffsets = new uint[]
    {
        0x90,   // root transform rotation (bahut common)
        0xA0,
        0xB0,
        0xC0,
        0x222,  // tera original
        0x1C0,  // skeletal mesh common
        0x200
    };

    public static void SetState(bool enable)
    {
        if (enable)
        {
            currentAngle = 0f;
            _cts = new CancellationTokenSource();
            new Thread(Loop) { IsBackground = true }.Start();
        }
        else
        {
            _cts?.Cancel();
            _cts = null;
        }
    }

    private static void Loop()
    {
        while (_cts != null && !_cts.IsCancellationRequested)
        {
            try
            {
                ulong player = Core.LocalPlayer;
                if (player == 0)
                {
                    Thread.Sleep(100);
                    continue;
                }

                currentAngle += SPIN_SPEED * 0.016f; // ~60 fps delta
                if (currentAngle >= 360f) currentAngle -= 360f;

                float rad = currentAngle * (MathF.PI / 180f);

                // Safe quaternion (yaw only)
                Quaternion quat = new Quaternion(
                    MathF.Sin(rad),   // X
                    0f,               // Y
                    0f,               // Z
                    MathF.Cos(rad)    // W
                                      // Note: Kuch games mein X aur W swap hote hain, agar nahi chala to neeche comment wala try kar
                );

                // Normalize (bahut zaroori hai crash avoid karne ke liye)
                float length = MathF.Sqrt(quat.X * quat.X + quat.Y * quat.Y + quat.Z * quat.Z + quat.W * quat.W);
                if (length > 0.0001f)
                {
                    quat.X /= length;
                    quat.Y /= length;
                    quat.Z /= length;
                    quat.W /= length;
                }

                // Sirf ek offset try karo pehle (crash avoid)
                // Change index 0 to 1,2,3... ek-ek karke test karna
                uint offsetToTry = PossibleRotationOffsets[0]; // ← yaha change kar ke test kar

                InternalMemory.Write(player + offsetToTry, quat);

                // Agar upar wala crash kare to yeh try kar (comment se hata ke)
                // Quaternion alt = new Quaternion(0f, MathF.Sin(rad), 0f, MathF.Cos(rad));
                // InternalMemory.Write(player + offsetToTry, alt);

                Thread.Sleep(10); // 100 fps → crash kam hone ka chance zyada
            }
            catch
            {
                // Silent catch taaki loop band na ho
                Thread.Sleep(200);
            }
        }
    }
}