using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;
using System.Diagnostics;
using NAudio.Wave;
using System.Runtime.CompilerServices;
using System.Transactions;
using System.Text.RegularExpressions;
using System.ComponentModel.DataAnnotations;

namespace bin2imgs
{
    public class Program
    {
        static long startedtime = -1;
        public static int Main(string[] args)
        {
            var rootCommand = new RootCommand("MatrixC#");
            rootCommand.Description = "ビデオプレイヤー on ターミナル C#版";
            var filenamearg = new Argument<string>("file-name");
            filenamearg.SetDefaultValue("");
            rootCommand.Add(filenamearg);
            /*
            var grayOption = new Option<bool>(new[] { "-g", "--grayscale" }, "モノクロで出力します。");
            grayOption.SetDefaultValue(false);
            rootCommand.Add(grayOption);
            */
            var newOption = new Option<bool>(new[] { "-n", "--new-output" }, "新しい方法でカラー出力を行います。縦ブレは無くなりますが、動画によっては映像がかなり遅れます。");
            newOption.SetDefaultValue(false);
            var debugOption = new Option<int>(new[] { "-d", "--debugOp" });
            debugOption.SetDefaultValue(-1);
            rootCommand.Add(newOption);
            rootCommand.Add(debugOption);
            string filename = "";
            bool new_output = false;
            int debug = -1;
            bool is_vailed = true;
            rootCommand.SetHandler((fileName, newOutput, debugOp) =>
            {
                Console.WriteLine($"filename = {fileName}");
                if (fileName == "") is_vailed = false;
                filename = fileName;
                if(!File.Exists(filename))
                {
                    Console.Error.WriteLine($"File not found: {filename}");
                    is_vailed = false;
                    Environment.Exit(1);
                }
                else
                {
                    Console.WriteLine(filename);
                }
                new_output = newOutput;
                debug = debugOp;
                //Environment.Exit(0);
            },
            filenamearg, newOption, debugOption);
            rootCommand.InvokeAsync(args).Wait();
            if(!is_vailed)
            {
                Environment.Exit(1);
            }
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true; // プログラムの終了をキャンセル
                //Console.WriteLine("Ctrl+C was pressed. Exiting...");
                Console.Clear();
                Console.Write("\x1b[0m");
                Environment.Exit(0);
            };
            //filename = "C:\\Users\\hoge\\Documents\\playground\\matrix\\RoughSketch ⧸ 777 (Official Videoclip) [xHuXXaXmStk].webm";
            filename = Path.GetFullPath(filename);
            Console.WriteLine($"Waiting for open file: {filename}");
            var mythread = new Thread(() => PlayAudioAsync(filename));
            var cap = new VideoCapture(filename);
            if(!cap.IsOpened())
            {
                Console.Error.WriteLine($"Failed to open file: {filename}");
                //Console.Error.WriteLine("Your filename may contain strange characters. Now trying to find and rename the file...");
                Console.Error.WriteLine("Your filename may contain strange characters. Please rename it.");
                /*
                var regex = new Regex("^" + Regex.Escape(Path.GetFileName(filename)).Replace("\\?", ".") + "$");
                var dirname = Path.GetDirectoryName(filename);
                if(dirname != null)
                {
                    Console.WriteLine(Path.GetInvalidFileNameChars());
                    string[] files = Directory.GetFiles(dirname, "*", SearchOption.TopDirectoryOnly);
                    if(0 < files.Length) filename = files[0];
                }
                else
                {
                    Console.Error.WriteLine($"Error while parsing filepath: {filename}");
                    Environment.Exit(1);
                }
                */
                return 1;
            }
            double fps = cap.Get(VideoCaptureProperties.Fps);
            int capw = (int)cap.Get(VideoCaptureProperties.FrameWidth);
            int caph = (int)cap.Get(VideoCaptureProperties.FrameHeight);
            int w, h;
            int lh = 0; //いらないけど怒られるの防ぐ用
            bool skip = false;
            mythread.Start();
            mythread.IsBackground = true;
            while(false)
            {
                Thread.Sleep(1000);
                Console.WriteLine(Curtime());
            }
            //while(startedtime == -1) Thread.Sleep(5);
            for(int i = 1; i <= (int)cap.Get(VideoCaptureProperties.FrameCount); i++)
            {
                w = Console.WindowWidth;
                h = Console.WindowHeight;
                if(h <= 1) h = 1;
                if(w/capw < h/caph)
                {
                    h = caph*w/capw;
                }
                else
                {
                    w = capw*h/caph;
                }
                w*=2;
                if(new_output && i != 1) Console.WriteLine("\x1b["+lh.ToString()+"F");
                var frame = new Mat();
                using(var rframe = new Mat()) //ノリでusingに
                {
                    while(!cap.Read(rframe))
                    {
                        Thread.Sleep(5);
                    }
                    if(skip)
                    {
                        skip = false;
                        continue;
                    }
                    Cv2.Resize(rframe, frame, new Size(w, h));
                }
                string text = "";
                string frametext = "";
                int[] old = [256, 256, 256];
                for(int j = 0; j < h; j++)
                {
                    text = "";
                    for(int k = 0; k < w; k++)
                    {
                        var bgr = frame.At<Vec3b>(j, k);
                        if(bgr[0] != old[0] || bgr[1] != old[1] || bgr[2] != old[2]) text += "\x1b[38;2;"+bgr[2].ToString()+";"+bgr[1].ToString()+";"+bgr[0].ToString()+"m";
                        text += "■";
                    }
                    if(new_output && i != 1) Console.WriteLine("\x1b[K"+text);
                    else if (new_output) Console.WriteLine(text);
                    else frametext += text + "\n";
                }
                lh = h+1;
                if(!new_output) Console.WriteLine(frametext);
                if(i/fps*1000 < Curtime()-startedtime) skip = true;
                else Thread.Sleep((int)((i/fps)*1000-(Curtime()-startedtime)));
            }
            Console.Clear();
            Console.Write("\x1b[0m");
            return 0;
        }
        public static void PlayAudioAsync(string videoFilePath)
        {
            var player = new WasapiOut();
            var reader = new AudioFileReader(videoFilePath);
            player.Init(reader);
            startedtime = Curtime();
            player.Play();
            while(player.PlaybackState == PlaybackState.Playing && reader.CurrentTime < reader.TotalTime)
            {
                Thread.Sleep(500);
            }
        }
        public static long Curtime() //手抜き用
        {
            return (long)DateTimeOffset.UtcNow.Subtract(DateTimeOffset.UnixEpoch).TotalMilliseconds;
        }
    }
}
