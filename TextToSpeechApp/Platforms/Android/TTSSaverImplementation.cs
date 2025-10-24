using Android.OS;
using Android.Speech.Tts;
using TextToSpeechApp;
using TextToSpeechApp.Interfaces;
using TextToSpeech = Android.Speech.Tts.TextToSpeech;


[assembly: Dependency(typeof(TTSSaverImplementation))]
namespace TextToSpeechApp;

public class TTSSaverImplementation : Java.Lang.Object, ITtsSaver, TextToSpeech.IOnInitListener
{
    private TextToSpeech _tts;
    private TaskCompletionSource<bool> _initTcs;
    private TaskCompletionSource<bool> _synthesisTcs;

    public async Task GenerateWavAsync(string text, SpeechOptions options, string outputPath)
    {
        try
        {
            _initTcs = new TaskCompletionSource<bool>();
            _synthesisTcs = new TaskCompletionSource<bool>();
            _tts = new TextToSpeech(Platform.CurrentActivity, this);
            await _initTcs.Task;

            // Set language based on options.Locale
            if (options?.Locale != null)
            {
                var locale = new Java.Util.Locale(options.Locale.Language, options.Locale.Country);
                //int result = _tts.SetLanguage(locale);
                //// Check language support using integer codes
                //if (result == TextToSpeech.LangMissingData || result == TextToSpeech.LangNotSupported)
                //{
                //    throw new Exception($"Language {options.Locale.Name} not supported or missing data.");
                //}
            }

            // Custom UtteranceProgressListener
            _tts.SetOnUtteranceProgressListener(new CustomUtteranceProgressListener
            {
                OnStartAction = utteranceId => { },
                OnDoneAction = utteranceId => _synthesisTcs.TrySetResult(true),
                OnErrorAction = (utteranceId, errorCode) => _synthesisTcs.TrySetException(new Exception($"TTS error: {errorCode}"))
            });

            var bundle = new Bundle(); // Correct Android.OS.Bundle
            bundle.PutString(TextToSpeech.Engine.KeyParamUtteranceId, "tts_video");
//                var file = new Java.IO.File(outputPath);

//#pragma warning disable CS0618 // SynthesizeToFile is deprecated but used for simplicity
//                int result = _tts.SynthesizeToFile(text, bundle, file, "tts_video");
//#pragma warning restore CS0618

//                if (result != TextToSpeech.Success)
//                {
//                    throw new Exception("Failed to start TTS synthesis.");
//                }

            await _synthesisTcs.Task;
            await Task.Delay(1000); // Additional delay to ensure file is written
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to generate WAV: {ex.Message}");
        }
        finally
        {
            _tts?.Stop();
            _tts?.Shutdown();
        }
    }

    public void OnInit(OperationResult status)
    {
        if (status == OperationResult.Success)
            _initTcs.SetResult(true);
        else
            _initTcs.SetException(new Exception("TTS initialization failed"));
    }

    // Custom class to extend UtteranceProgressListener
    private sealed class CustomUtteranceProgressListener : UtteranceProgressListener
    {
        public Action<string> OnStartAction { get; set; }
        public Action<string> OnDoneAction { get; set; }
        public Action<string, int> OnErrorAction { get; set; }

        public override void OnStart(string utteranceId)
        {
            OnStartAction?.Invoke(utteranceId);
        }

        public override void OnDone(string utteranceId)
        {
            OnDoneAction?.Invoke(utteranceId);
        }

     
        public override void OnError(string? utteranceId)
        {
            throw new NotImplementedException();
        }
    }
}