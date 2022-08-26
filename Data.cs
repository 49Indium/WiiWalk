using System.IO;

namespace WiiBalanceWalker
{
    public class DataWriter
    {
        public void WriteData(int dataPoint)
        {
            using (StreamWriter file = new StreamWriter("test.txt", append: true))
            {

                file.WriteLine(dataPoint.ToString());
            }
        }
    }
}
