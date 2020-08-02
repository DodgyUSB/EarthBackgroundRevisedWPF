﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.IO;
using System.Net;
using System.Globalization;
using System.Drawing.Imaging;
using System.Security.Policy;
using System.Xml.Schema;
using System.Threading;
using System.Diagnostics;
using Path = System.IO.Path;

namespace EarthBackgroundRevisedWPF
{
    class EarthBackgroundCore
    {
        private int _Res;
        private string _FilePath;
        private Task<bool> activeUpdate;
        private const int nullImageStringLength = 2834;
        private int _CompletedSubimages;
        public static event EventHandler<DownloadStatusChangedEventArgs> DownloadStatusChanged;
        private static event EventHandler<EventArgs> SubImageComplete;
        public static event EventHandler<UpdateCompleteEventArgs> UpdateComplete;

        public string getLatestImagePath()
        {
            return Path.Combine(_FilePath, string.Format("EarthBackground-{0}.png", getLatestStoredCode(_FilePath)));
        }

        public enum siteOption
        {
            Himawari,
            rammbSlider,
            HimawariBanded
        }

        public EarthBackgroundCore(int res, string filePath)
        {
            _Res = res;
            _FilePath = filePath;
        }

        private static void raiseDownloadStatusChangedEvent(string message, int percentage)
        {
            EventHandler<DownloadStatusChangedEventArgs> temp = DownloadStatusChanged;
            if (temp != null)
            {
                DownloadStatusChanged(null, new DownloadStatusChangedEventArgs(message, percentage));
            }
        }

        private static void RaiseSubImageCompleteEvent()
        {
            EventHandler<EventArgs> temp = SubImageComplete;
            if(temp != null)
            {
                temp(null, new EventArgs());
            }
        }

        private static void RaiseUpdateCompleteEvent(string message)
        {
            EventHandler<UpdateCompleteEventArgs> temp = UpdateComplete;
            if(temp != null)
            {
                temp(null, new UpdateCompleteEventArgs(message));
            }
        }

        public Task<bool> update(siteOption option)
         {
             if (activeUpdate == null || activeUpdate.Status != TaskStatus.Running)
             {
                 activeUpdate = new Task<bool>(() => updateFunc(option, _Res, _FilePath));
                 SubImageComplete += EarthBackgroundCore_SubImagecomplete;
                 activeUpdate.Start();
                UpdateComplete += EarthBackgroundCore_UpdateComplete;
                 return activeUpdate;
             }
             else
             {
                 return null;
             }
         }

        private void EarthBackgroundCore_UpdateComplete(object sender, UpdateCompleteEventArgs e)
        {
            //activeUpdate.Dispose();
        }

        private void EarthBackgroundCore_SubImagecomplete(object sender, EventArgs e)
        {
            _CompletedSubimages++;
            int percentage = (_CompletedSubimages / (_Res * _Res)) * 100;
            raiseDownloadStatusChangedEvent(string.Format("Downloading", percentage), percentage);
        }

