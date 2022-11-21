using InternetSpeedTest;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using Serilog;

namespace InternetSpeedCheck;

class Program
{
    private static readonly StreamWriter _writer = File.AppendText(@"./logfile.txt");

    static void Main(string[] args)
    {
        InternetSpeedTestLib.BuildConfig();

        // default to 10MB test file
        string testFile = "http://speedtest.tele2.net/10MB.zip";
        string mess = "10 secs";
        string logMess;

        var stopWatch = new Stopwatch();

        //_writer.AutoFlush = true;

        if (args.Length > 0)
        {
            switch (args[0])
            {
                case "1MB":
                    testFile = "http://speedtest.tele2.net/1MB.zip";
                    mess = "2 secs";
                    break;

                case "10MB":
                    testFile = "http://speedtest.tele2.net/10MB.zip";
                    mess = "10 secs";
                    break;

                case "100MB":
                    testFile = "http://speedtest.tele2.net/100MB.zip";
                    mess = "1 min";
                    break;

                case "1GB":
                    testFile = "http://speedtest.tele2.net/1GB.zip";
                    mess = "12 mins";
                    break;

                default:
                    break;
            }
        }


        _ = InternetSpeedTestLib.SpeedTest("speedtest.exe", "--accept-gdpr --format=json");

        //    logMess = $"{DateTime.Now}, Speed test start, estimated {mess}, Using {testFile}";
        //    Console.WriteLine(logMess);
        //    Log.Information(logMess);
        //    Console.WriteLine("\r\nPlease wait...\r\n");

        //    byte[] data;

        //    using (var client = new System.Net.WebClient())
        //    {
        //        stopWatch.Start();
        //        data = client.DownloadData(testFile);
        //        stopWatch.Stop();
        //    }

        //    var dataLength = (data.LongLength / (1024 * 1024));
        //    var speed = (dataLength / stopWatch.Elapsed.TotalSeconds) * 8;

        //    logMess = $"{DateTime.Now}, Took {stopWatch.Elapsed.TotalSeconds} secs, DataLength, {dataLength}MB, Speed, {speed} Mbps";
        //    Console.WriteLine(logMess);
        //    Log.Information(logMess);
    }

}
