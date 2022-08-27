using System.IO;

namespace WiiBalanceWalker
{
    public class DataWriter
    {
        StreamWriter file = new StreamWriter("test.txt", append: true);
        public DataWriter(string filePath)
        {
            file.WriteLine("Started");
        }


        public void WriteMessage(string message)
        {
            file.WriteLine(message);
        }
        public void WriteData(int dataPoint)
        {

            file.WriteLine(dataPoint.ToString());
        }
    }
}