        private Func<siteOption, int, string, bool> updateFunc = new Func<siteOption, int, string, bool>((siteOption siteSelection, int res, string filePath) =>
        {
            raiseDownloadStatusChangedEvent("Update Starting", 0);
            Stopwatch stopwatch = new Stopwatch();
            Console.WriteLine("Starting stopwatch");
            stopwatch.Start();
            int bands = 1;
            int subImageSize = 0;
            Queue<MemoryStream> streams = new Queue<MemoryStream>();
            List<Task<MemoryStream>> downloadTasks = new List<Task<MemoryStream>>();
            switch (siteSelection)
            {
                case siteOption.Himawari:
                    bands = 1;
                    subImageSize = 550;
                    break;
                case siteOption.rammbSlider:
                    bands = 3;
                    subImageSize = 688;
                    break;
                case siteOption.HimawariBanded:
                    bands = 3;
                    subImageSize = 550;
                    break;
            }
            DateTime ImageTime = getNextAvaliableTime(siteSelection, res);
            Console.WriteLine("Image found at time: {0}", ImageTime);
            raiseDownloadStatusChangedEvent(string.Format("Latest image found at {0}", ImageTime), 0);
            Console.WriteLine("DownloadStatusChangedEvent raised");
            if (Convert.ToInt64(getFileCode(ImageTime)) > Convert.ToInt64(getLatestStoredCode(filePath)))
            {
                raiseDownloadStatusChangedEvent("Download starting", 0);
                int imageSize = res * subImageSize;
                Queue<Bitmap> bitmaps = new Queue<Bitmap>();
                if (bands > 1)
                {
                    List<Task<Bitmap>> MergeTasks = new List<Task<Bitmap>>();
                    for (int x = 0; x < res; x++)
                    {
                        for (int y = 0; y < res; y++)
                        {
                            Console.WriteLine("Starting download of Image {0}-{1}", x, y);
                            Uri R = buildURL(siteSelection, ImageTime, x, y, res, 2);
                            Uri G = buildURL(siteSelection, ImageTime, x, y, res, 1);
                            Uri B = buildURL(siteSelection, ImageTime, x, y, res, 0);
                            Task<Bitmap> currentMerge = new Task<Bitmap>(() => DownloadCombinedBandedSubImage(R, G, B));
                            MergeTasks.Add(currentMerge);
                            currentMerge.Start();
                        }
                    }
                    Console.WriteLine("waiting for Merging to complete");
                    Task.WaitAll(MergeTasks.ToArray());
                    Console.WriteLine("Merging Complete");
                    MergeTasks.ForEach(currentTask =>
                    {
                        bitmaps.Enqueue(currentTask.Result);
                        currentTask.Dispose();
                    });
                    raiseDownloadStatusChangedEvent("Download Complete", 100);
                }
                else
                {
                    List<Task<MemoryStream>> downloads = new List<Task<MemoryStream>>();
                    for (int x = 0; x < res; x++)
                    {
                        for (int y = 0; y < res; y++)
                        {
                            Console.WriteLine("Starting download of Image {0}-{1}", x, y);
                            Uri currentImage = buildURL(siteSelection, ImageTime, x, y, res, 0);
                            Task<MemoryStream> currentDownloadTask = new Task<MemoryStream>(() => downloadImageToMemStream(currentImage));
                            downloads.Add(currentDownloadTask);
                            currentDownloadTask.Start();
                        }
                    }
                    Task.WaitAll(downloads.ToArray());
                    downloads.ForEach(current =>
                    {
                        bitmaps.Enqueue(new Bitmap(current.Result));
                        current.Dispose();
                    });
                }
                raiseDownloadStatusChangedEvent("Stitching", 100);
                Console.WriteLine("Stitching");
                using (Bitmap image = new Bitmap(imageSize, imageSize))
                {
                    using (Graphics g = Graphics.FromImage(image))
                    {
                        for (int x = 0; x < res; x++)
                        {
                            for (int y = 0; y < res; y++)
                            {
                                Bitmap currentSubImage = bitmaps.Dequeue();
                                g.DrawImage(currentSubImage, x * subImageSize, y * subImageSize);
                                currentSubImage.Dispose();
                            }
                        }
                    }
                    Console.WriteLine("Saving file");
                    clearDirectoy(filePath);
                    string fileName = string.Format("EarthBackground-{0}.png", getFileCode(ImageTime));
                    string fullPath = Path.Combine(filePath, fileName);
                    Console.WriteLine("Saving to path: {0}", fullPath);
                    image.Save(fullPath, ImageFormat.Png);
                }
                stopwatch.Stop();
                Console.WriteLine("Full time for download and merge: {0}ms", stopwatch.ElapsedMilliseconds);
                RaiseUpdateCompleteEvent("Update completed succesfully");
                return true;
            }
            else
            {
                raiseDownloadStatusChangedEvent("No new images avaliable", 100);
                RaiseUpdateCompleteEvent("No new images avaliable");
                return false;
            }
        });

        private static string getLatestStoredCode(string folderPath)
        {
            string largest = "0";
            DirectoryInfo direct = new DirectoryInfo(folderPath);
            foreach (FileInfo file in direct.GetFiles())
            {
                string currentCode = getCodeFromPath(file.Name);
                if (Convert.ToInt64(currentCode) > Convert.ToInt64(largest))
                {
                    largest = currentCode;
                }
            }
            return largest;
        }

        private static string getCodeFromPath(string fileName)
        {
            string name = fileName.Split('.')[0];
            if (name.Contains("-"))
            {
                string[] names = name.Split('-');
                return names[names.GetUpperBound(0)];
            }
            else
            {
                return "0";
            }
        }

