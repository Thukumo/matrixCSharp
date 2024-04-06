using System.CommandLine;
using OpenCvSharp;
using NAudio.Wave;

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
            rootCommand.SetHandler((fileName, newOutput, debugOp) =>
            {
                //Console.WriteLine($"filename = {fileName}");
                if(fileName == "")
                {
                    Console.Error.WriteLine("Please specify the name of a file to play.");
                    Environment.Exit(1);
                }
                debug = debugOp;
                new_output = newOutput;
                filename = fileName;
                filename = Path.GetFullPath(filename);
                if(!File.Exists(filename))
                {
                    Console.Error.WriteLine($"File not found: {fileName}");
                    Environment.Exit(1);
                }
                else if(debug != -1)
                {
                    Console.WriteLine(filename);
                    Thread.Sleep(1500);
                }
            },
            filenamearg, newOption, debugOption);
            rootCommand.InvokeAsync(args).Wait();
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                Console.Clear();
                Console.Write("\x1b[0m");
                Environment.Exit(0);
            };
            var mythread = new Thread(() => PlayAudioAsync(filename));
            var cap = new VideoCapture(filename);
            if(!cap.IsOpened())
            {
                Console.Error.WriteLine($"Failed to open file: {filename}");
                Console.Error.WriteLine("Name of the file may contain strange character(s). Please rename the file.");
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
            int capw = (int)cap.Get(VideoCaptureProperties.FrameWidth)*2;
            int caph = (int)cap.Get(VideoCaptureProperties.FrameHeight);
            int w, h;
            (int lh, int lw) = (0, 0); //いらないけど怒られるの防ぐ用
            bool skip = false;
            int dropframe = 0;
            mythread.Start();
            mythread.IsBackground = true;
            var fuga = cap.Get(VideoCaptureProperties.FrameCount);
            var frame = new Mat();
            while(startedtime == -1) Thread.Sleep(5);
            for(int i = 1; i < (int)cap.Get(VideoCaptureProperties.FrameCount); i++)
            {
                (w, h) = (Console.WindowWidth, Console.WindowHeight-1);
                if(h <= 1) h = 1;
                if(capw/caph > w/h)
                {
                    h = w/(capw/caph);
                }
                else
                {
                    w = h*(capw/caph);
                }
                if(new_output && i != 1 && (w != lw || h != lh)) Console.Clear();
                else if(new_output && i != 1) Console.WriteLine("\x1b["+(lh+1).ToString()+"F");
                using(var rframe = new Mat()) //ノリでusingに
                {
                    while(!cap.Read(rframe))
                    {
                        Thread.Sleep(5);
                    }
                    if(skip)
                    {
                        dropframe++;
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
                        for(int a = 0; a < 3; a++) old[a] = bgr[a];
                        text += "■";
                    }
                    if(new_output && i != 1) Console.WriteLine("\x1b[2K"+text);
                    else if (new_output) Console.WriteLine(text);
                    else frametext += text + "\n";
                }
                (lw, lh) = (w, h);
                if(!new_output) Console.WriteLine(frametext);
                if(i/fps*1000 < Curtime()-startedtime) skip = true;
                else Thread.Sleep((int)((i/fps)*1000-(Curtime()-startedtime)));
            }
            cap.Dispose();
            Console.Clear();
            var hoge = Curtime();
            while(Curtime()-hoge < 1000) Console.WriteLine("\x1b[0m"); //たまに色が戻らないのでゴリ押し
            if(debug != -1) Console.WriteLine(dropframe/fuga*100);
            Environment.Exit(0);
            return 0; //CS0161
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
                Thread.Sleep(100);
            }
            player.Dispose();
            reader.Dispose();
        }
        public static long Curtime() //手抜き用
        {
            return (long)DateTimeOffset.UtcNow.Subtract(DateTimeOffset.UnixEpoch).TotalMilliseconds;
        }
    }
}
