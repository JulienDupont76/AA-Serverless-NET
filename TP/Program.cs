using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace TP
{
    class Program
    {
        static void Main(string[] args)
        {
            string outputDirectory = Path.Combine(AppContext.BaseDirectory, "image-output");
            Directory.CreateDirectory(outputDirectory);

            string sourcePath;
            string resizedPath;
            int targetWidth = 400;
            int targetHeight = 250;

            if (args.Length >= 2)
            {
                sourcePath = args[0];
                resizedPath = args[1];

                if (args.Length >= 4)
                {
                    if (int.TryParse(args[2], out int parsedWidth))
                    {
                        targetWidth = parsedWidth;
                    }

                    if (int.TryParse(args[3], out int parsedHeight))
                    {
                        targetHeight = parsedHeight;
                    }
                }
            }
            else
            {
                sourcePath = Path.Combine(outputDirectory, "source-demo.png");
                resizedPath = Path.Combine(outputDirectory, "resized-demo.jpeg");
                CreateDemoImage(sourcePath);
            }

            ResizeImage(sourcePath, resizedPath, targetWidth, targetHeight);

            Console.WriteLine($"Source image: {sourcePath}");
            Console.WriteLine($"Resized image: {resizedPath}");
            Console.WriteLine($"Target size: {targetWidth}x{targetHeight}");
        }

        private static void CreateDemoImage(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);

            using Image<Rgba32> image = new(1200, 800);
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    Span<Rgba32> row = accessor.GetRowSpan(y);

                    for (int x = 0; x < row.Length; x++)
                    {
                        byte red = (byte)(40 + (x * 180 / row.Length));
                        byte green = (byte)(70 + (y * 150 / accessor.Height));
                        byte blue = (byte)(120 + ((x + y) % 60));
                        row[x] = new Rgba32(red, green, blue);
                    }
                }
            });

            image.SaveAsPng(path);
        }

        private static void ResizeImage(string sourcePath, string destinationPath, int width, int height)
        {
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException($"Source image not found: {sourcePath}");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? string.Empty);

            using Image image = Image.Load(sourcePath);
            image.Mutate(context => context.Resize(new ResizeOptions
            {
                Size = new Size(width, height),
                Mode = ResizeMode.Max
            }));

            image.Save(destinationPath, new JpegEncoder { Quality = 85 });
        }
    }
}
