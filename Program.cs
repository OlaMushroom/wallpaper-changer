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
    static extern int SystemParametersInfo(uint action, uint uParam, string vParam, uint winIni);

    public static void SetWallpaper(string path, string style) {
        RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true);

        key.SetValue(@"WallpaperStyle", 0.ToString()); // Default is Center
        key.SetValue(@"TileWallpaper", 0.ToString());

        if (style == "fill") {
            key.SetValue(@"WallpaperStyle", 10.ToString());
        }
        else if (style == "fit") {
            key.SetValue(@"WallpaperStyle", 6.ToString());
        }
        else if (style == "span") {
            key.SetValue(@"WallpaperStyle", 22.ToString());  // Windows 8 or newer only!
        }
        else if (style == "stretch") {
            key.SetValue(@"WallpaperStyle", 2.ToString());
        }
        else if (style == "tile") {
            key.SetValue(@"TileWallpaper", 1.ToString());
        }

        _ = SystemParametersInfo(20, 0, path, 1 | 2);
    }
}

class Program {
    struct Arguments {
        public string param, style;
        public bool isJPEG, changeWallpaper;
        public int interval;

        public Arguments(string vParam, bool vIsJPEG, bool vChangeWallpaper, string vStyle, int vInterval) {
            param = vParam;
            isJPEG = vIsJPEG;
            changeWallpaper = vChangeWallpaper;
            style = vStyle;
            interval = vInterval;
        }
    }

    static readonly HttpClient client = new();

    static async Task FetchImage(Arguments arguments) {
        string URL = $"https://konachan.com/post.json?{arguments.param}";
        Console.WriteLine($"URL: {URL}\n");

        try {
            HttpResponseMessage response = await client.GetAsync(URL);
            response.EnsureSuccessStatusCode();

            dynamic[] data = JsonSerializer.Deserialize<dynamic[]>(await response.Content.ReadAsStringAsync())!;
            foreach (var element in data) {
                ImageInfo image = JsonSerializer.Deserialize<ImageInfo>(element);

                string? imageURL = arguments.isJPEG ? image.jpeg_url : image.file_url;

                string? imageName = $"{UrlDecode(imageURL!).Split("/")[^1]}";
                string fileName = Regex.Replace(imageName, @"[<>/\|?:*]", " ");

                Console.WriteLine($"\nName: {imageName}\nSource: {image.source}\nURL: {imageURL}\nFile: {fileName}\n");

                string fullPath = Path.GetFullPath("./images/", Directory.GetCurrentDirectory());
                Directory.CreateDirectory(fullPath);

                string filePath = fullPath + fileName;
                File.WriteAllBytes(filePath, await client.GetByteArrayAsync(imageURL));
                Console.WriteLine($"File written: {filePath}\n");

                if (arguments.changeWallpaper == true) {
                    ChangeWallpaper.SetWallpaper(filePath, "center");
                    Console.WriteLine($"Wallpaper changed: {filePath}\n");

                    await Task.Delay(arguments.interval);
                }
            }
        }
        catch (HttpRequestException e) {
            Console.WriteLine($"\nException Caught!\nMessage: {e.Message}");
        }
    }

    static async Task Main(string[] args) {
        var interval = new Option<int>("--interval", () => 60, "Set interval (seconds).");
        var changeWallpaper = new Option<bool>("--change", () => false, "Change wallpaper.");
        var style = new Option<string>("--style", () => "center", "Set style.");
        var isJPEG = new Option<bool>("--jpeg", () => false, "Whether to use JPEG or not.");
        var page = new Option<int>("--page", () => 1, "Set page.");
        var limit = new Option<int>("--limit", () => 1, "Set a limited amount of requests."); limit.AddAlias("l");
        var tags = new Option<string>("--tags", () => "", "Set tags."); tags.AddAlias("t");

        var rootCommand = new RootCommand() { changeWallpaper, interval, isJPEG, limit, page, style, tags };

        rootCommand.SetHandler(async (vChangeWallpaper, vInterval, vIsJPEG, vLimit, vPage, vStyle, vTags) => {
            Arguments arguments = new($"page={vPage}&limit={vLimit}&tags={vTags}", vIsJPEG, vChangeWallpaper, vStyle, vInterval * 1000);
            Console.WriteLine($"Interval: {arguments.interval / 1000}s\nChange Wallpaper: {arguments.changeWallpaper}\nUse JPEG: {arguments.isJPEG}\n");

            await FetchImage(arguments);
        }, changeWallpaper, interval, isJPEG, limit, page, style, tags);

        await rootCommand.InvokeAsync(args);
    }
}
