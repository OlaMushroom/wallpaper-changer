using System.Runtime.InteropServices;
using System.CommandLine;
using System.Text.RegularExpressions;
using System.Text.Json;
using static System.Net.WebUtility;
using Microsoft.Win32;

class ImageInfo {
    //public int? id { get; set; }
    //public string? tags { get; set; }
    //public int? created_at { get; set; }
    //public int? creator_id { get; set; }
    //public string? author { get; set; }
    //public int? change { get; set; }
    public string? source { get; set; }
    //public int? score { get; set; }
    //public string? md5 { get; set; }
    //public int? file_size { get; set; }
    public string? file_url { get; set; }
    //public bool? is_shown_in_index { get; set; }
    //public string? preview_url { get; set; }
    //public int? preview_width { get; set; }
    //public int? preview_height { get; set; }
    //public int? actual_preview_width { get; set; }
    //public int? actual_preview_height { get; set; }
    //public string? sample_url { get; set; }
    //public int? sample_width { get; set; }
    //public int? sample_height { get; set; }
    //public int? sample_file_size { get; set; }
    public string? jpeg_url { get; set; }
    //public int? jpeg_width { get; set; }
    //public int? jpeg_height { get; set; }
    //public int? jpeg_file_size { get; set; }
    //public string? rating { get; set; }
    //public bool? has_children { get; set; }
    //public dynamic? parent_id { get; set; }
    //public string? status { get; set; }
    //public int? width { get; set; }
    //public int? height { get; set; }
    //public bool? is_held { get; set; }
    //public string? frames_pending_string { get; set; }
    //public dynamic[]? frames_pending { get; set; }
    //public string? frames_string { get; set; }
    //public dynamic[]? frames { get; set; }
}

class ChangeWallpaper {
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    static extern Int32 SystemParametersInfo(UInt32 action, UInt32 uParam, String vParam, UInt32 winIni);

    public static void SetWallpaper(String path, String style) {
        RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true);

        key.SetValue(@"WallpaperStyle", 0.ToString()); // Default is Center
        key.SetValue(@"TileWallpaper", 0.ToString());

        if (style == "fill") {
            key.SetValue(@"WallpaperStyle", 10.ToString());
        }
        else if (style == "fit") {
            key.SetValue(@"WallpaperStyle", 6.ToString());
        }
        else if (style == "span") // Windows 8 or newer only!
        {
            key.SetValue(@"WallpaperStyle", 22.ToString());
        }
        else if (style == "stretch") {
            key.SetValue(@"WallpaperStyle", 2.ToString());
        }
        else if (style == "tile") {
            key.SetValue(@"TileWallpaper", 1.ToString());
        }

        SystemParametersInfo(0x14, 0, path, 0x01 | 0x02);
    }
}

class Program {
    struct Arguments {
        public string Param;
        public bool IsJPEG, ChangeWallpaper;
        public int Interval;

        public Arguments(string param, bool isJPEG, bool changeWallpaper, int interval) {
            Param = param;
            IsJPEG = isJPEG;
            ChangeWallpaper = changeWallpaper;
            Interval = interval;
        }
    }

    static readonly HttpClient client = new();

    static async Task FetchImage(Arguments arguments) {
        try {
            string URL = $"https://konachan.com/post.json?{arguments.Param}";
            Console.WriteLine($"URL: {URL}\n");

            HttpResponseMessage response = await client.GetAsync(URL);
            response.EnsureSuccessStatusCode();

            dynamic[] data = JsonSerializer.Deserialize<dynamic[]>(await response.Content.ReadAsStringAsync())!;
            foreach (var element in data) {
                ImageInfo image = JsonSerializer.Deserialize<ImageInfo>(element);

                string? imageURL = arguments.IsJPEG ? image.jpeg_url : image.file_url;

                string? imageName = $"{UrlDecode(imageURL!).Split("/")[^1]}";
                string fileName = Regex.Replace(imageName, @"[<>/\|?:*]", " ");

                Console.WriteLine($"\nName: {imageName}\nSource: {image.source}\nURL: {imageURL}\nFile: {fileName}\n");

                string fullPath = Path.GetFullPath("./images/", Directory.GetCurrentDirectory());
                Directory.CreateDirectory(fullPath);

                string filePath = fullPath + fileName;
                File.WriteAllBytes(filePath, await client.GetByteArrayAsync(imageURL));
                Console.WriteLine($"File written: {filePath}\n");

                ChangeWallpaper.SetWallpaper(filePath, "center");
                Console.WriteLine($"Wallpaper changed: {filePath}\n");

                await Task.Delay(arguments.Interval);
            }
        }
        catch (HttpRequestException e) {
            Console.WriteLine($"\nException Caught!\nMessage: {e.Message}");
        }
    }

    static async Task Main(string[] args) {
        var Interval = new Option<int>("--interval", () => 60);
        var ChangeWallpaper = new Option<bool>("--change", () => false);
        var IsJPEG = new Option<bool>("--isJPEG", () => false); IsJPEG.AddAlias("j");
        var Page = new Option<int>("--page", () => 1);
        var Limit = new Option<int>("--limit", () => 1); Limit.AddAlias("l");
        var Tags = new Option<string>("--tags", () => ""); Tags.AddAlias("t");
        var rootCommand = new RootCommand("App") { Interval, ChangeWallpaper, IsJPEG, Page, Limit, Tags };

        rootCommand.SetHandler(async (interval, changeWallpaper, isJPEG, page, limit, tags) => {
            Arguments arguments = new($"page={page}&limit={limit}&tags={tags}", isJPEG, changeWallpaper, interval * 1000);
            Console.WriteLine($"Interval: {arguments.Interval} ms\nChange Wallpaper: {arguments.ChangeWallpaper}\nisJPEG: {arguments.IsJPEG}\n");
            await FetchImage(arguments);
        }, Interval, ChangeWallpaper, IsJPEG, Page, Limit, Tags);

        await rootCommand.InvokeAsync(args);
    }
}