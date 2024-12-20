﻿namespace InternetSpeedTest;

/// <summary>
/// Main program
/// </summary>
class Program
{
    static void Main(string[] args)
    {
        InternetSpeedTestLib.BuildConfig();

        // Run the speed test SPEEDTEST.EXE from Ookla
        _ = InternetSpeedTestLib.SpeedTest( "speedtest.exe", "--accept-license --accept-gdpr --format=json" );

        #region defunct code
        //// default to 10MB test file
        //string testFile = "http://speedtest.tele2.net/10MB.zip";
        //string mess = "10 secs";
        //string logMess;

        //var stopWatch = new Stopwatch();

        ////_writer.AutoFlush = true;

        //if (args.Length > 0)
        //{
        //    switch (args[0])
        //    {
        //        case "1MB":
        //            testFile = "http://speedtest.tele2.net/1MB.zip";
        //            mess = "2 secs";
        //            break;

        //        case "10MB":
        //            testFile = "http://speedtest.tele2.net/10MB.zip";
        //            mess = "10 secs";
        //            break;

        //        case "100MB":
        //            testFile = "http://speedtest.tele2.net/100MB.zip";
        //            mess = "1 min";
        //            break;

        //        case "1GB":
        //            testFile = "http://speedtest.tele2.net/1GB.zip";
        //            mess = "12 mins";
        //            break;

        //        default:
        //            break;
        //    }
        //}



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
        #endregion
    }

}
