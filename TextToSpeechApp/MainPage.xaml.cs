using HtmlAgilityPack;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using Microsoft.Maui.Media;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace TextToSpeechApp
{
    public partial class MainPage : ContentPage
    {
        private CancellationTokenSource _cancellationTokenSource;
        private SpeechOptions _speechOptions;
        private List<Locale> _availableLocales; // Cache available locales
        private string _webText; // Extracted webpage text
        private bool _isPlaying; // Tracks TTS state
        private const string WebUrl = "https://catdir.loc.gov/catdir/samples/random045/2002031355.html"; // Replace with your URL

        public MainPage()
        {
            InitializeComponent();
            _ = LoadAvailableLanguagesAsync(); // Load languages
            _ = LoadWebContentAsync(); // Load and extract webpage text
        }

        // Load available languages (offline query)
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
                    LanguagePicker.SelectedIndex = 0; // Default to first language
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

        // Load webpage and extract text
        private async Task LoadWebContentAsync()
        {
            try
            {
                using var client = new HttpClient();
                string html = await client.GetStringAsync(WebUrl);
                _webText = ExtractTextFromHtml(html);
                WebView.Source = WebUrl; // Load webpage in WebView
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to load webpage: {ex.Message}\nConnect to the internet and try again.", "OK");
            }
        }

        // Extract text from HTML (simple regex-based)
        private string ExtractTextFromHtml(string html)
        {
            // Remove HTML tags and normalize whitespace
            string text = Regex.Replace(html, "<[^>]+>", " ");
            text = Regex.Replace(text, @"\s+", " ").Trim();
            return text;
        }

        // Handle language selection
        private void OnLanguageSelected(object sender, EventArgs e)
        {
            if (LanguagePicker.SelectedIndex >= 0 && _availableLocales != null)
            {
                var selectedLocale = GetLocaleFromIndex(LanguagePicker.SelectedIndex);
                _speechOptions = new SpeechOptions
                {
                    Locale = selectedLocale,
                    Pitch = 1.0f, // Normal pitch (0.5-2.0)
                    Volume = 1.0f // Full volume (0.0-1.0)
                };
            }
        }

        // Get locale by index with fallback
        private Locale GetLocaleFromIndex(int index)
        {
            if (_availableLocales == null || index < 0 || index >= _availableLocales.Count)
            {
                return _availableLocales?.Count > 0 ? _availableLocales[0] : null;
            }
            return _availableLocales[index];
        }

        // Handle play/pause/stop button
        private async void OnTtsButtonClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_webText))
            {
                await DisplayAlert("Error", "No text extracted from webpage.", "OK");
                return;
            }

            if (!_isPlaying)
            {
                // Start or resume playing
                _cancellationTokenSource?.Cancel(); // Stop any ongoing speech
                _cancellationTokenSource = new CancellationTokenSource();
                _isPlaying = true;
                TtsButton.Text = "Pause";
                TtsStatusLabel.Text = "Playing";

                try
                {
                    if (_speechOptions == null)
                    {
                        _speechOptions = new SpeechOptions
                        {
                            Pitch = 1.0f,
                            Volume = 1.0f
                        };
                    }

                    // Debug: Log selected locale
                    //if (_speechOptions.Locale != null)
                    //{
                    //    await DisplayAlert("Debug",
                    //        $"Speaking with: {_speechOptions.Locale.Name} ({_speechOptions.Locale.Language}-{_speechOptions.Locale.Country})",
                    //        "OK");
                    //}

                    await TextToSpeech.Default.SpeakAsync(_webText, _speechOptions, _cancellationTokenSource.Token);
                    // Reset state when speech completes
                    _isPlaying = false;
                    TtsButton.Text = "Play";
                    TtsStatusLabel.Text = "Stopped";
                }
                catch (OperationCanceledException)
                {
                    // Handle pause or stop
                    _isPlaying = false;
                    TtsButton.Text = "Play";
                    TtsStatusLabel.Text = "Stopped";
                }
                catch (Exception ex)
                {
                    _isPlaying = false;
                    TtsButton.Text = "Play";
                    TtsStatusLabel.Text = "Stopped";
                    await DisplayAlert("Error",
                        $"Failed to speak: {ex.Message}\n" +
                        "Ensure the selected language voice is installed:\n" +
                        "- Android: Settings > System > Languages & input > Text-to-speech output > Install voice data\n" +
                        "- iOS: Settings > Accessibility > Spoken Content > Voices\n" +
                        "- Windows: Settings > Time & Language > Speech > Manage voices",
                        "OK");
                }
            }
            else
            {
                // Pause or stop
                _cancellationTokenSource?.Cancel();
                _isPlaying = false;
                TtsButton.Text = "Play";
                TtsStatusLabel.Text = "Paused";
            }
        }
    }
}
