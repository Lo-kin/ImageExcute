using System;
using System.Drawing;
using OpenCvSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;

namespace ImageExcute
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Program program = new Program();
            program.UI();
        }

        public static bool Exit()
        {
            return true;
        }

        public bool UI()
        {
            string FilePath = "";
            Mat mat = null;
            Mat[] Frames = [];

            ProcessData pd0 = new ProcessData(0);
            ProcessBar processBar0 = new ProcessBar(() => { return MakeShakeFrames(mat, 12, out Frames, pd0); }, pd0);
            ProcessBar processBar1 = new ProcessBar(() => { return TransToGif(mat.Cols, mat.Rows, Frames, 50, pd0); }, pd0);
            ProcessBar processBar2 = new ProcessBar(() => { return TransToChar(new Mat(FilePath , ImreadModes.Grayscale).Threshold(0, 255, ThresholdTypes.Otsu), 1, pd0); }, pd0);
            processBar0.Name = "Make Frames";
            processBar1.Name = "Trans Mats To Gif";
            processBar2.Name = "Trans Mat To Char Image";
            Selection selection = new Selection();
            selection.AddSelection("Shake Frames" , () => {return processBar0.Run();});
            selection.AddSelection("Transform Image To Chars", () => { return processBar2.Run();});
            selection.AddSelection("Get Shake Frames GIF", () => { return processBar1.Run(); });
            selection.AddSelection("Exit", () => { selection.ShowInfo(0, "Press ESC To Escape");return true; });
            selection.AddSelection("Choose File Path", () => {selection.ShowInfo(0,"Image Path>") ; FilePath = Console.ReadLine().Replace("\"", "");mat = Cv2.ImRead(FilePath); return true; });

            selection.Run();

            return true;
        }

        public bool MakeShakeFrames(Mat mat, int Count , out Mat[] Frames, ProcessData? PD = null)
        {
            if (PD != null)
            {
                PD.TotalTask = Count * mat.Cols * mat.Rows;
            }
            Frames = new Mat[Count];
            if (mat == null)
            {
                return false;
            }
            if (mat.Empty() == true || Count < 0 || Count >= 256)
            {
                return false;
            }
            Mat[] OutFrames = new Mat[Count];
            int[] frameoffset = new int[Count];
            for (int i = 0; i < Count; i++)
            {
                OutFrames[i] = new Mat(mat.Rows, mat.Cols, mat.Type());
                frameoffset[i] = (int)Random.Shared.NextInt64((long)(-mat.Cols * 0.2d), (long)(mat.Cols * 0.2d));
            }
            foreach (var frame in OutFrames)
            {
                for (int y = 0; y < mat.Rows; y++)
                {
                    int xoffset = (int)Random.Shared.NextInt64(-10, 10);
                    for (int x = 0; x < mat.Cols; x++)
                    {
                        Vec3b p = mat.Get<Vec3b>(y, x);
                        byte greyvalue = (byte)((p.Item0 + p.Item2 + p.Item1) / 3);
                        Vec3b grey = new Vec3b(p.Item0, p.Item1, p.Item2);
                        frame.Set<Vec3b>(y, x + xoffset, grey);
                        if (PD != null)
                        {
                            PD.Current++;
                        }
                    }
                }
            }
            Frames = OutFrames;
            if (PD != null)
            {
                PD.TaskOver();
            }
            return true;
        }

        public string Get2x4Char(byte[,] group , byte rate)
        {
            int _dexOrigin = 0;
            for (int y = 0; y < group.GetLength(1); y++) 
            {
                for (int x = 0; x < group.GetLength(0); x++)
                {
                    if (group[x,y] >= rate)
                    {
                        _dexOrigin += 1 * (int)Math.Pow(2, y + (x * 4));
                    }
                }
            }
            return BrailleChar.GetChar(_dexOrigin);
        }

        public bool TransToGif(int width , int height , Mat[] Frames , int Delay , ProcessData? PD = null)
        {
            if (PD != null)
            {
                PD.TotalTask = Frames.Length;
            }
            using Image<Rgba32> gif = new(width, height);
            gif.Metadata.GetGifMetadata().RepeatCount = 0;
            gif.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay = 10;
            foreach (var frame in Frames)
            {
                using Image image = Image.Load(frame.ToBytes());
                image.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay = 10;

                gif.Frames.AddFrame(image.Frames.RootFrame);
                if (PD != null)
                {
                    PD.Current++;
                }
            }
            gif.Frames.RemoveFrame(0);
            gif.SaveAsGif("output.gif");
            if (PD != null)
            {
                PD.TaskOver();
            }
            return true;
        }

        public bool TransToChar(Mat mat ,byte threshold,  ProcessData? PD = null)
        {
            if (PD != null)
            {
                PD.TotalTask = mat.Cols * mat.Rows;
            }
            string ImageResult = "";
            Mat newmat = new Mat(mat.Rows , mat.Cols , mat.Type());
            for (int y = 0; y < mat.Rows; y += 4)
            {
                for (int x = 0; x <= mat.Cols; x += 2)
                {
                    byte[,] bytes = new byte[2, 4];
                    for (int _cely = y; _cely < Math.Min(y + 4 , mat.Rows); _cely++)
                    {
                        for (int _celx = x; _celx < Math.Min(x + 2 , mat.Cols); _celx++)
                        {
                            Vec3b p = mat.Get<Vec3b>(_cely, _celx);
                            byte greyvalue = (byte)((p.Item0 + p.Item2 + p.Item1) / 3);
                            bytes[_celx - x, _cely - y] = greyvalue;
                            if (PD != null)
                            {
                                PD.Current++;
                            }
                        }
                    }
                    ImageResult += Get2x4Char(bytes, 140);
                }
                ImageResult += "\n";
            }
            File.WriteAllText("test.txt", string.Empty);
            using StreamWriter sw = new StreamWriter("test.txt" , false);
            sw.Write(ImageResult);
            sw.Flush();
            ImageResult = string.Empty;
            if (PD != null)
            {
                PD.TaskOver();
            }
            
            return true;
        }
    }

    public delegate bool TaskDelegate();

    public class ProcessBar
    {
        public string Name = "Default Task";
        public TaskDelegate Task = null;
        public ProcessData PD = null;

        public int CurTop = 0;

        public ProcessBar(TaskDelegate task, ProcessData pD)
        {
            Task = task;
            PD = pD;
        }

        public bool Run()
        {
            if (Task == null|| PD == null)
            {
                return false;
            }
            CurTop = 10;
            Thread t = new Thread(() => 
            {  
                while (true)
                {
                    string tmp = Name + " [";
                    Console.CursorTop = CurTop;
                    double perc;
                    if (PD.TotalTask == 0)
                    {
                        perc = 0;
                    }
                    else
                    {
                        perc = PD.Current / PD.TotalTask;
                    }
                    for (double i = perc; i > 0.1d;i-=0.1d)
                    {
                        tmp += "#";
                    }
                    tmp += "]" + PD.Current + "/" + PD.TotalTask;
                    Console.WriteLine(tmp);
                    if (PD.Over == true)
                    {
                        break;
                    }
                    Thread.Sleep(500);
                }
                Console.WriteLine("Task Done!");
            });
            t.Start();
            Task.Invoke();
            return true;
        }
    }

    public class ProcessData
    {
        public double TotalTask = 0;
        public double Current = 0;
        public bool Over = false;

        public ProcessData(double total) 
        {
            TotalTask = total;
        }

        public bool TaskOver()
        {
            Over = true;
            TotalTask = 0; Current = 0;
            return true;
        }
    }

    public struct BrailleChar
    {
        /*
            1 5
            2 6
            3 7
            4 8 binary
         *///useless struct current
        public int _dec;
        public int Dec
        {
            get
            {
                return _dec;
            }
            set
            {
                _dec = Math.Clamp(value, 0, 255);
            }
        }

        public bool[,] GetBoolImage()
        {
            bool[,] Image = new bool[2, 4];
            string orign = Convert.ToString(Dec, 2);
            int _x = 0;int _y = 0;
            foreach (var item in orign)
            {
                _x++;
                if (_x >= 3)
                {
                    _x = 0;
                    _y++;
                }
                if (item.ToString() == "0")
                {
                    Image[_x, _y] = false;
                }
                else
                {
                    Image[_x, _y] = true;
                }
            }
            return Image;
        }

        public string GetChar()
        {
            return char.ConvertFromUtf32(Dec + 10240);
        }

        public static string GetChar(int num)
        {
            num = Math.Clamp(num, 0, 255);
            return char.ConvertFromUtf32(num + 10240);
        }
    }

    public class Selection
    {
        public Dictionary<string, Func<bool >> Functions = [];
        public int _curPos = 0;
        public int CurPos
        {
            get
            {
                return _curPos;
            }
            set
            {
                if (value >= 0 || Functions.Count > value)
                {
                    _curPos = value;
                }
            }
        }

        public bool ShowOptions()
        {
            for (int i = 0; i < Functions.Count; i++)
            {
                if (i == CurPos)
                {
                    Console.BackgroundColor = ConsoleColor.Green;
                    Console.ForegroundColor = ConsoleColor.Black;
                }
                Console.WriteLine(i + " . " + Functions.Keys.ToArray()[i]);
                Console.ResetColor();
            }
            return true;
        }

        public bool AddSelection(string Name , Func<bool> func)
        {
            if (Name == null || func == null)
            {
                return false;
            }
            Functions.Add(Name, func);
            return true;
        }

        public bool ShowInfo(int pos , string info)
        {
            Console.CursorLeft = 0;
            Console.CursorTop = Functions.Count;
            Console.Write(new string(" ".ToCharArray()[0], Console.BufferWidth));
            Console.CursorLeft = 0;
            Console.Write("[{0}]{1}" , pos , info);
            return true;
        }

        public bool Run()
        {
            Console.Clear();
            while (true)
            {
                ShowOptions();
                var key = Console.ReadKey();
                if (key.Key == ConsoleKey.DownArrow)
                {
                    CurPos += 1;
                }
                else if (key.Key == ConsoleKey.UpArrow)
                {
                    CurPos -= 1;
                }
                else if (key.Key == ConsoleKey.Enter)
                {
                    Functions.Values.ToArray()[CurPos].Invoke();
                }
                else if (key.Key == ConsoleKey.Escape) 
                {
                    break;
                }
                Console.CursorLeft = 0;
                Console.CursorTop = 0;
            }
            return true;
        }

    }
}