        private static Func<Bitmap, Bitmap, Bitmap, Bitmap> mergeImages = new Func<Bitmap, Bitmap, Bitmap, Bitmap>((Bitmap R, Bitmap G, Bitmap B) =>
        {
            Bitmap output = new Bitmap(R.Width, R.Height);
            List<Task<int[,]>> getBrightnessTasks = new List<Task<int[,]>>();
            //Console.WriteLine("Starting R Brightness task");
            getBrightnessTasks.Add(new Task<int[,]>(() => getPixelBrightness(R)));
            //Console.WriteLine("Starting G Brightness task");
            getBrightnessTasks.Add(new Task<int[,]>(() => getPixelBrightness(G)));
            //Console.WriteLine("Starting B Brightness task");
            getBrightnessTasks.Add(new Task<int[,]>(() => getPixelBrightness(B)));
            foreach(Task<int[,]> task in getBrightnessTasks)
            {
                task.Start();
            }
            Task.WaitAll(getBrightnessTasks.ToArray());
            //Console.WriteLine("getBrightness Complete");
            int[,] RA = getBrightnessTasks[0].Result;
            int[,] GA = getBrightnessTasks[1].Result;
            int[,] BA = getBrightnessTasks[2].Result;
            for (int x = 0; x < output.Width; x++)
            {
                for(int y = 0; y < output.Height; y++)
                {
                    output.SetPixel(x, y, Color.FromArgb(RA[x,y],GA[x,y],BA[x,y]));
                }
            }
            R.Dispose();
            G.Dispose();
            B.Dispose();
            return output;
        });

        private static Func<Uri,Uri,Uri, Bitmap> DownloadCombinedBandedSubImage = new Func<Uri,Uri,Uri, Bitmap>((Uri R, Uri G, Uri B) =>
        {
            List<Task<MemoryStream>> downloadTasks = new List<Task<MemoryStream>>();
            Bitmap output;
            downloadTasks.Add(Task.Factory.StartNew(() => downloadImageToMemStream(R)));
            downloadTasks.Add(Task.Factory.StartNew(() => downloadImageToMemStream(G)));
            downloadTasks.Add(Task.Factory.StartNew(() => downloadImageToMemStream(B)));
            Task.WaitAll(downloadTasks.ToArray());
            Bitmap Rb = new Bitmap(downloadTasks[0].Result);
            Bitmap Gb = new Bitmap(downloadTasks[1].Result);
            Bitmap Bb = new Bitmap(downloadTasks[2].Result);
            output = mergeImages(Rb, Gb, Bb);
            Rb.Dispose();
            Gb.Dispose();
            Bb.Dispose();
            RaiseSubImageCompleteEvent();
            return output;
        });

        private static Func<Uri, MemoryStream> downloadImageToMemStream = new Func<Uri, MemoryStream>((Uri url) =>
        {
            MemoryStream output;
            using(WebClient client = new WebClient())
            {
                output = new MemoryStream(client.DownloadData(url));
            }
            return output;
        });

        private static Func<Bitmap, int[,]> getPixelBrightness = new Func<Bitmap, int[,]>((Bitmap image) =>
         {
             int[,] outputArray = new int[image.Width,image.Height];
             for(int x = 0; x < image.Width; x++)
             {
                 for(int y = 0; y < image.Height; y++)
                 {
                     outputArray[x, y] = (int)(image.GetPixel(x, y).GetBrightness() * 255);
                 }
             }
             return outputArray;
         });

        private static Uri buildURL(siteOption siteOption, DateTime imageTime,int x, int y, int res, int band)
        {
            int year = imageTime.Year;
            string day = addZeros(imageTime.Day, 2);
            string month = addZeros(imageTime.Month, 2);
            string utcTime = getTimeCode(imageTime);
            band++;//because they are not 0 indexed in the url
            switch (siteOption)
            {
                case siteOption.Himawari:
                    return new Uri(string.Format("https://himawari8-dl.nict.go.jp/himawari8/img/D531106/{0}d/550/{1}/{2}/{3}/{4}_{5}_{6}.png", res, year, month, day, utcTime, x, y));
                case siteOption.rammbSlider:
                    return new Uri(string.Format("https://rammb-slider.cira.colostate.edu/data/imagery/{2}{1}{0}/himawari---full_disk/band_{7}/{2}{1}{0}{3}/{4}/{5}_{6}.png", day, month, year, utcTime, addZeros((int)(Math.Log(res,2)), 2), addZeros(y, 3), addZeros(x, 3), addZeros(band, 2)));
                case siteOption.HimawariBanded:
                    return new Uri(string.Format("https://himawari8-dl.nict.go.jp/himawari8/img/FULL_24h/B{7}/{0}d/550/{1}/{2}/{3}/{4}_{5}_{6}.png", res, year, month, day, utcTime, x, y, addZeros(band, 2)));
                default:
                    return new Uri("");
            }
        }

