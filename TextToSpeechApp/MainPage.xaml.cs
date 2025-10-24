using FFMpegCore;
using FFMpegCore.Enums;
using SkiaSharp;
using System.Text.RegularExpressions;
using TextToSpeechApp.Interfaces;

namespace TextToSpeechApp;

public partial class MainPage : ContentPage
{
    private readonly ITtsSaver _ttsSaver;
    private CancellationTokenSource _cancellationTokenSource;
    private SpeechOptions _speechOptions;
    private List<Locale> _availableLocales;
    private string _webText;
    private bool _isPlaying;
    private const int VideoWidth = 640;
    private const int VideoHeight = 480;
    private const int FrameRate = 30;
    private const int DurationSeconds = 5;  // Adjust for longer videos
    private const string WebUrl = "https://catdir.loc.gov/catdir/samples/random045/2002031355.html";  // Replace with your URL

    public MainPage(ITtsSaver ttsSaver)
    {
        InitializeComponent();
        _ = LoadAvailableLanguagesAsync();
        _ = LoadWebContentAsync();
        _ = CopyFFmpegBinaryAsync();
        _ttsSaver = ttsSaver;
        GlobalFFOptions.Configure(options => options.BinaryFolder = GetFFmpegBinaryPath());
    }

    // Get FFmpeg binary path (platform-specific; bundle binaries in Platforms/[Platform]/Resources/Raw/)
    private string GetFFmpegBinaryPath()
    {
        if (DeviceInfo.Platform == DevicePlatform.Android)
            return Path.Combine(FileSystem.AppDataDirectory, "ffmpeg");
        if (DeviceInfo.Platform == DevicePlatform.iOS)
            return Path.Combine(FileSystem.AppDataDirectory, "ffmpeg");
        if (DeviceInfo.Platform == DevicePlatform.WinUI)
            return Path.Combine(FileSystem.AppDataDirectory, "ffmpeg.exe");
        return "ffmpeg"; // System PATH fallback
    }

    private async Task CopyFFmpegBinaryAsync()
    {
        try
        {
            string assetName = DeviceInfo.Platform == DevicePlatform.WinUI ? "ffmpeg.exe" : "ffmpeg";
            using var stream = await FileSystem.OpenAppPackageFileAsync(assetName);
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            await File.WriteAllBytesAsync(GetFFmpegBinaryPath(), memoryStream.ToArray());
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to copy FFmpeg binary: {ex.Message}", "OK");
        }
    }

