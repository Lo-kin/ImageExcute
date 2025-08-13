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
            Console.Write("Image Path>");
            program.Test(Console.ReadLine());
        }

        public bool Test(string Path)
        {
            Mat mat = Cv2.ImRead(Path , ImreadModes.Grayscale);
            //Cv2.Resize(mat, mat, new OpenCvSharp.Size(), 0.2, 0.2, InterpolationFlags.Linear);
            Mat[] Frames = [];
            Mat OTSU = mat.Threshold(0, 255, ThresholdTypes.Otsu);
            double rate = 0.2d;
            if (mat.Empty() == true)
            {
                return false;
            }
            ProcessData pd0 = new ProcessData(0);
            ProcessBar processBar0 = new ProcessBar(() => { return MakeShakeFrames(mat, 12 ,out Frames , pd0); } , pd0);
            processBar0.Name = "Make Frames";
            processBar0.Run();
            ProcessData pd1 = new ProcessData(0);
            ProcessBar processBar1 = new ProcessBar(() => { return TransToGif(mat.Cols, mat.Rows, Frames, 50, pd1); }, pd1);
            ProcessData pd2 = new ProcessData(0);
            ProcessBar processBar2 = new ProcessBar(() => { return TransToChar(OTSU ,1 ,  pd2); }, pd2);
            processBar1.Name = "Trans Mats To Gif";
            processBar2.Name = "Trans Mat To Char Image";
            processBar1.Run();
            processBar2.Run();
            
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
            if (mat.Empty() == true)
            {
                return false;
            }
            if (Count < 0  || Count >= 256)
            {
                return false;
            }
            Mat[] OutFrames = new Mat[Count];
            int[] frameoffset = new int[Count];
            for (int i = 0; i < Count; i++)
            {
                OutFrames[i] = new Mat(mat.Rows, mat.Cols, mat.Type());
                frameoffset[i] = (int)Random.Shared.NextInt64(-500 * (long)Math.Sin(i * 10), 500 * (long)Math.Sin(i * 10));
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
                PD.Over = true;
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
            PD.Over = true;
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
                    for (int _cely = y; _cely < y + 4; _cely++)
                    {
                        for (int _celx = x; _celx < x + 2; _celx++)
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
                    ImageResult += Get2x4Char(bytes, threshold);
                }
                ImageResult += "\n";
            }
            File.WriteAllText("test.txt", string.Empty);
            using StreamWriter sw = new StreamWriter("test.txt" , false);
            sw.Write(ImageResult);
            sw.Flush();
            PD.Over = true;
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
            CurTop = Console.CursorTop;
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
}