        private static string getFileCode(DateTime testTime)
        {
            string fileTimeCode = getTimeCode(testTime);
            string year = testTime.Year.ToString();
            string month = addZeros(testTime.Month, 2);
            string day = addZeros(testTime.Day, 2);
            return year + month + day + fileTimeCode;
        }

        private static string addZeros(int value, int size)
        {
            string val = value.ToString();
            if (val.Length < size)
            {
                for (int x = val.Length; x < size; x++)
                {
                    val = "0" + val;
                }
            }
            return val;
        }
        private static string getTimeCode(DateTime currentTime)
        {
            string hour = currentTime.Hour.ToString();
            string minute = roundIntTo10(currentTime.Minute).ToString();
            if (hour.Length < 1)
            {
                hour = "00";
            }
            else if (hour.Length < 2)
            {
                hour = "0" + hour;
            }
            if (minute.Length < 2)
            {
                minute = "00";
            }
            return hour + minute + "00";
        }
        private static double roundIntTo10(int number)
        {
            return 10 * Math.Floor(number / 10f);
        }

        private static DateTime getNextAvaliableTime(siteOption option, int res)
        {
            DateTime currentTime = new DateTime(DateTime.UtcNow.Ticks);
            currentTime.AddMinutes(10);
            switch (option) {
                case siteOption.Himawari:
                    currentTime = checkImage(option, currentTime, res, nullImageStringLength);
                    break;
                case siteOption.rammbSlider:
                    currentTime = checkImage(option, currentTime, res);
                    break;
                case siteOption.HimawariBanded:
                    currentTime = checkImage(option, currentTime, res, nullImageStringLength);
                    break;
            }
            return currentTime;
        }

        private static DateTime checkImage(siteOption option, DateTime currentTime, int res)
        {
            using (WebClient client = new WebClient())
            {
                string tempLine;
                bool valid = false;
                do
                {
                    try
                    {
                        currentTime = currentTime.AddMinutes(-10);
                        Console.WriteLine("Checking Time: {0}", currentTime);
                        Console.WriteLine("url: {0}", buildURL(option, currentTime, 0, 0, res, 0));
                        tempLine = client.DownloadString(buildURL(option, currentTime, 0, 0, res, 0));
                        Console.WriteLine("url: {0}", buildURL(option, currentTime, 0, 0, res, 1));
                        tempLine = client.DownloadString(buildURL(option, currentTime, 0, 0, res, 1));
                        Console.WriteLine("url: {0}", buildURL(option, currentTime, 0, 0, res, 2));
                        tempLine = client.DownloadString(buildURL(option, currentTime, 0, 0, res, 2));
                        valid = true;
                    }
                    catch
                    {
                        Console.WriteLine("Download Error: possibly 404 trying next time");
                    }
                } while (!valid);
            }
            return currentTime;
        }

        private static DateTime checkImage(siteOption option, DateTime currentTime, int res, int imageLength)
        {
            using (WebClient client = new WebClient())
            {
                int currentImageSize;
                do
                {
                    currentTime = currentTime.AddMinutes(-10);
                    Console.WriteLine("Checking Time: {0}", currentTime);
                    Uri downloadUrl = buildURL(option, currentTime, 0, 0, res, 0);
                    try
                    {
                        Console.WriteLine("URL: {0}", downloadUrl);
                        currentImageSize = client.DownloadString(downloadUrl).Length;
                        Console.WriteLine("Current image size: {0}", currentImageSize);
                    }
                    catch
                    {
                        Console.WriteLine("ERROR");
                        currentImageSize = nullImageStringLength;
                        currentTime = currentTime.AddMinutes(10);
                    }
                } while (currentImageSize == imageLength);
            }
            return currentTime;
        }

        private static void clearDirectoy(string DirectoryPath)
        {
            foreach(string file in Directory.GetFiles(DirectoryPath))
            {
                File.Delete(file);
            }
        }

        public class DownloadStatusChangedEventArgs : EventArgs
        {
            string StatusMessage;
            int Percentage;

            public DownloadStatusChangedEventArgs(string status, int percentage)
            {
                StatusMessage = status;
                Percentage = percentage;
            }

            public string Status
            {
                get => StatusMessage;
            }

            public int percentageComplete
            {
                get => Percentage;
            }
        }

        public class UpdateCompleteEventArgs : EventArgs
        {
            public string Message { get; }
            public UpdateCompleteEventArgs(string message)
            {
                Message = message;
            }
        }

    }
}
