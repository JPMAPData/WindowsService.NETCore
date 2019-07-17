using System;
using System.IO;
using System.ServiceProcess;

namespace CoreserviceTest
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var service = new TestService())
            {
                ServiceBase.Run(service);
            }
        }
        internal class TestService : ServiceBase
        {
            public TestService()
            {
                ServiceName = "TestService";
            }

            protected override void OnStart(string[] args)
            {
                string filename = CheckFileExists();
                File.AppendAllText(filename, $"{DateTime.Now} started.{Environment.NewLine}");
            }

            protected override void OnStop()
            {
                string filename = CheckFileExists();
                File.AppendAllText(filename, $"{DateTime.Now} stopped.{Environment.NewLine}");
            }

            private static string CheckFileExists()
            {
                string filename = @"C:\Users\joao.martins\Desktop\serv.txt";
                if (!File.Exists(filename))
                {
                    File.Create(filename);
                }
                
                return filename;
            }
        }
    }
}