    private async Task LoadAvailableLanguagesAsync()
    {
        try
        {
            _availableLocales = new List<Locale>(await TextToSpeech.Default.GetLocalesAsync());
            LanguagePicker.Items.Clear();
            foreach (var locale in _availableLocales)
            {
                LanguagePicker.Items.Add($"{locale.Name} ({locale.Language}-{locale.Country})");
            }
            if (LanguagePicker.Items.Count > 0)
            {
                LanguagePicker.SelectedIndex = 0;
            }
            else
            {
                await DisplayAlert("Warning",
                    "No TTS voices found. Please install voices:\n" +
                    "- Android: Settings > System > Languages & input > Text-to-speech output > Install voice data\n" +
                    "- iOS: Settings > Accessibility > Spoken Content > Voices\n" +
                    "- Windows: Settings > Time & Language > Speech > Manage voices",
                    "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load languages: {ex.Message}", "OK");
        }
    }

    private async Task LoadWebContentAsync()
    {
        try
        {
            using var client = new HttpClient();
            string html = await client.GetStringAsync(WebUrl);
            _webText = ExtractTextFromHtml(html);
            WebView.Source = WebUrl;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load webpage: {ex.Message}\nConnect to the internet and try again.", "OK");
        }
    }

    private string ExtractTextFromHtml(string html)
    {
        string text = Regex.Replace(html, "<[^>]+>", " ");
        text = Regex.Replace(text, @"\s+", " ").Trim();
        return text;
    }

    private void OnLanguageSelected(object sender, EventArgs e)
    {
        if (LanguagePicker.SelectedIndex >= 0 && _availableLocales != null)
        {
            var selectedLocale = GetLocaleFromIndex(LanguagePicker.SelectedIndex);
            _speechOptions = new SpeechOptions
            {
                Locale = selectedLocale,
                Pitch = 1.0f,
                Volume = 1.0f
            };
        }
    }

    private Locale GetLocaleFromIndex(int index)
    {
        if (_availableLocales == null || index < 0 || index >= _availableLocales.Count)
        {
            return _availableLocales?.Count > 0 ? _availableLocales[0] : null;
        }
        return _availableLocales[index];
    }

    private async void OnTtsButtonClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_webText))
        {
            await DisplayAlert("Error", "No text extracted from webpage.", "OK");
            return;
        }

        if (!_isPlaying)
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
            _isPlaying = true;
            TtsButton.Text = "Pause";
            TtsStatusLabel.Text = "Playing";

            try
            {
                if (_speechOptions == null)
                {
                    _speechOptions = new SpeechOptions { Pitch = 1.0f, Volume = 1.0f };
                }

                if (_speechOptions.Locale != null)
                {
                    await DisplayAlert("Debug",
                        $"Speaking with: {_speechOptions.Locale.Name} ({_speechOptions.Locale.Language}-{_speechOptions.Locale.Country})",
                        "OK");
                }

                await TextToSpeech.Default.SpeakAsync(_webText, _speechOptions, _cancellationTokenSource.Token);
                _isPlaying = false;
                TtsButton.Text = "Play";
                TtsStatusLabel.Text = "Stopped";
            }
            catch (OperationCanceledException)
            {
                _isPlaying = false;
                TtsButton.Text = "Play";
                TtsStatusLabel.Text = "Stopped";
            }
            catch (Exception ex)
            {
                _isPlaying = false;
                TtsButton.Text = "Play";
                TtsStatusLabel.Text = "Stopped";
                await DisplayAlert("Error", $"Failed to speak: {ex.Message}\nEnsure the selected language voice is installed.", "OK");
            }
        }
        else
        {
            _cancellationTokenSource?.Cancel();
            _isPlaying = false;
            TtsButton.Text = "Play";
            TtsStatusLabel.Text = "Paused";
        }
    }

    private async void OnVideoButtonClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_webText))
        {
            await DisplayAlert("Error", "No text extracted from webpage.", "OK");
            return;
        }

        if (_speechOptions == null)
        {
            await DisplayAlert("Error", "Please select a language first.", "OK");
            return;
        }

        TtsStatusLabel.Text = "Creating video...";
        VideoButton.IsEnabled = false;

        try
        {
            if (DeviceInfo.Platform == DevicePlatform.Android)
            {
                var status = await Permissions.RequestAsync<Permissions.StorageWrite>();
                if (status != PermissionStatus.Granted)
                {
                    throw new Exception("Storage permission denied.");
                }
            }

            string audioPath = Path.Combine(FileSystem.CacheDirectory, "narration.wav");
            await GenerateAudioWav(audioPath);

            string framesDir = Path.Combine(FileSystem.CacheDirectory, "frames");
            Directory.CreateDirectory(framesDir);
            await GenerateFrames(framesDir);

            string videoPath = Path.Combine(FileSystem.CacheDirectory, "webpage_video.mp4");
            await CreateVideoFromFramesAndAudio(framesDir, audioPath, videoPath);

            // Fixed: Correct OpenFileRequest usage
            await DisplayAlert("Success", $"Video saved to {videoPath}. Opening...", "OK");
            await Launcher.OpenAsync(new OpenFileRequest
            {
                Title = "Open Video",
                File = new ReadOnlyFile(videoPath)
            });

            TtsStatusLabel.Text = "Video created!";
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to create video: {ex.Message}", "OK");
            TtsStatusLabel.Text = "Stopped";
        }
        finally
        {
            VideoButton.IsEnabled = true;
        }
    }

    private async Task GenerateAudioWav(string outputPath)
    {
        if (_ttsSaver == null)
        {
            await DisplayAlert("Error",
                "TTS saver service not initialized. Ensure ITtsSaver is registered in MauiProgram.cs with namespace TextToSpeechApp.Platforms.Android.\n" +
                "Check Platforms/Android/TTSSaverImplementation.cs exists and compiles.\n" +
                "Rebuild the solution and target Android platform.",
                "OK");
            throw new InvalidOperationException("TTS saver service not initialized.");
        }
        await _ttsSaver.GenerateWavAsync(_webText, _speechOptions, outputPath);
    }

    private async Task GenerateFrames(string framesDir)
    {
        int totalFrames = FrameRate * DurationSeconds;
        for (int i = 0; i < totalFrames; i++)
        {
            using var surface = SKSurface.Create(new SKImageInfo(VideoWidth, VideoHeight));
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Black);

            using var paint = new SKPaint
            {
                Color = SKColors.White,
                TextSize = 24,
                IsAntialias = true
            };
            float y = VideoHeight - (i * 5f);
            canvas.DrawText(_webText, 10, y, paint);

            string framePath = Path.Combine(framesDir, $"frame_{i:D5}.png");
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            await File.WriteAllBytesAsync(framePath, data.ToArray());
        }
    }

    // Fixed: Correct FFMpegCore arguments
    private async Task CreateVideoFromFramesAndAudio(string framesDir, string audioPath, string outputPath)
    {
        await FFMpegArguments
            .FromFileInput($"{framesDir}/frame_%05d.png", false, args => args.WithFramerate(FrameRate))
            .AddFileInput(audioPath)
            .OutputToFile(outputPath, false, args => args
                .WithVideoCodec(VideoCodec.LibX264)
               // .WithPixelFormat(PixelFormat.Yuv420p)
                .WithDuration(TimeSpan.FromSeconds(DurationSeconds)))
            .ProcessAsynchronously();
    }
}
