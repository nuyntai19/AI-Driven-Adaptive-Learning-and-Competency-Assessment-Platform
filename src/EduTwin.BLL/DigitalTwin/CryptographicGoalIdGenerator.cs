using System.Security.Cryptography;

namespace EduTwin.BLL.DigitalTwin;

public class CryptographicGoalIdGenerator : IGoalIdGenerator
{
    public ulong GenerateId()
    {
        ulong id;
        do
        {
            byte[] bytes = new byte[8];
            RandomNumberGenerator.Fill(bytes);
            id = System.BitConverter.ToUInt64(bytes, 0);
        } while (id == 0);

        return id;
    }
}
